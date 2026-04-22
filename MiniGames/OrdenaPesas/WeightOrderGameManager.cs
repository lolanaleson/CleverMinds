using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class WeightOrderGameManager : MonoBehaviour
{
    public enum WeightOrderLevel { Level1 = 1, Level2 = 2, Level3 = 3, Level4 = 4 }

    // =========================================================
    //  CONFIGURACIÓN DE NIVEL (Integración CleverMinds)
    // =========================================================
    [Header("Level Config")]
    // - En producción, el nivel se lee desde GameSessionManager.I.currentSelection.level
    // - En modo test, si no hay GameSessionManager, puedes forzar el nivel desde el inspector.
    [SerializeField] private LevelId fallbackLevelForTesting = LevelId.Level1;
    private WeightOrderLevel currentLevel = WeightOrderLevel.Level1;

    [Tooltip("Multiplicador del tiempo en Level 4 (1=normal, 0.8 más rápido, 1.2 más lento)")]
    [SerializeField] private float timeFactor = 1f;

    // =========================================================
    //  RONDAS (como EncajaLaLlave / EncuentraElCoche)
    // =========================================================
    [Header("Rounds")]
    [SerializeField] private int goalCorrectRounds_Level1 = 5;
    [SerializeField] private int goalCorrectRounds_Level2 = 6;
    [SerializeField] private int goalCorrectRounds_Level3 = 7;
    [SerializeField] private int goalCorrectRounds_Level4 = 7;

    private int goalCorrectRounds = 5;
    private int correctRoundsThisAttempt = 0;

    // Errores por ronda (para firstTryCorrect)
    private int roundErrors = 0;

    // -------- Level tuning --------
    private int minW, maxW;
    private bool useChronometer, useTimeBar;
    private bool giveStrongHints, giveMediumHints;

    // -------- Ronda --------
    private bool ascendingRule = true; // true: menor->mayor, false: mayor->menor
    private readonly List<int> roundCorrectWeights = new(); // 3 pesos correctos (en orden placeholder[0..2])

    // =========================================================
    //  UI
    // =========================================================
    [Header("UI - Título / Nivel / Instrucción")]
    [SerializeField] private TextMeshProUGUI titleTMP;
    [SerializeField] private TextMeshProUGUI levelTMP;
    [SerializeField] private TextMeshProUGUI instructionTMP;

    [Header("UI - Rondas restantes (solo número)")]
    [SerializeField] private TextMeshProUGUI roundsRemainingText;

    [Header("UI - Caja Pista/Feedback (misma caja)")]
    [SerializeField] private GameObject hintContainer;
    [SerializeField] private TextMeshProUGUI hintTMP;
    [SerializeField] private float feedbackSeconds = 1.25f;
    [SerializeField] private float winFeedbackSeconds = 1.6f;
    private Coroutine hintFeedbackRoutine;

    [Header("UI - Flash verde (Image overlay)")]
    [SerializeField] private Image imageFeedbackFlash;
    [SerializeField, Range(0f, 1f)] private float flashMaxAlpha = 0.35f;
    [SerializeField] private float flashInSeconds = 0.12f;
    [SerializeField] private float flashOutSeconds = 0.18f;

    [Header("UI - Cronómetro (L1-L3)")]
    [SerializeField] private GameObject chronometerContainer;
    [SerializeField] private TextMeshProUGUI chronometerTMP;
    private float chronometerTime = 0f;
    private bool chronometerRunning = false;

    [Header("UI - Barra de tiempo (L4)")]
    [SerializeField] private GameObject timeBarContainer;
    [SerializeField] private Image timeBarFill;
    [SerializeField] private float level4TimeLimit = 15f;

    private float timeRemaining = 0f;
    private bool timeBarRunning = false;

    [Header("Pausa")]
    [SerializeField] private GameObject pausePanel;
    public bool IsPaused { get; private set; } = false;

    [Header("Contenedores")]
    [SerializeField] private RectTransform weightsSpawnContainer; // donde aparecen las pesas arrastrables

    [Header("Prefabs")]
    [SerializeField] private GameObject weightPrefab; // PF_Weight

    [Header("Drag Layer (para que el Layout no solape)")]
    [SerializeField] private RectTransform dragLayer;
    public RectTransform DragLayer => dragLayer;

    [Header("Placeholders (3 fijos, asignados en Inspector)")]
    [SerializeField] private WeightPlaceholderDrop[] placeholders = new WeightPlaceholderDrop[3];

    [Header("Hub / Salida")]
    [SerializeField] private string hubSceneName = "03_MinigameHub";

    private bool roundWinRunning = false;

    // =========================================================
    //  INTEGRACIÓN CON GAMESESSIONMANAGER (intentos + guardado)
    // =========================================================
    [Header("CleverMinds - Guardado")]
    [SerializeField] private bool saveAttemptOnExit = true;
    private bool attemptStarted = false;
    private bool attemptEnded = false;

    private void Awake()
    {
        LoadFromGameSessionManagerOrFallback();
        SetupLevelConfig();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        IsPaused = false;
        if (pausePanel) pausePanel.SetActive(false);

        // Conectar placeholders con el GM
        for (int i = 0; i < placeholders.Length; i++)
            if (placeholders[i] != null)
                placeholders[i].SetGameManager(this);

        SetupUI();

        // Flash invisible al empezar
        SetFlashAlpha(0f);

        BeginAttemptIfPossible();

        StartRound();

        if (useChronometer) StartChronometer();
        if (useTimeBar) StartTimeBar();
    }

    private void Update()
    {
        if (IsPaused) return;

        // Tick del attempt en el Core (si existe)
        if (GameSessionManager.I != null && GameSessionManager.I.currentAttempt != null && attemptStarted && !attemptEnded)
            GameSessionManager.I.TickTime(Time.deltaTime);

        if (useChronometer && chronometerRunning)
        {
            chronometerTime += Time.deltaTime;
            UpdateChronometerUI();
        }

        if (useTimeBar && timeBarRunning)
        {
            timeRemaining -= Time.deltaTime;
            timeRemaining = Mathf.Max(0f, timeRemaining);
            UpdateTimeBarUI();

            if (timeRemaining <= 0f)
            {
                timeBarRunning = false;

                // ✅ Igual que Encaja/Coche: se acabó el tiempo => intento fallido + salida
                ShowFeedback("Se acabó el tiempo", winFeedbackSeconds);
                DisableAllDrags();
                StartCoroutine(TimeOutCoroutine());
            }
        }
    }

    private IEnumerator TimeOutCoroutine()
    {
        yield return new WaitForSecondsRealtime(1.0f);
        EndAttemptIfStarted(completed: false);
        GoToResults();
    }

    // =========================================================
    // NIVEL / RONDAS
    // =========================================================
    private void SetupLevelConfig()
    {
        giveStrongHints = false;
        giveMediumHints = false;

        switch (currentLevel)
        {
            case WeightOrderLevel.Level1:
                minW = 1; maxW = 16;
                useChronometer = true;
                useTimeBar = false;
                giveStrongHints = true;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level1);
                break;

            case WeightOrderLevel.Level2:
                minW = 5; maxW = 47;
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level2);
                break;

            case WeightOrderLevel.Level3:
                minW = 1; maxW = 16;
                useChronometer = true;
                useTimeBar = false;
                giveMediumHints = true;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level3);
                break;

            case WeightOrderLevel.Level4:
                // ✅ Igual que Nivel 3 (hueco fantasma), pero con tiempo
                minW = 1; maxW = 16;
                useChronometer = false;
                useTimeBar = true;
                giveMediumHints = true;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level4);

                // Si hay tuning en el Core, usar su targetTimeSeconds
                if (GameSessionManager.I != null)
                {
                    var tuning = GameSessionManager.I.GetTuning();
                    if (tuning != null && tuning.targetTimeSeconds > 0.1f)
                        level4TimeLimit = tuning.targetTimeSeconds;
                }
                break;
        }

        correctRoundsThisAttempt = 0;
        roundErrors = 0;
        attemptEnded = false;
    }

    private void SetupUI()
    {
        if (titleTMP) titleTMP.text = "Ordena las pesas";
        if (levelTMP) levelTMP.text = $"NIVEL {(int)currentLevel}";

        if (chronometerContainer) chronometerContainer.SetActive(useChronometer);
        if (timeBarContainer) timeBarContainer.SetActive(useTimeBar);

        if (hintContainer) hintContainer.SetActive(true);
        if (hintTMP) hintTMP.text = "";

        if (useChronometer && chronometerTMP) chronometerTMP.text = "00:00";

        if (useTimeBar && timeBarFill)
        {
            timeBarFill.fillAmount = 1f;
            timeBarFill.color = Color.white;
        }

        UpdateRoundsRemainingUI();
    }

    private void UpdateRoundsRemainingUI()
    {
        if (roundsRemainingText == null) return;
        int remaining = Mathf.Max(0, goalCorrectRounds - correctRoundsThisAttempt);
        roundsRemainingText.text = remaining.ToString();
    }

    // =========================================================
    // RONDA
    // =========================================================
    private void StartRound()
    {
        roundWinRunning = false;
        roundErrors = 0;

        ClearRound();

        // Regla aleatoria SIEMPRE
        ascendingRule = (Random.value < 0.5f);
        if (instructionTMP)
            instructionTMP.text = ascendingRule ? "ORDENA LAS PESAS: MENOR A MAYOR" : "ORDENA LAS PESAS: MAYOR A MENOR";

        // ✅ NIVEL 3 y 4 = Hueco fantasma (L4 es igual que L3 pero con tiempo)
        if (currentLevel == WeightOrderLevel.Level3 || currentLevel == WeightOrderLevel.Level4)
        {
            SetupLevel3GhostSlotScenario();
            if (useTimeBar) StartTimeBar();
            return;
        }

        // Niveles 1,2: ordenar 3 pesas
        List<int> baseWeights = GenerateWeightsForLevel();

        List<int> ordered = new List<int>(baseWeights);
        ordered.Sort();
        if (!ascendingRule) ordered.Reverse();

        roundCorrectWeights.Clear();
        roundCorrectWeights.AddRange(ordered);

        for (int i = 0; i < 3; i++)
        {
            if (placeholders[i] == null) continue;
            placeholders[i].SetCorrectWeight(roundCorrectWeights[i]);
            placeholders[i].ForceSetOccupied(false);
        }

        SpawnDraggableWeights(baseWeights);
    }

    private void ClearRound()
    {
        if (weightsSpawnContainer != null)
        {
            for (int i = weightsSpawnContainer.childCount - 1; i >= 0; i--)
                Destroy(weightsSpawnContainer.GetChild(i).gameObject);
        }

        if (hintFeedbackRoutine != null)
        {
            StopCoroutine(hintFeedbackRoutine);
            hintFeedbackRoutine = null;
        }
        if (hintTMP) hintTMP.text = "";
    }

    private List<int> GenerateWeightsForLevel()
    {
        if (currentLevel == WeightOrderLevel.Level1)
        {
            // Gimnasio real: 3 consecutivos
            int start = Random.Range(minW, maxW - 1);
            return new List<int> { start, start + 1, start + 2 };
        }

        return GenerateFineComparisonCombo();
    }

    private List<int> GenerateFineComparisonCombo()
    {
        int[][] patterns = new int[][]
        {
            new[] { 0, 1, 3 },
            new[] { 0, 2, 3 },
            new[] { 0, 1, 2 },
            new[] { 0, 5, 6 },
            new[] { 0, 7, 9 },
            new[] { 0, 10, 11 },
            new[] { 0, 2, 10 },
        };

        int[] p = patterns[Random.Range(0, patterns.Length)];
        int maxDelta = p.Max();

        int startMin = minW;
        int startMax = maxW - maxDelta;
        int start = Random.Range(startMin, startMax + 1);

        return new List<int> { start + p[0], start + p[1], start + p[2] };
    }

    private void SpawnDraggableWeights(List<int> toSpawn)
    {
        if (weightsSpawnContainer == null || weightPrefab == null) return;

        List<int> list = new List<int>(toSpawn);
        Shuffle(list);

        foreach (int w in list)
        {
            GameObject go = Instantiate(weightPrefab, weightsSpawnContainer);
            var drag = go.GetComponent<WeightDraggable>();
            drag.SetWeight(w);
            drag.SetGameManager(this);
        }

        RebuildSpawnLayout();
    }

    // =========================================================
    // NIVEL 3/4 (Hueco fantasma)
    // =========================================================
    private void SetupLevel3GhostSlotScenario()
    {
        if (weightsSpawnContainer == null || weightPrefab == null) return;

        int emptyIndex = Random.Range(0, 3);

        int v1 = Random.Range(minW, maxW + 1);
        int v2 = Random.Range(minW, maxW + 1);
        int safety = 0;
        while (v2 == v1 && safety < 200)
        {
            safety++;
            v2 = Random.Range(minW, maxW + 1);
        }

        int[] target = new int[3];

        int fixedAIndex = (emptyIndex == 0) ? 1 : 0;
        int fixedBIndex = (emptyIndex == 2) ? 1 : 2;

        int low = Mathf.Min(v1, v2);
        int high = Mathf.Max(v1, v2);

        if (ascendingRule)
        {
            target[fixedAIndex] = low;
            target[fixedBIndex] = high;
        }
        else
        {
            target[fixedAIndex] = high;
            target[fixedBIndex] = low;
        }

        int topVal = target[fixedAIndex];
        int bottomVal = target[fixedBIndex];
        int minBound = Mathf.Min(topVal, bottomVal);
        int maxBound = Mathf.Max(topVal, bottomVal);

        int correct;

        if (emptyIndex == 1)
        {
            if (maxBound - minBound <= 1) { SetupLevel3GhostSlotScenario(); return; }
            correct = Random.Range(minBound + 1, maxBound);
        }
        else if (emptyIndex == 0)
        {
            if (ascendingRule)
            {
                if (minBound <= minW) { SetupLevel3GhostSlotScenario(); return; }
                correct = Random.Range(minW, minBound);
            }
            else
            {
                if (maxBound >= maxW) { SetupLevel3GhostSlotScenario(); return; }
                correct = Random.Range(maxBound + 1, maxW + 1);
            }
        }
        else
        {
            if (ascendingRule)
            {
                if (maxBound >= maxW) { SetupLevel3GhostSlotScenario(); return; }
                correct = Random.Range(maxBound + 1, maxW + 1);
            }
            else
            {
                if (minBound <= minW) { SetupLevel3GhostSlotScenario(); return; }
                correct = Random.Range(minW, minBound);
            }
        }

        target[emptyIndex] = correct;

        for (int i = 0; i < 3; i++)
        {
            placeholders[i].SetCorrectWeight(target[i]);
            placeholders[i].ForceSetOccupied(false);
        }

        for (int i = 0; i < 3; i++)
        {
            if (i == emptyIndex) continue;
            PlaceFixedWeightIntoPlaceholder(i, target[i]);
        }

        List<int> options = new List<int> { correct };

        int d1, d2;

        if (emptyIndex == 1)
        {
            d1 = GetRandomLessThan(minBound);
            d2 = GetRandomGreaterThan(maxBound);
        }
        else if (emptyIndex == 2)
        {
            if (ascendingRule)
            {
                d1 = GetRandomBetween(minW, minBound);
                d2 = GetRandomBetween(minBound + 1, maxBound);
            }
            else
            {
                d1 = GetRandomBetween(maxBound + 1, maxW + 1);
                d2 = GetRandomBetween(minBound + 1, maxBound);
            }
        }
        else
        {
            if (ascendingRule)
            {
                d1 = GetRandomBetween(maxBound + 1, maxW + 1);
                d2 = GetRandomBetween(minBound + 1, maxBound);
            }
            else
            {
                d1 = GetRandomBetween(minW, minBound);
                d2 = GetRandomBetween(minBound + 1, maxBound);
            }
        }

        d1 = FixCandidate(d1, options, target);
        options.Add(d1);

        d2 = FixCandidate(d2, options, target);
        options.Add(d2);

        Shuffle(options);

        foreach (int w in options)
        {
            GameObject go = Instantiate(weightPrefab, weightsSpawnContainer);
            var drag = go.GetComponent<WeightDraggable>();
            drag.SetWeight(w);
            drag.SetGameManager(this);
        }

        RebuildSpawnLayout();

        if (useTimeBar) StartTimeBar();
    }

    private int GetRandomLessThan(int threshold)
    {
        if (threshold <= minW) return minW;
        return Random.Range(minW, threshold);
    }

    private int GetRandomGreaterThan(int threshold)
    {
        if (threshold >= maxW) return maxW;
        return Random.Range(threshold + 1, maxW + 1);
    }

    private int GetRandomBetween(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return Random.Range(minW, maxW + 1);
        return Random.Range(minInclusive, maxExclusive);
    }

    private int FixCandidate(int candidate, List<int> currentOptions, int[] target)
    {
        int safety = 0;
        while ((currentOptions.Contains(candidate) || target.Contains(candidate)) && safety < 300)
        {
            safety++;
            candidate = Random.Range(minW, maxW + 1);
        }
        return candidate;
    }

    private void PlaceFixedWeightIntoPlaceholder(int placeholderIndex, int weight)
    {
        if (placeholderIndex < 0 || placeholderIndex >= placeholders.Length) return;
        var ph = placeholders[placeholderIndex];
        if (ph == null) return;

        ph.ForceSetOccupied(true);

        GameObject go = Instantiate(weightPrefab, ph.transform);
        var drag = go.GetComponent<WeightDraggable>();
        drag.SetWeight(weight);
        drag.SetGameManager(this);
        drag.DisableDrag();

        RectTransform wRect = go.GetComponent<RectTransform>();
        RectTransform dropRect = ph.GetComponent<RectTransform>();
        Vector3 dropWorldCenter = dropRect.TransformPoint(dropRect.rect.center);

        wRect.SetParent(dropRect, worldPositionStays: true);
        wRect.position = dropWorldCenter;
        wRect.localRotation = Quaternion.identity;
        wRect.localScale = Vector3.one;
    }

    // =========================================================
    // VALIDACIÓN / FEEDBACK + SCORING + RONDAS
    // =========================================================
    public void OnWrongWeightDropped(int dropped, int correct)
    {
        // ✅ Sistema: error
        if (GameSessionManager.I != null) GameSessionManager.I.AddError();
        roundErrors++;

        if (giveStrongHints)
            ShowFeedback(dropped < correct ? "Necesitas una pesa MÁS PESADA" : "Necesitas una pesa MÁS LIGERA", feedbackSeconds);
        else if (giveMediumHints)
            ShowFeedback(dropped < correct ? "Te has quedado corto" : "Te has pasado", feedbackSeconds);
        else
            ShowFeedback("Esa no es", feedbackSeconds);
    }

    public void ShowFeedbackCorrect()
    {
        ShowFeedback("¡Muy bien!", feedbackSeconds);
    }

    private void ShowFeedbackWin()
    {
        ShowFeedback("¡Acertaste!", winFeedbackSeconds);
    }

    private void ShowFeedback(string message, float seconds)
    {
        if (hintFeedbackRoutine != null)
            StopCoroutine(hintFeedbackRoutine);

        hintFeedbackRoutine = StartCoroutine(FeedbackCoroutine(message, seconds));
    }

    private IEnumerator FeedbackCoroutine(string message, float seconds)
    {
        if (hintContainer) hintContainer.SetActive(true);
        if (hintTMP) hintTMP.text = message;

        yield return new WaitForSecondsRealtime(seconds);

        if (hintTMP) hintTMP.text = "";
        hintFeedbackRoutine = null;
    }

    public void OnCorrectPlaced()
    {
        if (roundWinRunning) return;

        bool all = placeholders.Length == 3 && placeholders.All(p => p != null && p.Occupied);
        if (!all) return;

        roundWinRunning = true;

        // ✅ Sistema: acierto + firstTry
        if (GameSessionManager.I != null) GameSessionManager.I.AddCorrect(firstTry: roundErrors == 0);

        correctRoundsThisAttempt++;
        UpdateRoundsRemainingUI();

        StopTiming();
        DisableAllDrags();

        StartCoroutine(RoundSolvedCoroutine());
    }

    private IEnumerator RoundSolvedCoroutine()
    {
        // Flash verde de ronda
        yield return StartCoroutine(FlashGreenCoroutine());
        yield return new WaitForSecondsRealtime(0.05f);

        // ✅ ¿Se completó el nivel por rondas?
        if (correctRoundsThisAttempt >= goalCorrectRounds)
        {
            EndAttemptIfStarted(completed: true);
            GoToResults();
            yield break;
        }

        // Si no, siguiente ronda
        StartRound();
        if (useChronometer) StartChronometer();
        if (useTimeBar) StartTimeBar();

        roundWinRunning = false;
    }

    // =========================================================
    // FLASH VERDE
    // =========================================================
    private IEnumerator FlashGreenCoroutine()
    {
        if (imageFeedbackFlash == null) yield break;

        float t = 0f;
        float durIn = Mathf.Max(0.01f, flashInSeconds);
        while (t < durIn)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(0f, flashMaxAlpha, t / durIn);
            SetFlashAlpha(a);
            yield return null;
        }

        t = 0f;
        float durOut = Mathf.Max(0.01f, flashOutSeconds);
        while (t < durOut)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(flashMaxAlpha, 0f, t / durOut);
            SetFlashAlpha(a);
            yield return null;
        }

        SetFlashAlpha(0f);
    }

    private void SetFlashAlpha(float a)
    {
        if (imageFeedbackFlash == null) return;
        Color c = imageFeedbackFlash.color;
        c.a = Mathf.Clamp01(a);
        imageFeedbackFlash.color = c;
    }

    private void DisableAllDrags()
    {
        foreach (var d in FindObjectsOfType<WeightDraggable>())
            d.DisableDrag();
    }

    // =========================================================
    // LAYOUT
    // =========================================================
    public void RebuildSpawnLayout()
    {
        if (weightsSpawnContainer == null) return;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(weightsSpawnContainer);
        Canvas.ForceUpdateCanvases();
    }

    // =========================================================
    // CRONÓMETRO / BARRA
    // =========================================================
    private void StartChronometer()
    {
        chronometerTime = 0f;
        chronometerRunning = true;
        UpdateChronometerUI();
    }

    private void UpdateChronometerUI()
    {
        if (!chronometerTMP) return;
        int total = Mathf.FloorToInt(chronometerTime);
        int m = total / 60;
        int s = total % 60;
        chronometerTMP.text = $"{m:00}:{s:00}";
    }

    private void StartTimeBar()
    {
        timeRemaining = level4TimeLimit * Mathf.Max(0.01f, timeFactor);
        timeBarRunning = true;
        UpdateTimeBarUI();
    }

    private void UpdateTimeBarUI()
    {
        if (!timeBarFill) return;

        float denom = level4TimeLimit * Mathf.Max(0.01f, timeFactor);
        float ratio = denom <= 0f ? 0f : Mathf.Clamp01(timeRemaining / denom);

        timeBarFill.fillAmount = ratio;

        if (ratio > 0.6f) timeBarFill.color = Color.white;
        else if (ratio > 0.3f) timeBarFill.color = Color.yellow;
        else timeBarFill.color = Color.red;
    }

    private void StopTiming()
    {
        chronometerRunning = false;
        timeBarRunning = false;
    }

    // =========================================================
    // PAUSA / SALIDA
    // =========================================================
    public void PauseGame()
    {
        if (IsPaused) return;
        IsPaused = true;
        Time.timeScale = 0f;
        if (pausePanel) pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = 1f;
        if (pausePanel) pausePanel.SetActive(false);
    }

    public void ExitToHub()
    {
        // Si se sale manualmente sin haber ganado, se cierra como no completado.
        EndAttemptIfStarted(completed: false);

        Time.timeScale = 1f;
        IsPaused = false;
        if (pausePanel) pausePanel.SetActive(false);

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
        else
            Debug.LogError("[WeightOrderGameManager] hubSceneName no configurado");
    }

    private void OnDestroy()
    {
        EndAttemptIfStarted(completed: false);
    }

    private void Shuffle(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // =========================================================
    //  CLEVERMINDS: nivel + attempt
    // =========================================================
    private void LoadFromGameSessionManagerOrFallback()
    {
        if (GameSessionManager.I != null && GameSessionManager.I.profile != null)
        {
            // LevelId (1..4) mapea 1:1 con WeightOrderLevel (1..4)
            currentLevel = (WeightOrderLevel)GameSessionManager.I.currentSelection.level;
            return;
        }

        currentLevel = (WeightOrderLevel)fallbackLevelForTesting;
    }

    private void BeginAttemptIfPossible()
    {
        if (!saveAttemptOnExit) return;
        if (attemptStarted) return;
        if (GameSessionManager.I == null || GameSessionManager.I.profile == null) return;

        float limit = (currentLevel == WeightOrderLevel.Level4)
            ? Mathf.Max(0.1f, level4TimeLimit * Mathf.Max(0.01f, timeFactor))
            : 0f;

        GameSessionManager.I.BeginAttempt(limit);
        attemptStarted = true;
        attemptEnded = false;
    }

    private void EndAttemptIfStarted(bool completed)
    {
        if (!saveAttemptOnExit) return;
        if (!attemptStarted || attemptEnded) return;
        if (GameSessionManager.I == null || GameSessionManager.I.currentAttempt == null) return;

        // Tiempo final consistente con el HUD
        if (useChronometer)
        {
            GameSessionManager.I.currentAttempt.timeSeconds = chronometerTime;
        }
        else if (useTimeBar)
        {
            float limit = level4TimeLimit * Mathf.Max(0.01f, timeFactor);
            float elapsed = Mathf.Max(0f, limit - timeRemaining);
            GameSessionManager.I.currentAttempt.timeSeconds = elapsed;
        }

        GameSessionManager.I.EndAttempt(completed);
        attemptEnded = true;
    }

    private const string resultsSceneName = "05_Results";

    private void GoToResults()
    {
        Time.timeScale = 1f;
        IsPaused = false;
        SceneManager.LoadScene(resultsSceneName);
    }
}

