using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Minijuego "Completa la palabra" integrado en la arquitectura de CleverMinds.
/// - NO cambia mecánicas: mantiene rondas, letras draggable y placeholders.
/// - Añade integración con GameSessionManager (BeginAttempt / AddCorrect / AddError / EndAttempt)
/// - Lee el nivel desde GameSessionManager (con fallback para test).
/// - Añade objetivo de rondas por nivel (como EncajaLaLlave) para poder cerrar el intento y volver al Hub.
/// </summary>
public class WordFillGameManager : MonoBehaviour
{
    // =========================================================
    //  CONFIGURACIÓN DE NIVEL (Integración CleverMinds)
    // =========================================================
    [Header("Level Config (Integración)")]
    [SerializeField] private LevelId fallbackLevelForTesting = LevelId.Level1;
    private LevelId currentLevel = LevelId.Level1;

    [Tooltip("Multiplicador del tiempo en Level 4 (1=normal, 0.8 más rápido, 1.2 más lento)")]
    [SerializeField] private float timeFactor = 1f;

    // =========================================================
    //  OBJETIVO DEL NIVEL (para poder cerrar el intento)
    // =========================================================
    [Header("Objetivo (nº de palabras completadas para completar el nivel)")]
    [SerializeField] private int goalCorrectRounds_Level1 = 3;
    [SerializeField] private int goalCorrectRounds_Level2 = 4;
    [SerializeField] private int goalCorrectRounds_Level3 = 6;
    [SerializeField] private int goalCorrectRounds_Level4 = 6;

    private int goalCorrectRounds = 3;
    private int correctRoundsThisAttempt = 0;

    // Errores dentro de la ronda actual (palabra actual) para firstTryCorrect
    private int roundErrors = 0;

    private bool attemptEnded = false;

    // -------- Level tuning (mecánica interna) --------
    private int minLen, maxLen, holesMin, holesMax, distractorsExtra;
    private bool hintActive, useChronometer, useTimeBar;

    [Header("UI - Título / Nivel")]
    [SerializeField] private TextMeshProUGUI titleTMP;
    [SerializeField] private TextMeshProUGUI levelTMP;

    // (Opcional) contador de rondas restantes (solo número)
    [Header("UI - Rondas restantes (solo número, opcional)")]
    [SerializeField] private TextMeshProUGUI roundsRemainingTMP;

    [Header("UI - Palabra")]
    [SerializeField] private TextMeshProUGUI wordTMP;
    [SerializeField] private RectTransform placeholdersCanvas;

    [Header("Placeholders Size (tamaño fijo configurable)")]
    [SerializeField] private bool useFixedPlaceholderSize = true;
    [SerializeField] private Vector2 fixedPlaceholderSize = new Vector2(80f, 80f);

    [Header("UI - Caja Pista/Feedback (misma caja)")]
    [SerializeField] private GameObject hintContainer;
    [SerializeField] private TextMeshProUGUI hintTMP;
    [SerializeField] private float feedbackSeconds = 1.25f;
    [SerializeField] private float winFeedbackSeconds = 1.6f;
    private Coroutine hintFeedbackRoutine;
    private string hintBaseText = "";

    [Header("UI - Cronómetro (L1-L3)")]
    [SerializeField] private GameObject chronometerContainer;
    [SerializeField] private TextMeshProUGUI chronometerTMP;
    private float chronometerTime = 0f;
    private bool chronometerRunning = false;

    [Header("UI - Barra de tiempo (L4)")]
    [SerializeField] private GameObject timeBarContainer;
    [SerializeField] private Image timeBarFill;

    [Tooltip("Tiempo base de Level 4 (si NO hay GameSessionManager o no hay tuning).")]
    [SerializeField] private float level4TimeLimit = 15f;

    [Tooltip("Cuando se acaba el tiempo, cuánto dura el parpadeo de letras faltantes")]
    [SerializeField] private float revealBlinkDuration = 2f;

    [Tooltip("Frecuencia de parpadeo (veces por segundo)")]
    [SerializeField] private float revealBlinkHz = 6f;

    private float timeRemaining = 0f;
    private bool timeBarRunning = false;

    [Header("Pausa")]
    [SerializeField] private GameObject pausePanel;
    public bool IsPaused { get; private set; } = false;

    [Header("Answers Layout (HorizontalLayoutGroup parent)")]
    [SerializeField] private RectTransform answersContainer;

    [Header("Prefabs/Managers")]
    [SerializeField] private GameObject placeholderPrefab;
    [SerializeField] private AnswerManagerLetras answerManager;
    [SerializeField] private WordPicker wordPicker;

    [Header("Opciones máximas en el contenedor")]
    [SerializeField] private int maxOptions = 8; // límite UI real

    [Header("Hub / Salida")]
    [SerializeField] private string hubSceneName = "03_MinigameHub";

    // -------- Estado ronda --------
    private string currentWord;
    private string currentHint;

    private readonly List<int> holeIndices = new();
    private readonly List<char> hiddenLetters = new();
    private readonly List<PlaceholderDrop> placeholders = new();

    private Canvas rootCanvas;

    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        LoadFromGameSessionManagerOrFallback();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        IsPaused = false;
        if (pausePanel) pausePanel.SetActive(false);

        SetupLevelConfig();   // usa maxOptions=8
        SetupUI();
        UpdateRoundsRemainingUI();

        StartRound();

        if (useChronometer) StartChronometer();
        if (useTimeBar) StartTimeBar();

        BeginAttemptInSystem();
    }

    private void Update()
    {
        if (IsPaused) return;

        // Tick tiempo del intento (solo si existe y no se cerró)
        if (GameSessionManager.I != null && GameSessionManager.I.currentAttempt != null && !attemptEnded)
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

                // ✅ Revelar letras faltantes parpadeando (mecánica original)
                RevealMissingLettersLevel4();

                // Feedback
                StartFeedback("Se acabó el tiempo", winFeedbackSeconds);

                // Espera para ver el reveal + vuelve a empezar (mecánica original)
                StartCoroutine(NextRoundCoroutineWithDelay(revealBlinkDuration + 0.5f));
            }
        }
    }

    // =========================================================
    //  SINGLETON / NIVEL DESDE SESIÓN
    // =========================================================
    private void LoadFromGameSessionManagerOrFallback()
    {
        if (GameSessionManager.I != null)
        {
            currentLevel = GameSessionManager.I.currentSelection.level;
        }
        else
        {
            currentLevel = fallbackLevelForTesting;
        }
    }

    // =========================================================
    // NIVEL (adaptado a máximo 8 opciones) + objetivo de rondas
    // =========================================================
    private void SetupLevelConfig()
    {
        // Regla: opciones = huecos + distractores <= maxOptions (8)
        switch (currentLevel)
        {
            case LevelId.Level1:
                minLen = 3; maxLen = 5;
                holesMin = 1; holesMax = 1;
                distractorsExtra = 2; // 1+2=3
                hintActive = true;
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level1);
                break;

            case LevelId.Level2:
                minLen = 5; maxLen = 8;
                holesMin = 2; holesMax = 2;
                distractorsExtra = 4; // 2+4=6
                hintActive = true;
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level2);
                break;

            case LevelId.Level3:
                minLen = 7; maxLen = 12;
                holesMin = 3; holesMax = 3;
                distractorsExtra = 5; // 3+5=8
                hintActive = false;
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level3);
                break;

            case LevelId.Level4:
                minLen = 7; maxLen = 12;
                holesMin = 3; holesMax = 3;
                distractorsExtra = 5; // 3+5=8
                hintActive = false;
                useChronometer = false;
                useTimeBar = true;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level4);
                break;
        }

        // Si el nivel 4 tiene tuning en el MiniGameConfig, lo usamos como tiempo base
        if (useTimeBar && GameSessionManager.I != null)
        {
            var tuning = GameSessionManager.I.GetTuning();
            if (tuning != null && tuning.targetTimeSeconds > 0.1f)
                level4TimeLimit = tuning.targetTimeSeconds;
        }

        correctRoundsThisAttempt = 0;
        roundErrors = 0;
        attemptEnded = false;
    }

    private void SetupUI()
    {
        if (titleTMP) titleTMP.text = "Completa la palabra";
        if (levelTMP) levelTMP.text = $"NIVEL {(int)currentLevel}";

        if (chronometerContainer) chronometerContainer.SetActive(useChronometer);
        if (timeBarContainer) timeBarContainer.SetActive(useTimeBar);

        // Caja pista/feedback:
        // - L1-L2: visible (pista fija)
        // - L3-L4: oculta hasta feedback
        if (hintContainer) hintContainer.SetActive(hintActive);

        if (useChronometer && chronometerTMP) chronometerTMP.text = "00:00";

        if (useTimeBar && timeBarFill)
        {
            timeBarFill.fillAmount = 1f;
            timeBarFill.color = Color.white;
        }
    }

    private void UpdateRoundsRemainingUI()
    {
        if (roundsRemainingTMP == null) return;
        int remaining = Mathf.Max(0, goalCorrectRounds - correctRoundsThisAttempt);
        roundsRemainingTMP.text = remaining.ToString();
    }

    // =========================================================
    // RONDA
    // =========================================================
    private void StartRound()
    {
        ClearRound();

        bool requireHint = hintActive; // L1-L2 obligatorio
        var pick = wordPicker.Pick(minLen, maxLen, requireHint);

        currentWord = pick.word;
        currentHint = pick.hint;

        BuildWordWithInvisibleHoles(currentWord);
        SpawnPlaceholdersFromTMP();
        SpawnLetters();

        // SOLO pista del SO
        if (hintActive)
        {
            hintBaseText = $"Pista: {currentHint}";
            SetHintBox(hintBaseText, true);
        }
        else
        {
            hintBaseText = "";
            SetHintBox("", false);
        }

        // Reinicia los errores de esta palabra (para firstTryCorrect)
        roundErrors = 0;

        if (useTimeBar) StartTimeBar();
    }

    private void ClearRound()
    {
        // borrar placeholders
        for (int i = placeholdersCanvas.childCount - 1; i >= 0; i--)
            Destroy(placeholdersCanvas.GetChild(i).gameObject);

        placeholders.Clear();
        holeIndices.Clear();
        hiddenLetters.Clear();

        // borrar letras
        if (answerManager) answerManager.Clear();

        // parar feedback coroutine
        if (hintFeedbackRoutine != null)
        {
            StopCoroutine(hintFeedbackRoutine);
            hintFeedbackRoutine = null;
        }
    }

    // ✅ NO "_" → letra real invisible, mantiene métrica perfecta
    private void BuildWordWithInvisibleHoles(string word)
    {
        int holesCount = Random.Range(holesMin, holesMax + 1);
        holesCount = Mathf.Clamp(holesCount, 1, Mathf.Max(1, word.Length - 1));

        HashSet<int> set = new HashSet<int>();
        while (set.Count < holesCount)
        {
            int idx = Random.Range(0, word.Length);

            // en niveles con pista, no ocultes extremos (más fácil)
            if (hintActive && (idx == 0 || idx == word.Length - 1))
                continue;

            set.Add(idx);
        }

        holeIndices.AddRange(set);
        holeIndices.Sort();

        foreach (int idx in holeIndices)
            hiddenLetters.Add(word[idx]);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < word.Length; i++)
        {
            char c = word[i];
            if (set.Contains(i))
                sb.Append("<color=#00000000>").Append(c).Append("</color>");
            else
                sb.Append(c);
        }

        wordTMP.text = sb.ToString();
        wordTMP.ForceMeshUpdate();
    }

    private void SpawnPlaceholdersFromTMP()
    {
        TMP_TextInfo info = wordTMP.textInfo;
        Camera cam = GetCanvasCamera();

        for (int k = 0; k < holeIndices.Count; k++)
        {
            int charIndex = holeIndices[k];
            if (charIndex >= info.characterCount) continue;

            var cInfo = info.characterInfo[charIndex];

            // Centro del carácter
            Vector3 worldPos = wordTMP.transform.TransformPoint((cInfo.bottomLeft + cInfo.topRight) / 2f);
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPos);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                placeholdersCanvas,
                screenPos,
                cam,
                out Vector2 localPos
            );

            GameObject ph = Instantiate(placeholderPrefab, placeholdersCanvas);
            RectTransform r = ph.GetComponent<RectTransform>();
            r.anchoredPosition = localPos;

            // Tamaño fijo configurable
            if (useFixedPlaceholderSize)
            {
                r.sizeDelta = fixedPlaceholderSize;
            }
            else
            {
                float w = cInfo.topRight.x - cInfo.bottomLeft.x;
                float h = cInfo.topRight.y - cInfo.bottomLeft.y;
                r.sizeDelta = new Vector2(w, h);
            }

            var drop = ph.GetComponent<PlaceholderDrop>();
            drop.SetGameManager(this);
            drop.SetCorrectLetter(hiddenLetters[k]);

            placeholders.Add(drop);
        }
    }

    private Camera GetCanvasCamera()
    {
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) return Camera.main;

        if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
    }

    private void SpawnLetters()
    {
        // ✅ Airbag: nunca pasar de 8 opciones (por UI)
        distractorsExtra = Mathf.Max(0, Mathf.Min(distractorsExtra, maxOptions - hiddenLetters.Count));

        answerManager.distractoresExtra = distractorsExtra;

        List<char> alphabet = new List<char>("ABCDEFGHIJKLMNÑOPQRSTUVWXYZ".ToCharArray());
        answerManager.SetupLetters(hiddenLetters, alphabet, this);

        RebuildAnswersLayout();
    }

    // =========================================================
    // REBUILD LAYOUT
    // =========================================================
    public void RebuildAnswersLayout()
    {
        if (answersContainer == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(answersContainer);
        Canvas.ForceUpdateCanvases();
    }

    // =========================================================
    // FEEDBACK (misma caja) + integración errores
    // =========================================================
    public void ShowFeedbackWrong()
    {
        // Integración: cuenta error en el intento
        if (GameSessionManager.I != null) GameSessionManager.I.AddError();
        roundErrors++;

        StartFeedback("Esa no es", feedbackSeconds);
    }

    public void ShowFeedbackCorrect()
    {
        StartFeedback("¡Muy bien!", feedbackSeconds);
    }

    public void ShowFeedbackWin()
    {
        StartFeedback("¡Acertaste!", winFeedbackSeconds);
    }

    private void StartFeedback(string message, float seconds)
    {
        if (hintFeedbackRoutine != null)
            StopCoroutine(hintFeedbackRoutine);

        hintFeedbackRoutine = StartCoroutine(FeedbackCoroutine(message, seconds));
    }

    private IEnumerator FeedbackCoroutine(string message, float seconds)
    {
        SetHintBox(message, true);
        yield return new WaitForSecondsRealtime(seconds);

        if (hintActive)
            SetHintBox(hintBaseText, true);
        else
            SetHintBox("", false);

        hintFeedbackRoutine = null;
    }

    private void SetHintBox(string msg, bool visible)
    {
        if (hintTMP) hintTMP.text = msg;
        if (hintContainer) hintContainer.SetActive(visible);
    }

    // =========================================================
    // CHECK VICTORIA (completar palabra)
    // =========================================================
    public void OnCorrectPlaced()
    {
        bool all = placeholders.Count > 0 && placeholders.All(p => p.Occupied);
        if (!all) return;

        StopTiming();
        ShowFeedbackWin();

        // Integración: se considera 1 "acierto" por palabra completada
        if (GameSessionManager.I != null)
            GameSessionManager.I.AddCorrect(firstTry: roundErrors == 0);

        correctRoundsThisAttempt++;
        UpdateRoundsRemainingUI();

        foreach (var d in FindObjectsOfType<LetterDraggable>())
            d.DisableDrag();

        // Si ya hemos cumplido el objetivo de nivel, cerramos intento y volvemos al hub
        if (correctRoundsThisAttempt >= goalCorrectRounds)
        {
            StartCoroutine(WinAndExitCoroutine());
            return;
        }

        StartCoroutine(NextRoundCoroutineWithDelay(1.0f));
    }

    private IEnumerator WinAndExitCoroutine()
    {
        yield return new WaitForSecondsRealtime(1.0f);

        EndAttemptInSystem(completed: true);
        GoToResults();
    }

    // =========================================================
    // NIVEL 4: reveal letras faltantes
    // =========================================================
    private void RevealMissingLettersLevel4()
    {
        if (!useTimeBar) return;

        foreach (var ph in placeholders)
        {
            if (ph != null && !ph.Occupied)
                ph.RevealCorrectLetterWithBlink(revealBlinkDuration, revealBlinkHz);
        }
    }

    private IEnumerator NextRoundCoroutineWithDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        StartRound();

        if (useChronometer) StartChronometer();
        if (useTimeBar) StartTimeBar();
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
        float baseLimit = level4TimeLimit * Mathf.Max(0.01f, timeFactor);
        timeRemaining = baseLimit;
        timeBarRunning = true;
        UpdateTimeBarUI();
    }

    private void UpdateTimeBarUI()
    {
        if (!timeBarFill) return;

        float denom = level4TimeLimit * Mathf.Max(0.01f, timeFactor);
        float ratio = denom <= 0f ? 0f : Mathf.Clamp01(timeRemaining / denom);

        timeBarFill.fillAmount = ratio;

        // ✅ Blanco -> Amarillo -> Rojo
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
    // PAUSA
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
        // Si el usuario sale manualmente, cerramos intento como NO completado (si no estaba ya)
        EndAttemptInSystem(completed: false);

        // Salimos sin pausa
        Time.timeScale = 1f;
        IsPaused = false;

        if (pausePanel) pausePanel.SetActive(false);

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
        else
            Debug.LogError("[WordFillGameManager] hubSceneName no configurado");
    }

    // =========================================================
    //  INTEGRACIÓN CON GAMESESSIONMANAGER (intentos + guardado)
    // =========================================================
    private void BeginAttemptInSystem()
    {
        if (GameSessionManager.I == null) return;
        if (attemptEnded) return;

        float limit = useTimeBar ? Mathf.Max(0.1f, level4TimeLimit * Mathf.Max(0.01f, timeFactor)) : 0f;
        GameSessionManager.I.BeginAttempt(limit);
    }

    private void EndAttemptInSystem(bool completed)
    {
        if (attemptEnded) return;
        if (GameSessionManager.I == null) return;
        if (GameSessionManager.I.currentAttempt == null) return;

        // Sin cambiar mecánicas: registramos el tiempo del modo UI
        if (useChronometer)
        {
            GameSessionManager.I.currentAttempt.timeSeconds = chronometerTime;
        }
        else if (useTimeBar)
        {
            float limit = level4TimeLimit * Mathf.Max(0.01f, timeFactor);
            float elapsed = Mathf.Max(0f, limit - timeRemaining);
            GameSessionManager.I.currentAttempt.timeSeconds = elapsed;
            GameSessionManager.I.currentAttempt.timeLimitSeconds = limit;
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

