using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class BushPeekAndWalkFlipTestManager : MonoBehaviour
{
    public enum PreviewTarget { Benito, Teodora }

    public enum CatchLevel
    {
        Level1 = 1,
        Level2 = 2,
        Level3 = 3,
        Level4 = 4
    }

    [System.Serializable]
    public class LevelSpec
    {
        [Header("Objetivo / rondas")]
        [Min(1)] public int catchesToWin = 5;

        [Header("Ritmo / densidad")]
        [Range(1, 3)] public int maxSimultaneousVisible = 2;
        [Range(0f, 1f)] public float chanceExtraDistractor = 0.35f;
        [Range(0f, 1f)] public float chanceMainIsTarget = 0.60f;
        [Range(0, 5)] public int forceTargetAfterMissStreak = 2;
        public float pauseBetweenTurns = 0.45f;

        [Header("Tuning global (no teletransporte)")]
        public float bushMoveMultiplier = 1.15f;
        public float bushPeekMultiplier = 1.50f;
        public float bushHideMultiplier = 1.15f;
        public float walkSpeedMultiplier = 1.0f;

        [Header("Nivel 4 - Tiempo límite (barra)")]
        [Tooltip("Solo se usa en Nivel 4. Si llega a 0, pierdes.")]
        public float level4TimeLimitSeconds = 12f;

        [Header("Transición entre turnos")]
        public float waitExtrasToFinishTimeout = 3.0f;
    }

    // =========================
    // CORE INTEGRATION (CleverMinds)
    // =========================
    [Header("CleverMinds - Integración")]
    [Tooltip("Si no existe GameSessionManager (test directo), usa este nivel del inspector.")]
    [SerializeField] private LevelId fallbackLevelForTesting = LevelId.Level1;

    [Tooltip("Si está ON: crea/guarda Attempt al jugar (GameSessionManager).")]
    [SerializeField] private bool saveAttemptOnExit = true;

    private bool attemptStarted = false;
    private bool attemptEnded = false;

    // Para firstTryCorrect: si hubo error desde el último acierto, no cuenta como firstTry
    private bool hadErrorSinceLastCatch = false;

    // =========================
    // START PANEL
    // =========================
    [Header("Start Panel (solo presentación)")]
    public GameObject startPanel;
    public Button startButton;
    public TextMeshProUGUI titleTMP;
    public TextMeshProUGUI instructionTMP;
    public GameObject benitoPreviewGO;
    public GameObject teodoraPreviewGO;

    [Header("Preview target")]
    public PreviewTarget previewTarget = PreviewTarget.Teodora;
    public bool randomizePreviewTarget = true;

    // =========================
    // HUD (solo gameplay)
    // =========================
    [Header("HUD Gameplay Root")]
    public GameObject hudRoot;

    [Header("HUD - Nivel")]
    public TextMeshProUGUI levelTMP;

    [Header("HUD - Remaining")]
    public TextMeshProUGUI remainingTMP;
    public GameObject benitoIconGO;
    public GameObject teodoraIconGO;

    [Header("HUD - Chronometer (Lv1-3)")]
    public GameObject chronometerContainer;
    public TextMeshProUGUI chronometerTMP;

    [Header("HUD - Time Bar (Lv4)")]
    public GameObject timeBarContainer;
    public Image timeBarFill;
    public Image timeBarFillOptionalTint; // opcional: si quieres cambiar color
    public Color timeBarGreen = new Color(0.25f, 0.9f, 0.35f, 1f);
    public Color timeBarYellow = new Color(0.95f, 0.85f, 0.25f, 1f);
    public Color timeBarRed = new Color(0.95f, 0.25f, 0.25f, 1f);

    [Header("HUD - Pause")]
    public Button pauseButton;
    public GameObject pausePanel;
    public Button resumeButton;
    public Button exitButton;
    public string hubSceneName = "03_MinigameHub";
    public string resultsScene = "05_Results";

    // =========================
    // Mirror / Actors
    // =========================
    [Header("Mirror")]
    public float mirrorCenterX = 0f;

    [Header("Actores (auto-detecta si vacío)")]
    public BushPeekActor[] actors;

    // =========================
    // LEVELS (Inspector)
    // =========================
    [Header("Level (manual si no hay singleton)")]
    public CatchLevel currentLevel = CatchLevel.Level1;

    [Header("Specs por nivel (4 entradas)")]
    public LevelSpec level1 = new LevelSpec();
    public LevelSpec level2 = new LevelSpec();
    public LevelSpec level3 = new LevelSpec();
    public LevelSpec level4 = new LevelSpec();

    // =========================
    // Estado
    // =========================
    private PreviewTarget _target;
    private int _remaining;
    private bool _started;
    private bool _paused;

    private Coroutine _loop;
    private int _turnsWithoutTarget = 0;
    private readonly List<BushPeekActor> _activeNow = new List<BushPeekActor>();

    // timers
    private float _chronoTime;
    private float _timeRemainingLv4;
    private bool _chronoRunning;
    private bool _barRunning;

    // slots anti-solape
    private readonly HashSet<string> _occupiedSlots = new HashSet<string>();
    private readonly Dictionary<BushPeekActor, List<string>> _actorReserved = new Dictionary<BushPeekActor, List<string>>();

    // cache originales
    private class Orig
    {
        public float moveSeconds;
        public Vector2 hideRange;
        public Vector2 peekRange;
        public float walkSpeed;
    }
    private readonly Dictionary<BushPeekActor, Orig> _orig = new Dictionary<BushPeekActor, Orig>();

    private void Awake()
    {
        // UI listeners
        if (startButton) startButton.onClick.AddListener(OnStartPressed);

        if (pauseButton) pauseButton.onClick.AddListener(PauseGame);
        if (resumeButton) resumeButton.onClick.AddListener(ResumeGame);
        if (exitButton) exitButton.onClick.AddListener(ExitToHub);

        // 🔥 Integración: si venimos del menú, leemos el nivel desde GameSessionManager
        LoadLevelFromGameSessionOrFallback();
    }

    private void Start()
    {
        if (actors == null || actors.Length == 0)
            actors = FindObjectsOfType<BushPeekActor>(true);

        CacheOriginals();

        // Start panel ON, HUD OFF
        if (startPanel) startPanel.SetActive(true);
        if (hudRoot) hudRoot.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);

        ChooseTarget();
        SetupStartPanel();

        StopAndHideAllActors();
        ApplyLevelText();
    }

    private void Update()
    {
        if (!_started) return;
        if (_paused) return;

        // ✅ Integración: tick del intento (igual que otros managers)
        if (saveAttemptOnExit && GameSessionManager.I != null && GameSessionManager.I.currentAttempt != null && !attemptEnded)
            GameSessionManager.I.TickTime(Time.deltaTime);

        // Cronómetro Lv1-3
        if (_chronoRunning)
        {
            _chronoTime += Time.deltaTime;
            UpdateChronometer();
        }

        // Barra Lv4
        if (_barRunning)
        {
            _timeRemainingLv4 -= Time.deltaTime;
            UpdateTimeBar();

            if (_timeRemainingLv4 <= 0f)
            {
                OnTimeExpired();
            }
        }
    }

    // =========================================================
    // CleverMinds - Nivel desde sesión
    // =========================================================
    private void LoadLevelFromGameSessionOrFallback()
    {
        // Si hay sesión, usamos selection.level (1..4). Tu enum CatchLevel coincide en valores.
        if (GameSessionManager.I != null)
        {
            int lv = (int)GameSessionManager.I.currentSelection.level;
            if (lv >= 1 && lv <= 4)
            {
                currentLevel = (CatchLevel)lv;
                return;
            }
        }

        // Fallback test
        currentLevel = (CatchLevel)(int)fallbackLevelForTesting;
    }

    private void EnsureCoreSelectionMatchesThisLevel()
    {
        if (!saveAttemptOnExit) return;
        if (GameSessionManager.I == null) return;

        // Forzamos a que el Attempt vaya al nivel correcto
        // (y así también el scoring usa el tuning correcto de ese nivel).
        GameSessionManager.I.SelectMiniGameAndLevel(MiniGameId.AtrapaBT, (LevelId)(int)currentLevel);
    }

    // =========================================================
    // Level Spec
    // =========================================================
    private LevelSpec GetSpec()
    {
        return currentLevel switch
        {
            CatchLevel.Level1 => level1,
            CatchLevel.Level2 => level2,
            CatchLevel.Level3 => level3,
            _ => level4
        };
    }

    private void ApplyLevelText()
    {
        if (levelTMP) levelTMP.text = $"NIVEL {(int)currentLevel}";
    }

    // =========================================================
    // Start panel / start game
    // =========================================================
    private void ChooseTarget()
    {
        _target = randomizePreviewTarget
            ? (Random.value < 0.5f ? PreviewTarget.Benito : PreviewTarget.Teodora)
            : previewTarget;
    }

    private void SetupStartPanel()
    {
        bool isBenito = (_target == PreviewTarget.Benito);

        if (titleTMP)
            titleTMP.text = isBenito ? "ATRAPA A BENITO" : "ATRAPA A TEODORA";

        var spec = GetSpec();
        if (instructionTMP)
            instructionTMP.text = $"Atrapa {spec.catchesToWin} veces a {(isBenito ? "Benito" : "Teodora")}";

        if (benitoPreviewGO) benitoPreviewGO.SetActive(isBenito);
        if (teodoraPreviewGO) teodoraPreviewGO.SetActive(!isBenito);
    }

    private void OnStartPressed()
    {
        // Cierra start panel
        if (startPanel) startPanel.SetActive(false);
        if (benitoPreviewGO) benitoPreviewGO.SetActive(false);
        if (teodoraPreviewGO) teodoraPreviewGO.SetActive(false);

        // Activa HUD gameplay
        if (hudRoot) hudRoot.SetActive(true);
        ApplyLevelText();

        bool isBenito = (_target == PreviewTarget.Benito);
        if (benitoIconGO) benitoIconGO.SetActive(isBenito);
        if (teodoraIconGO) teodoraIconGO.SetActive(!isBenito);

        // ✅ El tiempo empieza aquí (justo al desactivar el panel)
        ResetTimersForLevel();
        StartTimersForLevel();

        _started = true;
        _paused = false;
        _turnsWithoutTarget = 0;

        var spec = GetSpec();
        _remaining = spec.catchesToWin;
        RefreshRemaining();

        hadErrorSinceLastCatch = false;

        PrepareActorsForSession();
        StartLoop();

        // ✅ Integración: comenzar attempt (uno por nivel)
        BeginAttemptInSystem();
    }

    // =========================================================
    // Timers
    // =========================================================
    private void ResetTimersForLevel()
    {
        _chronoRunning = false;
        _barRunning = false;

        _chronoTime = 0f;

        var spec = GetSpec();
        _timeRemainingLv4 = spec.level4TimeLimitSeconds;

        UpdateChronometer();
        UpdateTimeBar();

        if (chronometerContainer) chronometerContainer.SetActive(currentLevel != CatchLevel.Level4);
        if (timeBarContainer) timeBarContainer.SetActive(currentLevel == CatchLevel.Level4);
    }

    private void StartTimersForLevel()
    {
        if (currentLevel == CatchLevel.Level4)
        {
            _barRunning = true;
            _chronoRunning = false;
        }
        else
        {
            _chronoRunning = true;
            _barRunning = false;
        }
    }

    private void UpdateChronometer()
    {
        if (!chronometerTMP) return;
        int s = Mathf.FloorToInt(_chronoTime);
        chronometerTMP.text = $"{s / 60:00}:{s % 60:00}";
    }

    private void UpdateTimeBar()
    {
        if (!timeBarFill) return;

        var spec = GetSpec();
        float norm = Mathf.Clamp01(_timeRemainingLv4 / Mathf.Max(0.01f, spec.level4TimeLimitSeconds));
        timeBarFill.fillAmount = norm;

        // color por tramos (opcional)
        var img = timeBarFillOptionalTint ? timeBarFillOptionalTint : timeBarFill;
        if (img)
        {
            if (norm > 0.5f) img.color = timeBarGreen;
            else if (norm > 0.2f) img.color = timeBarYellow;
            else img.color = timeBarRed;
        }
    }

    private void OnTimeExpired()
    {
        // 🔥 Integración: cerrar intento como NO completado
        EndAttemptInSystem(completed: false);

        // Mantengo tu comportamiento: vuelve al start panel para reintentar el MISMO nivel
        _started = false;
        StopLoop();
        StopAndHideActive();
        StopAndHideAllActors();

        ChooseTarget();
        SetupStartPanel();
        if (hudRoot) hudRoot.SetActive(false);
        if (startPanel) startPanel.SetActive(true);
    }

    // =========================================================
    // Pause
    // =========================================================
    public void PauseGame()
    {
        if (!_started) return;
        if (_paused) return;

        _paused = true;

        if (pausePanel) pausePanel.SetActive(true);

        // congela actores
        foreach (var a in actors)
            if (a) a.SetPaused(true);
    }

    public void ResumeGame()
    {
        if (!_paused) return;

        _paused = false;
        if (pausePanel) pausePanel.SetActive(false);

        foreach (var a in actors)
            if (a) a.SetPaused(false);
    }

    public void ExitToHub()
    {
        // 🔥 Integración: si sales a mitad, lo marcamos como no completado
        if (_started && !attemptEnded)
            EndAttemptInSystem(completed: false);

        ResumeGame();

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
    }

    // =========================================================
    // Gameplay
    // =========================================================
    private void RefreshRemaining()
    {
        if (remainingTMP) remainingTMP.text = $"{_remaining}";
    }

    private void CacheOriginals()
    {
        _orig.Clear();
        foreach (var a in actors)
        {
            if (!a) continue;
            _orig[a] = new Orig
            {
                moveSeconds = a.moveSeconds,
                hideRange = a.hideSecondsRange,
                peekRange = a.peekSecondsRange,
                walkSpeed = a.walkSpeed
            };
        }
    }

    private void PrepareActorsForSession()
    {
        foreach (var a in actors)
        {
            if (!a) continue;
            a.Stop();
            a.gameObject.SetActive(false);
            a.SetMirrorCenterX(mirrorCenterX);
            a.SetPaused(false);
            a.Caught -= OnActorCaught;
            a.WrongTapped -= OnActorWrongTapped;
            a.Caught += OnActorCaught;
            a.WrongTapped += OnActorWrongTapped;
        }

        _occupiedSlots.Clear();
        _actorReserved.Clear();
        _activeNow.Clear();
    }

    private void StartLoop()
    {
        StopLoop();
        _loop = StartCoroutine(GameLoop());
    }

    private void StopLoop()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
    }

    private IEnumerator GameLoop()
    {
        var targetChar = (_target == PreviewTarget.Benito) ? BushPeekActor.CharacterId.Benito : BushPeekActor.CharacterId.Teodora;
        List<BushPeekActor> targetActors = actors.Where(a => a && a.characterId == targetChar).ToList();
        List<BushPeekActor> distractorActors = actors.Where(a => a && a.characterId != targetChar).ToList();

        while (_started && _remaining > 0)
        {
            var spec = GetSpec();

            // si queda gente activa, esperamos a que termine (sin cortar extras)
            if (_activeNow.Count > 0)
                yield return WaitAllActiveOrTimeout(spec.waitExtrasToFinishTimeout);

            bool forceTarget = (spec.forceTargetAfterMissStreak > 0 && _turnsWithoutTarget >= spec.forceTargetAfterMissStreak);
            bool spawnTarget = forceTarget || (Random.value < spec.chanceMainIsTarget);

            var mainPool = spawnTarget ? targetActors : distractorActors;

            if (!TryPickSpawn(mainPool, avoid: null, out var main, out var mainSide))
            {
                yield return null;
                continue;
            }

            bool mainFinished = false;
            ActivateAndPlayOne(main, mainSide, () => mainFinished = true);

            if (spawnTarget) _turnsWithoutTarget = 0;
            else _turnsWithoutTarget++;

            // extra distractor
            if (spec.maxSimultaneousVisible >= 2 && Random.value < spec.chanceExtraDistractor)
            {
                if (TryPickSpawn(distractorActors, avoid: main, out var extra, out var extraSide))
                {
                    yield return WaitPausable(Random.Range(0.10f, 0.20f));
                    if (_started && _remaining > 0)
                        ActivateAndPlayOne(extra, extraSide, onFinished: null);
                }
            }

            while (_started && _remaining > 0 && !mainFinished)
            {
                if (_paused) { yield return null; continue; }
                yield return null;
            }

            if (_started && _remaining > 0 && _activeNow.Count > 0)
                yield return WaitAllActiveOrTimeout(spec.waitExtrasToFinishTimeout);

            if (_started && _remaining > 0)
                yield return WaitPausable(spec.pauseBetweenTurns);
        }

        StopAndHideActive();
    }

    private IEnumerator WaitAllActiveOrTimeout(float timeout)
    {
        float t = 0f;
        while (_started && _remaining > 0 && _activeNow.Count > 0 && t < timeout)
        {
            if (_paused) { yield return null; continue; }
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitPausable(float seconds)
    {
        if (seconds <= 0f) yield break;
        float t = 0f;
        while (t < seconds)
        {
            if (_paused) { yield return null; continue; }
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // =========================
    // Anti-solape
    // =========================
    private bool TryPickSpawn(List<BushPeekActor> pool, BushPeekActor avoid, out BushPeekActor chosen, out BushPeekActor.Side? bushSide)
    {
        chosen = null;
        bushSide = null;

        if (pool == null || pool.Count == 0) return false;

        var candidates = pool.Where(a => a && a != avoid && !_activeNow.Contains(a))
                             .OrderBy(_ => Random.value)
                             .ToList();

        foreach (var a in candidates)
        {
            if (a.mode == BushPeekActor.ActorMode.WalkPath)
            {
                string walkSlot = GetWalkSlotKey(a);
                if (walkSlot == null) continue;

                if (!_occupiedSlots.Contains(walkSlot))
                {
                    chosen = a;
                    return true;
                }
            }
            else
            {
                string baseKey = GetBushBaseKey(a);
                if (baseKey == null) continue;

                var sidesToTry = new List<BushPeekActor.Side> { BushPeekActor.Side.Left, BushPeekActor.Side.Right }
                    .OrderBy(_ => Random.value)
                    .ToList();

                if (!a.randomSideEachCycle)
                    sidesToTry = new List<BushPeekActor.Side> { a.fixedSide };

                foreach (var side in sidesToTry)
                {
                    string slot = $"{baseKey}:SIDE_{side}";
                    if (!_occupiedSlots.Contains(slot))
                    {
                        chosen = a;
                        bushSide = side;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private string GetWalkSlotKey(BushPeekActor a)
    {
        if (!a || a.walkPosA == null || a.walkPosB == null) return null;
        int idA = a.walkPosA.GetInstanceID();
        int idB = a.walkPosB.GetInstanceID();
        int min = Mathf.Min(idA, idB);
        int max = Mathf.Max(idA, idB);
        return $"WALK:{min}:{max}";
    }

    private string GetBushBaseKey(BushPeekActor a)
    {
        if (!a || a.hideBase == null || a.peekBase == null) return null;
        int idH = a.hideBase.GetInstanceID();
        int idP = a.peekBase.GetInstanceID();
        return $"BUSH:{idH}:{idP}:BASE_{a.baseSide}";
    }

    // =========================
    // Activación + tuning
    // =========================
    private void ActivateAndPlayOne(BushPeekActor a, BushPeekActor.Side? forcedBushSide, System.Action onFinished)
    {
        if (!a) return;

        var spec = GetSpec();

        var targetChar = (_target == PreviewTarget.Benito)
            ? BushPeekActor.CharacterId.Benito
            : BushPeekActor.CharacterId.Teodora;

        a.ConfigureTarget(targetChar, enableCatch: true);
        a.SetMirrorCenterX(mirrorCenterX);
        a.SetPaused(_paused);

        ApplyGlobalTuning(a, spec);

        ReserveSlotsForActor(a, forcedBushSide);

        a.gameObject.SetActive(true);
        _activeNow.Add(a);

        void Finish()
        {
            if (!a) return;

            a.Stop();
            a.gameObject.SetActive(false);

            RestoreOriginals(a);
            ReleaseSlotsForActor(a);

            _activeNow.Remove(a);
            onFinished?.Invoke();
        }

        if (a.mode == BushPeekActor.ActorMode.BushPeek)
            a.PlayBushOnce(Finish, forcedBushSide);
        else
            a.PlayWalkOnce(Finish, null);
    }

    private void ApplyGlobalTuning(BushPeekActor a, LevelSpec spec)
    {
        if (!a) return;
        if (!_orig.TryGetValue(a, out var o)) return;

        a.moveSeconds = o.moveSeconds;
        a.hideSecondsRange = o.hideRange;
        a.peekSecondsRange = o.peekRange;
        a.walkSpeed = o.walkSpeed;

        if (a.mode == BushPeekActor.ActorMode.BushPeek)
        {
            a.moveSeconds = Mathf.Max(0.01f, o.moveSeconds * spec.bushMoveMultiplier);
            a.hideSecondsRange = new Vector2(
                Mathf.Max(0f, o.hideRange.x * spec.bushHideMultiplier),
                Mathf.Max(0f, o.hideRange.y * spec.bushHideMultiplier)
            );
            a.peekSecondsRange = new Vector2(
                Mathf.Max(0f, o.peekRange.x * spec.bushPeekMultiplier),
                Mathf.Max(0f, o.peekRange.y * spec.bushPeekMultiplier)
            );
        }
        else
        {
            a.walkSpeed = Mathf.Max(0.01f, o.walkSpeed * spec.walkSpeedMultiplier);
        }
    }

    private void RestoreOriginals(BushPeekActor a)
    {
        if (!a) return;
        if (!_orig.TryGetValue(a, out var o)) return;

        a.moveSeconds = o.moveSeconds;
        a.hideSecondsRange = o.hideRange;
        a.peekSecondsRange = o.peekRange;
        a.walkSpeed = o.walkSpeed;
    }

    // =========================
    // Slots reserve/release
    // =========================
    private void ReserveSlotsForActor(BushPeekActor a, BushPeekActor.Side? forcedBushSide)
    {
        if (!_actorReserved.TryGetValue(a, out var list))
        {
            list = new List<string>();
            _actorReserved[a] = list;
        }

        if (a.mode == BushPeekActor.ActorMode.WalkPath)
        {
            string walkSlot = GetWalkSlotKey(a);
            if (!string.IsNullOrEmpty(walkSlot))
            {
                _occupiedSlots.Add(walkSlot);
                list.Add(walkSlot);
            }
        }
        else
        {
            string baseKey = GetBushBaseKey(a);
            if (string.IsNullOrEmpty(baseKey)) return;

            var side = forcedBushSide ?? (a.randomSideEachCycle
                ? (Random.value < 0.5f ? BushPeekActor.Side.Left : BushPeekActor.Side.Right)
                : a.fixedSide);

            string slot = $"{baseKey}:SIDE_{side}";
            _occupiedSlots.Add(slot);
            list.Add(slot);
        }
    }

    private void ReleaseSlotsForActor(BushPeekActor a)
    {
        if (!_actorReserved.TryGetValue(a, out var list)) return;
        foreach (var slot in list) _occupiedSlots.Remove(slot);
        list.Clear();
    }

    // =========================
    // Catch events
    // =========================
    private void OnActorCaught(BushPeekActor actor)
    {
        if (!_started) return;
        if (_remaining <= 0) return;

        // ✅ Métrica correct + firstTry
        if (saveAttemptOnExit && GameSessionManager.I != null && GameSessionManager.I.currentAttempt != null && !attemptEnded)
        {
            bool firstTry = !hadErrorSinceLastCatch;
            GameSessionManager.I.AddCorrect(firstTry);
        }
        hadErrorSinceLastCatch = false;

        _remaining--;
        RefreshRemaining();

        if (_remaining <= 0)
        {
            // ✅ Nivel completado → se guarda y se desbloquea el siguiente (por progreso)
            EndAttemptInSystem(completed: true);

            _started = false;
            StopLoop();
            StopAndHideActive();

            // IMPORTANTE: NO avanzar nivel automáticamente.
            // Enseñamos resultados
            // El selector verá el progreso y desbloqueará el siguiente.
            GoToResults();
        }
    }


    private void OnActorWrongTapped(BushPeekActor actor)
    {
        // ✅ Integración: métrica error
        if (saveAttemptOnExit && GameSessionManager.I != null && GameSessionManager.I.currentAttempt != null && !attemptEnded)
            GameSessionManager.I.AddError();

        hadErrorSinceLastCatch = true;

        // (feedback suave opcional: lo dejas tú como lo tenías)
    }

    // =========================
    // Stop/hide
    // =========================
    private void StopAndHideActive()
    {
        var copy = _activeNow.ToList();
        foreach (var a in copy)
        {
            if (!a) continue;
            a.Stop();
            a.gameObject.SetActive(false);
            RestoreOriginals(a);
            ReleaseSlotsForActor(a);
        }
        _activeNow.Clear();
    }

    private void StopAndHideAllActors()
    {
        foreach (var a in actors)
        {
            if (!a) continue;
            a.Stop();
            a.gameObject.SetActive(false);
            RestoreOriginals(a);
        }
        _activeNow.Clear();
        _occupiedSlots.Clear();
        _actorReserved.Clear();
    }

    // =========================================================
    // CleverMinds - Attempt lifecycle (Begin/End)
    // =========================================================
    private void BeginAttemptInSystem()
    {
        if (!saveAttemptOnExit) return;
        if (attemptEnded) return;
        if (GameSessionManager.I == null) return;

        EnsureCoreSelectionMatchesThisLevel();

        var spec = GetSpec();
        float limit = (currentLevel == CatchLevel.Level4) ? Mathf.Max(0.1f, spec.level4TimeLimitSeconds) : 0f;

        GameSessionManager.I.BeginAttempt(limit);
        attemptStarted = true;
        attemptEnded = false;
    }

    private void EndAttemptInSystem(bool completed)
    {
        if (!saveAttemptOnExit) return;
        if (!attemptStarted || attemptEnded) return;
        if (GameSessionManager.I == null || GameSessionManager.I.currentAttempt == null) return;

        // Igual que EncajaLaLlave: dejamos TickTime, pero aquí fijamos timeSeconds al valor real del juego.
        if (currentLevel == CatchLevel.Level4)
        {
            var spec = GetSpec();
            float elapsed = Mathf.Max(0f, spec.level4TimeLimitSeconds - _timeRemainingLv4);
            GameSessionManager.I.currentAttempt.timeSeconds = elapsed;
        }
        else
        {
            GameSessionManager.I.currentAttempt.timeSeconds = _chronoTime;
        }

        GameSessionManager.I.EndAttempt(completed);
        attemptEnded = true;
    }
    private const string resultsSceneName = "05_Results";

    private void GoToResults()
    {
        Time.timeScale = 1f;
        _paused = false;
        SceneManager.LoadScene(resultsSceneName);
    }
}
