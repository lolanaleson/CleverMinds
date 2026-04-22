using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Memoriza Boda (Wedding Faces) - GameManager
/// Integración CleverMinds:
/// - Lee nivel + accesibilidad desde GameSessionManager
/// - Registra intento (BeginAttempt / AddCorrect / AddError / EndAttempt)
/// - Mantiene la mecánica original (rondas encadenadas) y solo añade lo necesario
/// </summary>
public enum WeddingFacesLevel
{
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
    Level4 = 4
}

public class WeddingFacesGameManager : MonoBehaviour
{
    // =========================================================
    //  LEVEL (CleverMinds)
    // =========================================================
    [Header("Level Config")]
    [Tooltip("Solo testing si NO existe GameSessionManager en escena.")]
    [SerializeField] private LevelId fallbackLevelForTesting = LevelId.Level1;

    [SerializeField] private WeddingFacesLevel currentLevel = WeddingFacesLevel.Level1;

    private bool hasVisualDifficulty = false;   // profile.hasVisionIssues (snapshot ya lo guarda GSM)
    private bool hasAuditoryDifficulty = false; // profile.hasHearingIssues (snapshot ya lo guarda GSM)

    private int charactersCount;
    private int optionsCount;

    private bool useChronometer;
    private bool useTimeBar;

    // ✅ Objetivo por nivel (para poder cerrar el intento)
    [Header("Objetivo (nº de rondas correctas para completar el nivel)")]
    [SerializeField] private int goalCorrectRounds_Level1 = 3;
    [SerializeField] private int goalCorrectRounds_Level2 = 4;
    [SerializeField] private int goalCorrectRounds_Level3 = 6;
    [SerializeField] private int goalCorrectRounds_Level4 = 6;
    private int goalCorrectRounds = 3;

    private int correctRoundsThisAttempt = 0;

    // ✅ Nuevo: rondas totales y progreso. En este minijuego "goalCorrectRounds" se usa como nº de rondas del nivel.
    private int totalRoundsThisLevel = 0;
    private int roundsPlayedThisLevel = 0;
    private int roundsWonThisLevel = 0;

    private bool attemptEnded = false;

    // =========================================================
    //  DATA
    // =========================================================
    [Header("Character Data")]
    [SerializeField] private List<WeddingCharacterData> charactersData;
    private readonly Dictionary<WeddingCharacterId, WeddingCharacterData> dataDict = new();

    // =========================================================
    //  TEMPLATES
    // =========================================================
    [System.Serializable]
    public class CharacterTemplateEntry
    {
        public WeddingCharacterId characterId;
        public RectTransform template;
    }

    [Header("Templates")]
    [SerializeField] private List<CharacterTemplateEntry> templates;
    private readonly Dictionary<WeddingCharacterId, RectTransform> templateDict = new();

    [SerializeField] private RectTransform runtimeCharactersParent;

    // =========================================================
    //  MOVEMENT
    // =========================================================
    [Header("Movement Points")]
    [SerializeField] private RectTransform moveStartPoint;
    [SerializeField] private RectTransform moveEndPoint;

    [Header("Observation Timing (TOTAL duration)")]
    [SerializeField] private Vector2 level1Observation = new(6f, 7.5f);
    [SerializeField] private Vector2 level2Observation = new(7f, 9f);
    [SerializeField] private Vector2 level3Observation = new(8f, 10f);

    [Header("Movement Speeds")]
    [SerializeField] private float level1Speed = 220f;
    [SerializeField] private float level2Speed = 320f;
    [SerializeField] private float level3Speed = 380f;

    [Header("Spawn Intervals (overlap control)")]
    [SerializeField] private float level1SpawnInterval = 0.85f;
    [SerializeField] private float level2SpawnInterval = 0.65f;
    [SerializeField] private float level3SpawnInterval = 0.55f;

    private bool nextStartToEnd = true;

    // =========================================================
    //  HUD & PANELS
    // =========================================================
    [Header("HUD Observation")]
    [SerializeField] private GameObject hudObservation;
    [SerializeField] private TextMeshProUGUI textLevelObservation;
    [SerializeField] private TextMeshProUGUI textInstructionObservation;

    [Header("Answer Panel Root")]
    [SerializeField] private GameObject answerPanelRoot;
    [SerializeField] private TextMeshProUGUI textLevelAnswer;
    [SerializeField] private TextMeshProUGUI textInstructionAnswer;

    [Header("Options Panels")]
    [SerializeField] private GameObject optionsPanel3;
    [SerializeField] private GameObject optionsPanel4;
    [SerializeField] private List<WeddingOptionButtonController> optionButtons3;
    [SerializeField] private List<WeddingOptionButtonController> optionButtons4;

    [Header("Answer Panel Input Lock (optional)")]
    [Tooltip("Si lo asignas, al pausar bloquea clicks para evitar taps fantasma.")]
    [SerializeField] private CanvasGroup answerPanelCanvasGroup;

    [Header("Start Panel")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private Button startButton;

    private bool gameStarted = false;

    // =========================================================
    //  TIMERS
    // =========================================================
    [Header("Chronometer")]
    [SerializeField] private GameObject chronometerContainer;
    [SerializeField] private TextMeshProUGUI textChronometer;

    [Header("Time Bar (Level 4)")]
    [SerializeField] private GameObject timeBarContainer;
    [SerializeField] private Image timeBarFill;
    [SerializeField] private float timeLimitSeconds = 8f;

    private float chronometerTime;
    private float timeRemaining;
    private bool chronometerRunning;
    private bool timeBarRunning;

    // =========================================================
    //  FEEDBACK & PAUSE
    // =========================================================
    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI textFeedback;
    [SerializeField] private Image imageFeedbackFlash;

    [Header("Pause")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private string hubSceneName = "03_MinigameHub";

    private bool isPaused;
    public bool IsPaused => isPaused;

    // =========================================================
    //  ROUND STATE
    // =========================================================
    private struct ObservedFace
    {
        public WeddingCharacterId cid;
        public WeddingExpressionId eid;

        public ObservedFace(WeddingCharacterId c, WeddingExpressionId e)
        {
            cid = c;
            eid = e;
        }
    }

    private readonly List<ObservedFace> observedThisRound = new();

    private WeddingCharacterId correctCharacter;
    private WeddingExpressionId correctExpression;

    private bool waitingForAnswer;
    private bool roundFinished;

    private int attemptsRemaining;

    // ✅ Nuevo: para evitar repetición inmediata del objetivo en niveles 3–4
    private int lastTargetIndex = -1;

    // ✅ Integración: detectar “a la primera” por ronda
    private int attemptsAtRoundStart = 0;

    // ✅ Nuevo: para poder revelar la opción correcta al fallar
    private WeddingOptionButtonController lastCorrectButton;

    // =========================================================
    //  UNITY
    // =========================================================
    private void Awake()
    {
        LoadFromGameSessionManagerOrFallback();
        BuildDictionaries();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        isPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);

        SetupLevelConfig();
        SetupUI();
        SetupStartPanel();

        if (startPanel == null)
            BeginGame();
    }

    private void Update()
    {
        if (isPaused) return;

        // ✅ Integración: tiempo total de intento (GSM)
        if (GameSessionManager.I != null && GameSessionManager.I.currentAttempt != null && !attemptEnded)
            GameSessionManager.I.TickTime(Time.deltaTime);

        // Cronómetro UI
        if (useChronometer && chronometerRunning)
        {
            chronometerTime += Time.deltaTime;
            UpdateChronometer();
        }

        // Barra tiempo UI
        if (useTimeBar && timeBarRunning)
        {
            timeRemaining -= Time.deltaTime;
            UpdateTimeBar();

            if (timeRemaining <= 0f)
                OnTimeExpired();
        }
    }

    // =========================================================
    //  SINGLETON / PERFIL / NIVEL
    // =========================================================
    private void LoadFromGameSessionManagerOrFallback()
    {
        if (GameSessionManager.I != null && GameSessionManager.I.profile != null)
        {
            hasVisualDifficulty = GameSessionManager.I.profile.hasVisionIssues;
            hasAuditoryDifficulty = GameSessionManager.I.profile.hasHearingIssues;

            currentLevel = (WeddingFacesLevel)GameSessionManager.I.currentSelection.level;
        }
        else
        {
            currentLevel = (WeddingFacesLevel)fallbackLevelForTesting;
        }

        _ = hasVisualDifficulty;
        _ = hasAuditoryDifficulty;
    }

    // =========================================================
    //  SETUP
    // =========================================================
    private void BuildDictionaries()
    {
        dataDict.Clear();
        templateDict.Clear();

        foreach (var d in charactersData)
        {
            if (d == null) continue;
            dataDict[d.characterId] = d;
            d.BuildRuntimeDictIfNeeded();
        }

        foreach (var t in templates)
        {
            if (t == null || t.template == null) continue;
            templateDict[t.characterId] = t.template;
        }
    }

    private void SetupLevelConfig()
    {
        switch (currentLevel)
        {
            case WeddingFacesLevel.Level1: goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level1); break;
            case WeddingFacesLevel.Level2: goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level2); break;
            case WeddingFacesLevel.Level3: goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level3); break;
            case WeddingFacesLevel.Level4: goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level4); break;
        }

        // En WeddingFaces, el objetivo equivale al nº de rondas a jugar en el nivel.
        totalRoundsThisLevel = goalCorrectRounds;
        roundsPlayedThisLevel = 0;
        roundsWonThisLevel = 0;

        switch (currentLevel)
        {
            case WeddingFacesLevel.Level1:
                charactersCount = 2;
                optionsCount = 3;
                useChronometer = true;
                useTimeBar = false;
                break;

            case WeddingFacesLevel.Level2:
                charactersCount = 3;
                optionsCount = 3;
                useChronometer = true;
                useTimeBar = false;
                break;

            case WeddingFacesLevel.Level3:
                charactersCount = 4;
                optionsCount = 4;
                useChronometer = true;
                useTimeBar = false;
                break;

            case WeddingFacesLevel.Level4:
                charactersCount = 4;
                optionsCount = 4;
                useChronometer = false;
                useTimeBar = true;
                break;
        }

        // ✅ (Opcional) Nivel 4 lee el límite desde Tuning.targetTimeSeconds
        if (currentLevel == WeddingFacesLevel.Level4 && GameSessionManager.I != null)
        {
            var tuning = GameSessionManager.I.GetTuning();
            if (tuning != null && tuning.targetTimeSeconds > 0.1f)
                timeLimitSeconds = tuning.targetTimeSeconds;
        }

        correctRoundsThisAttempt = 0;
        attemptEnded = false;
    }

    private void SetupUI()
    {
        if (hudObservation != null) hudObservation.SetActive(true);
        if (answerPanelRoot != null) answerPanelRoot.SetActive(false);

        string lvl = $"NIVEL {(int)currentLevel}";
        if (textLevelObservation != null) textLevelObservation.text = lvl;
        if (textLevelAnswer != null) textLevelAnswer.text = lvl;

        if (textInstructionObservation != null) textInstructionObservation.text = "Observa atentamente a los personajes";
        if (textInstructionAnswer != null) textInstructionAnswer.text = "¿A quién has visto en la boda?";

        if (chronometerContainer != null) chronometerContainer.SetActive(false);
        if (timeBarContainer != null) timeBarContainer.SetActive(false);

        if (pausePanel != null) pausePanel.SetActive(false);

        foreach (var b in optionButtons3) if (b != null) b.Bind(this);
        foreach (var b in optionButtons4) if (b != null) b.Bind(this);

        if (answerPanelCanvasGroup != null)
        {
            answerPanelCanvasGroup.interactable = true;
            answerPanelCanvasGroup.blocksRaycasts = true;
        }
    }

    private void SetupStartPanel()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartButtonPressed);
            startButton.onClick.AddListener(OnStartButtonPressed);
        }

        if (startPanel != null)
            startPanel.SetActive(true);
    }

    public void OnStartButtonPressed()
    {
        if (gameStarted) return;

        if (startPanel != null)
            startPanel.SetActive(false);

        BeginGame();
    }

    private void BeginGame()
    {
        if (gameStarted) return;

        gameStarted = true;
        chronometerTime = 0f;
        UpdateChronometer();

        BeginAttemptInSystem();

        if (useChronometer)
            chronometerRunning = true;

        StartNewRound();
    }

    // =========================================================
    //  ROUND FLOW
    // =========================================================
    private void StartNewRound()
    {
        waitingForAnswer = false;
        roundFinished = false;

        lastCorrectButton = null;

        attemptsRemaining = (optionsCount == 3) ? 2 : 3;
        attemptsAtRoundStart = attemptsRemaining;

        if (hudObservation != null) hudObservation.SetActive(true);
        if (answerPanelRoot != null) answerPanelRoot.SetActive(false);

        ClearRuntimeCharacters();
        observedThisRound.Clear();

        if (textFeedback != null) textFeedback.text = "";

        ResetTimers();

        StartCoroutine(RoundCoroutine());
    }

    private IEnumerator RoundCoroutine()
    {
        var characters = PickCharactersForCurrentLevel();

        foreach (var c in characters)
            observedThisRound.Add(new ObservedFace(c, PickRandomExpressionFor(c)));

        yield return StartCoroutine(ObservationCoroutine());

        if (hudObservation != null) hudObservation.SetActive(false);
        if (answerPanelRoot != null) answerPanelRoot.SetActive(true);

        ShowTimers();
        StartTimers();

        SetupQuestionAndOptions();
        waitingForAnswer = true;

        nextStartToEnd = !nextStartToEnd;
    }

    // =========================================================
    //  OBSERVATION
    // =========================================================
    private IEnumerator ObservationCoroutine()
    {
        RectTransform from = nextStartToEnd ? moveStartPoint : moveEndPoint;
        RectTransform to = nextStartToEnd ? moveEndPoint : moveStartPoint;

        float speed = currentLevel switch
        {
            WeddingFacesLevel.Level1 => level1Speed,
            WeddingFacesLevel.Level2 => level2Speed,
            _ => level3Speed
        };

        float interval = currentLevel switch
        {
            WeddingFacesLevel.Level1 => level1SpawnInterval,
            WeddingFacesLevel.Level2 => level2SpawnInterval,
            _ => level3SpawnInterval
        };

        Vector2 range = currentLevel switch
        {
            WeddingFacesLevel.Level1 => level1Observation,
            WeddingFacesLevel.Level2 => level2Observation,
            _ => level3Observation
        };

        const float minRespiroFinal = 0.25f;

        float distance = 0f;
        if (from != null && to != null)
            distance = Mathf.Abs(to.anchoredPosition.x - from.anchoredPosition.x);

        float travelTime = (speed <= 0f) ? 0f : (distance / speed);
        float spawnScheduleEnd = Mathf.Max(0f, (observedThisRound.Count - 1) * interval);
        float minTotalNeeded = spawnScheduleEnd + travelTime + minRespiroFinal;

        float totalDuration = Random.Range(range.x, range.y);
        totalDuration = Mathf.Max(totalDuration, minTotalNeeded);

        float startTime = Time.time;

        foreach (var f in observedThisRound)
        {
            SpawnCharacter(f, from, to, speed);
            yield return new WaitForSeconds(interval);
        }

        float elapsed = Time.time - startTime;
        float remaining = Mathf.Max(0f, totalDuration - elapsed);
        yield return new WaitForSeconds(remaining);
    }

    private void SpawnCharacter(ObservedFace face, RectTransform from, RectTransform to, float speed)
    {
        if (from == null || to == null) return;

        if (!templateDict.TryGetValue(face.cid, out var template) || template == null)
        {
            Debug.LogError($"[WeddingFaces] No template for {face.cid}");
            return;
        }

        if (!dataDict.TryGetValue(face.cid, out var data) || data == null)
        {
            Debug.LogError($"[WeddingFaces] No data for {face.cid}");
            return;
        }

        RectTransform instance = Instantiate(template, template.parent);
        instance.gameObject.SetActive(true);

        if (runtimeCharactersParent != null)
            instance.SetParent(runtimeCharactersParent, true);

        var img = instance.GetComponentInChildren<Image>(true);
        if (img != null)
            img.sprite = data.GetSprite(face.eid, WeddingSpriteType.Observe);

        Vector2 pos = instance.anchoredPosition;
        pos.x = from.anchoredPosition.x;
        instance.anchoredPosition = pos;

        StartCoroutine(MoveX(instance, from.anchoredPosition.x, to.anchoredPosition.x, speed));
    }

    private IEnumerator MoveX(RectTransform rt, float startX, float endX, float speed)
    {
        float x = startX;
        float dir = Mathf.Sign(endX - startX);

        while (rt && ((dir > 0 && x < endX) || (dir < 0 && x > endX)))
        {
            x += dir * speed * Time.deltaTime;
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
            yield return null;
        }

        if (rt) Destroy(rt.gameObject);
    }

    // =========================================================
    //  QUESTIONS & OPTIONS
    // =========================================================
    private void SetupQuestionAndOptions()
    {
        HideOptionsPanels();

        int targetIndex;

        if (currentLevel == WeddingFacesLevel.Level1 || currentLevel == WeddingFacesLevel.Level2)
        {
            targetIndex = observedThisRound.Count - 1;
        }
        else
        {
            targetIndex = Random.Range(0, observedThisRound.Count);

            if (observedThisRound.Count > 1 && targetIndex == lastTargetIndex)
                targetIndex = (targetIndex + 1) % observedThisRound.Count;
        }

        lastTargetIndex = targetIndex;

        var target = observedThisRound[targetIndex];
        correctCharacter = target.cid;
        correctExpression = target.eid;

        var options = optionsCount == 3
            ? BuildDifferentCharactersOptions(correctCharacter, correctExpression, 3)
            : BuildSameCharacterDifferentExpressions(correctCharacter, correctExpression, 4);

        ApplyOptions(options);
    }

    private void ApplyOptions(List<(WeddingCharacterId, WeddingExpressionId)> options)
    {
        Shuffle(options);

        var buttons = optionsCount == 3 ? optionButtons3 : optionButtons4;
        var panel = optionsCount == 3 ? optionsPanel3 : optionsPanel4;

        if (panel != null) panel.SetActive(true);

        for (int i = 0; i < buttons.Count; i++)
        {
            var b = buttons[i];
            if (b == null) continue;

            b.ResetVisual();

            if (i >= options.Count)
            {
                b.gameObject.SetActive(false);
                continue;
            }

            b.gameObject.SetActive(true);

            var optCid = options[i].Item1;
            var optEid = options[i].Item2;

            bool correct = optCid == correctCharacter && optEid == correctExpression;

            Sprite sprite = null;
            if (dataDict.TryGetValue(optCid, out var data) && data != null)
                sprite = data.GetSprite(optEid, WeddingSpriteType.Button);

            b.Setup(optCid, optEid, sprite, correct);

            // Guardamos referencia para poder parpadear la correcta si se falla la ronda.
            if (correct)
                lastCorrectButton = b;
        }
    }

    public void OnOptionSelected(WeddingOptionButtonController btn)
    {
        if (isPaused) return;
        if (!waitingForAnswer || roundFinished) return;
        if (btn == null) return;

        if (btn.IsCorrect())
        {
            bool firstTry = (attemptsRemaining == attemptsAtRoundStart);

            // ✅ Integración: acierto
            if (GameSessionManager.I != null && !attemptEnded)
                GameSessionManager.I.AddCorrect(firstTry);

            FinishRound(true);
            return;
        }

        // ✅ Integración: error
        if (GameSessionManager.I != null && !attemptEnded)
            GameSessionManager.I.AddError();

        attemptsRemaining--;
        btn.MarkWrong();

        var uibtn = btn.GetComponent<Button>();
        if (uibtn != null) uibtn.interactable = false;

        if (attemptsRemaining > 0)
        {
            ShowFeedback("Casi… inténtalo otra vez");
            Flash(Color.red);
            return;
        }

        FinishRound(false);
    }

    private void FinishRound(bool success)
    {
        roundFinished = true;
        waitingForAnswer = false;

        timeBarRunning = false;

        // Contabilizamos progreso de nivel por rondas (no por "aciertos acumulados").
        roundsPlayedThisLevel++;

        if (success)
        {
            roundsWonThisLevel++;
            correctRoundsThisAttempt++; // (se mantiene por compatibilidad/telemetría interna)

            ShowFeedback("¡Perfecto!");
            Flash(Color.green);
            StartCoroutine(AfterRoundProceed(revealCorrect: false));
            return;
        }

        // ❗ Al FALLAR todos los intentos de la ronda:
        // - NO reiniciamos partida/ronda.
        // - Mostramos cuál era la correcta con un parpadeo verde.
        // - Si era la última ronda del nivel, volvemos al hub y marcamos completed=false.
        ShowFeedback("Esta era la correcta");
        StartCoroutine(AfterRoundProceed(revealCorrect: true));
    }

    private IEnumerator AfterRoundProceed(bool revealCorrect)
    {
        // Revela la correcta (parpadeo verde) si procede
        if (revealCorrect)
        {
            yield return StartCoroutine(BlinkCorrectOptionGreen());
        }
        else
        {
            // Mantén el feedback un instante para coherencia con el resto de minijuegos
            yield return new WaitForSeconds(0.8f);
        }

        bool isLastRound = roundsPlayedThisLevel >= totalRoundsThisLevel;

        if (isLastRound)
        {
            bool completed = (roundsWonThisLevel >= totalRoundsThisLevel);
            StartCoroutine(CompleteLevelAndExit(completed));
            yield break;
        }

        StartCoroutine(NextRound());
    }

    private IEnumerator CompleteLevelAndExit(bool completed)
    {
        yield return new WaitForSeconds(1.0f);

        // ✅ Solo se considera "completado" si se han ganado TODAS las rondas.
        EndAttemptInSystem(completed: completed);

        Time.timeScale = 1f;
        isPaused = false;

        if (!string.IsNullOrEmpty(resultsSceneName))
            GoToResults();
        else
            Debug.LogError("[WeddingFaces] 'resultsScene' no está configurado.");
    }

    private IEnumerator BlinkCorrectOptionGreen()
    {
        // Si no tenemos referencia por lo que sea, intentamos encontrarla en los botones activos.
        var correctBtn = lastCorrectButton != null ? lastCorrectButton : FindCorrectButtonOnScreen();
        if (correctBtn == null)
        {
            // Fallback: flash verde general (sin tocar sprites del botón)
            Flash(Color.green);
            yield return new WaitForSeconds(0.9f);
            yield break;
        }

        var img = correctBtn.GetComponent<Image>();
        if (img == null)
            img = correctBtn.GetComponentInChildren<Image>(true);

        if (img == null)
        {
            Flash(Color.green);
            yield return new WaitForSeconds(0.9f);
            yield break;
        }

        Color original = img.color;
        Color green = new Color(0.2f, 1f, 0.2f, original.a);

        const int blinks = 3;
        const float blinkOn = 0.6f;
        const float blinkOff = 0.4f;

        for (int i = 0; i < blinks; i++)
        {
            img.color = green;
            yield return new WaitForSeconds(blinkOn);
            img.color = original;
            yield return new WaitForSeconds(blinkOff);
        }
    }

    private WeddingOptionButtonController FindCorrectButtonOnScreen()
    {
        var buttons = optionsCount == 3 ? optionButtons3 : optionButtons4;
        if (buttons == null) return null;

        foreach (var b in buttons)
        {
            if (b == null || !b.gameObject.activeInHierarchy) continue;
            if (b.IsCorrect()) return b;
        }

        return null;
    }

    private IEnumerator NextRound()
    {
        yield return new WaitForSeconds(1f);
        StartNewRound();
    }

    // =========================================================
    //  TIMERS
    // =========================================================
    private void StartTimers()
    {
        if (useChronometer)
        {
            chronometerRunning = true; // tiempo total (no resetea entre rondas)
        }

        if (useTimeBar)
        {
            timeRemaining = timeLimitSeconds;
            timeBarRunning = true;
        }
    }

    private void ResetTimers()
    {
        // Barra por ronda (mecánica original)
        timeBarRunning = false;
        timeRemaining = timeLimitSeconds;

        // Cronómetro total (para métrica coherente)
        UpdateChronometer();
        UpdateTimeBar();
    }

    private void ShowTimers()
    {
        if (chronometerContainer != null) chronometerContainer.SetActive(useChronometer);
        if (timeBarContainer != null) timeBarContainer.SetActive(useTimeBar);
    }

    private void UpdateChronometer()
    {
        if (textChronometer == null) return;

        int s = Mathf.FloorToInt(chronometerTime);
        textChronometer.text = $"{s / 60:00}:{s % 60:00}";
    }

    private void UpdateTimeBar()
    {
        if (timeBarFill == null) return;
        if (timeLimitSeconds <= 0f) { timeBarFill.fillAmount = 0f; return; }
        timeBarFill.fillAmount = Mathf.Clamp01(timeRemaining / timeLimitSeconds);
    }

    private void OnTimeExpired()
    {
        if (roundFinished) return;

        timeRemaining = 0f;
        timeBarRunning = false;

        // ✅ Integración: timeout = error
        if (GameSessionManager.I != null && !attemptEnded)
            GameSessionManager.I.AddError();

        FinishRound(false);
    }

    // =========================================================
    //  HELPERS
    // =========================================================
    private void ClearRuntimeCharacters()
    {
        if (runtimeCharactersParent == null) return;

        for (int i = runtimeCharactersParent.childCount - 1; i >= 0; i--)
            Destroy(runtimeCharactersParent.GetChild(i).gameObject);
    }

    private WeddingExpressionId PickRandomExpressionFor(WeddingCharacterId cid)
    {
        var pool = new List<WeddingExpressionId>
        {
            WeddingExpressionId.Neutral,
            WeddingExpressionId.Triste,
            WeddingExpressionId.Enfadado,
            WeddingExpressionId.Sorprendido,
            WeddingExpressionId.Asustado
        };

        Shuffle(pool);

        if (!dataDict.TryGetValue(cid, out var data) || data == null)
            return WeddingExpressionId.Neutral;

        foreach (var e in pool)
            if (data.HasExpression(e)) return e;

        return WeddingExpressionId.Neutral;
    }

    private List<WeddingCharacterId> PickCharactersForCurrentLevel()
    {
        var all = new List<WeddingCharacterId>(dataDict.Keys);
        Shuffle(all);

        if (currentLevel < WeddingFacesLevel.Level3)
            return all.GetRange(0, Mathf.Min(charactersCount, all.Count));

        // Nivel 3–4: niño siempre tercero (mecánica original)
        var adults = all.FindAll(c => c != WeddingCharacterId.Sobrina && c != WeddingCharacterId.Sobrino);
        Shuffle(adults);

        var child = Random.value > 0.5f ? WeddingCharacterId.Sobrino : WeddingCharacterId.Sobrina;

        if (adults.Count < 3)
        {
            Debug.LogWarning("[WeddingFaces] Not enough adult characters in data. Falling back.");
            return all.GetRange(0, Mathf.Min(charactersCount, all.Count));
        }

        return new List<WeddingCharacterId>
        {
            adults[0],
            adults[1],
            child,
            adults[2]
        };
    }

    private List<(WeddingCharacterId, WeddingExpressionId)> BuildDifferentCharactersOptions(
        WeddingCharacterId cid, WeddingExpressionId eid, int count)
    {
        var list = new List<(WeddingCharacterId, WeddingExpressionId)>
        {
            (cid, eid)
        };

        var pool = new List<WeddingCharacterId>(dataDict.Keys);
        pool.Remove(cid);
        Shuffle(pool);

        for (int i = 0; i < count - 1 && i < pool.Count; i++)
            list.Add((pool[i], eid));

        return list;
    }

    private List<(WeddingCharacterId, WeddingExpressionId)> BuildSameCharacterDifferentExpressions(
        WeddingCharacterId cid, WeddingExpressionId eid, int count)
    {
        var list = new List<(WeddingCharacterId, WeddingExpressionId)>
        {
            (cid, eid)
        };

        var pool = new List<WeddingExpressionId>
        {
            WeddingExpressionId.Neutral,
            WeddingExpressionId.Triste,
            WeddingExpressionId.Enfadado,
            WeddingExpressionId.Sorprendido,
            WeddingExpressionId.Asustado
        };

        pool.Remove(eid);
        Shuffle(pool);

        for (int i = 0; i < count - 1 && i < pool.Count; i++)
            list.Add((cid, pool[i]));

        return list;
    }

    private void HideOptionsPanels()
    {
        if (optionsPanel3 != null) optionsPanel3.SetActive(false);
        if (optionsPanel4 != null) optionsPanel4.SetActive(false);
    }

    private void ShowFeedback(string msg)
    {
        if (textFeedback != null)
            textFeedback.text = msg;
    }

    private void Flash(Color c)
    {
        if (imageFeedbackFlash == null) return;

        imageFeedbackFlash.color = new Color(c.r, c.g, c.b, 0.4f);
        StartCoroutine(FadeFlash());
    }

    private IEnumerator FadeFlash()
    {
        yield return new WaitForSeconds(0.25f);
        if (imageFeedbackFlash != null)
            imageFeedbackFlash.color = new Color(0, 0, 0, 0);
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    // =========================================================
    //  PAUSA (real: congela TODO)
    // =========================================================
    public void PauseGame()
    {
        if (isPaused) return;

        isPaused = true;
        Time.timeScale = 0f;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (answerPanelCanvasGroup != null)
        {
            answerPanelCanvasGroup.interactable = false;
            answerPanelCanvasGroup.blocksRaycasts = false;
        }
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (answerPanelCanvasGroup != null)
        {
            answerPanelCanvasGroup.interactable = true;
            answerPanelCanvasGroup.blocksRaycasts = true;
        }
    }

    public void ExitToHub()
    {
        EndAttemptInSystem(completed: false);

        Time.timeScale = 1f;
        isPaused = false;

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
        else
            Debug.LogError("[WeddingFaces] 'hubSceneName' no está configurado.");
    }

    // =========================================================
    //  INTEGRACIÓN CON GAMESESSIONMANAGER (intentos + guardado)
    // =========================================================
    private void BeginAttemptInSystem()
    {
        if (GameSessionManager.I == null) return;
        if (attemptEnded) return;

        float limit = (currentLevel == WeddingFacesLevel.Level4) ? Mathf.Max(0.1f, timeLimitSeconds) : 0f;
        GameSessionManager.I.BeginAttempt(limit);
    }

    private void EndAttemptInSystem(bool completed)
    {
        if (attemptEnded) return;
        if (GameSessionManager.I == null) return;
        if (GameSessionManager.I.currentAttempt == null) return;

        // Asegurar tiempo final consistente
        if (useChronometer)
            GameSessionManager.I.currentAttempt.timeSeconds = chronometerTime;

        GameSessionManager.I.EndAttempt(completed);
        attemptEnded = true;
    }

    private const string resultsSceneName = "05_Results";

    private void GoToResults()
    {
        Time.timeScale = 1f;
        isPaused = false;
        SceneManager.LoadScene(resultsSceneName);
    }
}


