using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public enum SpanishCultureQuizLevel
{
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
    Level4 = 4
}

public class SpanishCultureQuizGameManager : MonoBehaviour
{
    // =========================================================
    //  CONFIGURACIÓN DE NIVEL
    // =========================================================
    [Header("Level Config")]
    [SerializeField] private SpanishCultureQuizLevel currentLevel = SpanishCultureQuizLevel.Level1;

    [Header("CleverMinds (solo testing si no hay GameSessionManager)")]
    [Tooltip("Si ejecutas esta escena suelta, se usará este nivel.")]
    [SerializeField] private SpanishCultureQuizLevel fallbackLevelForTesting = SpanishCultureQuizLevel.Level1;

    [Header("Rondas objetivo (por nivel)")]
    [SerializeField] private int goalCorrectRounds_Level1 = 5;
    [SerializeField] private int goalCorrectRounds_Level2 = 6;
    [SerializeField] private int goalCorrectRounds_Level3 = 7;
    [SerializeField] private int goalCorrectRounds_Level4 = 7;

    private int goalCorrectRounds = 5;
    private int correctRoundsThisAttempt = 0;
    private int roundsPlayedThisAttempt = 0;
    private bool failedAnyRoundThisAttempt = false;

    private int optionsCount = 2;
    private bool useChronometer = false;
    private bool useTimeBar = false;

    // =========================================================
    //  ACCESIBILIDAD / PERFIL
    // =========================================================
    [Header("Accesibilidad (temporal en inspector)")]
    [SerializeField] private bool hasVisualDifficulty = false;
    [SerializeField] private bool hasAuditoryDifficulty = false;

    [Header("CleverMinds - Guardado intento")]
    [Tooltip("Si es true, al finalizar (win/timeout/salir) se guarda el intento (BeginAttempt/EndAttempt).")]
    [SerializeField] private bool saveAttemptOnExit = true;

    private bool attemptStarted = false;
    private bool attemptEnded = false;

    // =========================================================
    //  SCRIPTABLE OBJECTS DE PREGUNTAS
    // =========================================================
    [Header("Categorías de preguntas (SO)")]
    [SerializeField] private SpanishCultureCategorySO[] questionCategories;

    // =========================================================
    //  UI PRINCIPAL
    // =========================================================
    [Header("UI General")]
    [SerializeField] private TextMeshProUGUI textMinijuegoTitulo;
    [SerializeField] private TextMeshProUGUI textNivelActual;
    [SerializeField] private TextMeshProUGUI textPregunta;
    [SerializeField] private Image imagePregunta;
    [SerializeField] private TextMeshProUGUI textFeedback;
    [SerializeField] private Image imageFeedbackFlash;

    [Header("UI Rondas (solo número)")]
    [SerializeField] private TextMeshProUGUI roundsRemainingText;

    [Header("Start Panel")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private TextMeshProUGUI level4StartWarningText;

    // =========================================================
    //  CONTENEDORES DE RESPUESTA
    // =========================================================
    [Header("Contenedores de respuestas")]
    [SerializeField] private GameObject answersContainer2Options;
    [SerializeField] private GameObject answersContainer3Options;
    [SerializeField] private GameObject answersContainer4Options;

    [Header("Botones respuesta nivel 1 (2 opciones)")]
    [SerializeField] private List<SpanishCultureAnswerButtonController> answerButtons2 = new List<SpanishCultureAnswerButtonController>();

    [Header("Botones respuesta nivel 2 (3 opciones)")]
    [SerializeField] private List<SpanishCultureAnswerButtonController> answerButtons3 = new List<SpanishCultureAnswerButtonController>();

    [Header("Botones respuesta niveles 3 y 4 (4 opciones)")]
    [SerializeField] private List<SpanishCultureAnswerButtonController> answerButtons4 = new List<SpanishCultureAnswerButtonController>();

    // =========================================================
    //  UI CRONÓMETRO / TIEMPO / PAUSA
    // =========================================================
    [Header("UI Cronómetro (niveles 1-3)")]
    [SerializeField] private GameObject chronometerContainer;
    [SerializeField] private TextMeshProUGUI textChronometer;

    [Header("UI Barra de Tiempo (nivel 4)")]
    [SerializeField] private GameObject timeBarContainer;
    [SerializeField] private Image timeBarFillImage;

    [Header("UI Pausa")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private string hubSceneName = "03_MinigameHub";

    // =========================================================
    //  TIMERS
    // =========================================================
    [Header("Timing (Barra de tiempo - Nivel 4)")]
    [SerializeField] private float timeLimitSeconds = 20f;

    [Header("Timing - Fallback (si no hay MiniGameConfig)")]
    [SerializeField] private float fallbackTimeLimitSeconds_Level4 = 20f;

    private float currentTime = 0f;
    private bool timeBarRunning = false;

    private float chronometerTime = 0f;
    private bool chronometerRunning = false;

    // =========================================================
    //  ESTADO DE PARTIDA / RONDA
    // =========================================================
    private List<string> currentRoundAnswers = new List<string>();
    private int currentRoundCorrectIndex = -1;

    private bool isPaused = false;
    private bool gameStarted = false;
    private bool waitingForAnswer = false;
    private bool roundResolved = false;

    private int incorrectClicksThisRound = 0;

    private SpanishCultureCategorySO.SpanishCultureQuestionData currentQuestion = null;
    private SpanishCultureAnswerButtonController correctAnswerButton = null;

    // Cache config/tuning (Core)
    private MiniGameConfig cfg;
    private MiniGameConfig.LevelTuning tuning;

    private const string resultsSceneName = "05_Results";

    // =========================================================
    //  UNITY
    // =========================================================
    private void Awake()
    {
        LoadPlayerDataFromSingleton();
        PullConfigTuningFromCore();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        SetupLevelConfig();
        SetupUI();
        BindAllAnswerButtons();
        SetupStartPanel();

        if (startPanel == null)
            BeginGame();
    }

    private void Update()
    {
        if (attemptStarted && !attemptEnded && !isPaused && GameSessionManager.I != null)
            GameSessionManager.I.TickTime(Time.deltaTime);

        if (useChronometer && chronometerRunning && !isPaused)
        {
            chronometerTime += Time.deltaTime;
            UpdateChronometerUI();
        }

        if (useTimeBar && timeBarRunning && !isPaused)
        {
            currentTime -= Time.deltaTime;
            UpdateTimeBarUI();

            if (currentTime <= 0f)
            {
                currentTime = 0f;
                timeBarRunning = false;
                OnTimeExpired();
            }
        }
    }

    // =========================================================
    //  CLEVERMINDS (perfil/nivel)
    // =========================================================
    private void LoadPlayerDataFromSingleton()
    {
        if (GameSessionManager.I != null && GameSessionManager.I.profile != null)
        {
            hasVisualDifficulty = GameSessionManager.I.profile.hasVisionIssues;
            hasAuditoryDifficulty = GameSessionManager.I.profile.hasHearingIssues;
            currentLevel = (SpanishCultureQuizLevel)GameSessionManager.I.currentSelection.level;
            return;
        }

        currentLevel = fallbackLevelForTesting;
    }

    private void PullConfigTuningFromCore()
    {
        if (GameSessionManager.I == null) return;

        cfg = GameSessionManager.I.GetConfig();
        tuning = GameSessionManager.I.GetTuning();

        if (currentLevel == SpanishCultureQuizLevel.Level4)
        {
            if (tuning != null && tuning.targetTimeSeconds > 0.1f)
                timeLimitSeconds = Mathf.Max(1f, tuning.targetTimeSeconds);
            else
                timeLimitSeconds = fallbackTimeLimitSeconds_Level4;
        }
    }

    private void BeginAttemptIfPossible()
    {
        if (!saveAttemptOnExit) return;
        if (GameSessionManager.I == null || GameSessionManager.I.profile == null) return;

        float limit = (currentLevel == SpanishCultureQuizLevel.Level4) ? timeLimitSeconds : 0f;
        GameSessionManager.I.BeginAttempt(limit);
        attemptStarted = true;
        attemptEnded = false;
    }

    private void EndAttemptIfStarted(bool completed)
    {
        if (!attemptStarted || attemptEnded) return;
        if (GameSessionManager.I == null) return;

        GameSessionManager.I.EndAttempt(completed);
        attemptEnded = true;
    }

    // =========================================================
    //  SETUP
    // =========================================================
    private void SetupLevelConfig()
    {
        switch (currentLevel)
        {
            case SpanishCultureQuizLevel.Level1:
                optionsCount = 2;
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level1);
                break;

            case SpanishCultureQuizLevel.Level2:
                optionsCount = 3;
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level2);
                break;

            case SpanishCultureQuizLevel.Level3:
                optionsCount = 4;
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level3);
                break;

            case SpanishCultureQuizLevel.Level4:
                optionsCount = 4;
                useChronometer = false;
                useTimeBar = true;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level4);
                break;
        }

        correctRoundsThisAttempt = 0;
        roundsPlayedThisAttempt = 0;
        failedAnyRoundThisAttempt = false;
    }

    private void SetupUI()
    {
        if (textMinijuegoTitulo != null)
            textMinijuegoTitulo.text = "Cultura general española";

        if (textNivelActual != null)
            textNivelActual.text = $"NIVEL {(int)currentLevel}";

        if (textFeedback != null)
            textFeedback.text = "";

        if (textPregunta != null)
            textPregunta.text = "";

        if (imagePregunta != null)
        {
            imagePregunta.sprite = null;
            imagePregunta.enabled = false;
        }

        if (imageFeedbackFlash != null)
        {
            Color c = imageFeedbackFlash.color;
            c.a = 0f;
            imageFeedbackFlash.color = c;
        }

        if (chronometerContainer != null)
            chronometerContainer.SetActive(false);

        if (textChronometer != null)
            textChronometer.text = "00:00";

        if (timeBarContainer != null)
            timeBarContainer.SetActive(false);

        if (timeBarFillImage != null)
        {
            timeBarFillImage.fillAmount = 1f;
            timeBarFillImage.color = Color.white;
        }

        HideAllAnswerContainers();
        UpdateRoundsRemainingUI();
    }

    private void BindAllAnswerButtons()
    {
        foreach (var btn in answerButtons2)
            if (btn != null) btn.Bind(this);

        foreach (var btn in answerButtons3)
            if (btn != null) btn.Bind(this);

        foreach (var btn in answerButtons4)
            if (btn != null) btn.Bind(this);
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

        if (level4StartWarningText != null)
            level4StartWarningText.gameObject.SetActive(currentLevel == SpanishCultureQuizLevel.Level4);
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
        UpdateChronometerUI();

        BeginAttemptIfPossible();

        if (useChronometer)
        {
            chronometerRunning = true;
            if (chronometerContainer != null)
                chronometerContainer.SetActive(true);
        }

        if (useTimeBar)
        {
            if (timeBarContainer != null)
                timeBarContainer.SetActive(true);
        }

        GenerateNewRound();

        if (useTimeBar)
            StartTimeBar();
    }

    private void UpdateRoundsRemainingUI()
    {
        if (roundsRemainingText == null) return;

        int currentRoundDisplay = Mathf.Clamp(roundsPlayedThisAttempt + 1, 1, goalCorrectRounds);
        roundsRemainingText.text = $"{currentRoundDisplay}/{goalCorrectRounds}";
    }

    // =========================================================
    //  RONDA
    // =========================================================
    private void GenerateNewRound()
    {
        roundResolved = false;
        waitingForAnswer = false;
        incorrectClicksThisRound = 0;
        correctAnswerButton = null;
        currentQuestion = null;
        currentRoundAnswers.Clear();
        currentRoundCorrectIndex = -1;

        if (textFeedback != null)
            textFeedback.text = "";

        HideAllAnswerContainers();
        ActivateContainerForCurrentLevel();

        List<SpanishCultureCategorySO.SpanishCultureQuestionData> validQuestions = BuildValidQuestionsPool(optionsCount);

        if (validQuestions.Count == 0)
        {
            Debug.LogError($"[SpanishCultureQuiz] No hay preguntas válidas con {optionsCount} opciones en los ScriptableObjects asignados.");
            return;
        }

        currentQuestion = validQuestions[Random.Range(0, validQuestions.Count)];

        ApplyQuestionToUI(currentQuestion);
        BuildRoundAnswers(currentQuestion, optionsCount);
        ApplyAnswersToButtons();

        waitingForAnswer = true;
    }

    private List<SpanishCultureCategorySO.SpanishCultureQuestionData> BuildValidQuestionsPool(int requiredOptionsCount)
    {
        List<SpanishCultureCategorySO.SpanishCultureQuestionData> result = new List<SpanishCultureCategorySO.SpanishCultureQuestionData>();

        if (questionCategories == null)
            return result;

        for (int i = 0; i < questionCategories.Length; i++)
        {
            SpanishCultureCategorySO category = questionCategories[i];
            if (category == null || category.Questions == null) continue;

            for (int j = 0; j < category.Questions.Count; j++)
            {
                SpanishCultureCategorySO.SpanishCultureQuestionData question = category.Questions[j];
                if (question == null) continue;
                if (!question.IsValidForOptionsCount(requiredOptionsCount)) continue;

                result.Add(question);
            }
        }

        return result;
    }

    private void ApplyQuestionToUI(SpanishCultureCategorySO.SpanishCultureQuestionData question)
    {
        if (question == null) return;

        if (textPregunta != null)
            textPregunta.text = question.QuestionText;

        if (imagePregunta != null)
        {
            imagePregunta.sprite = question.QuestionImage;
            imagePregunta.enabled = question.QuestionImage != null;
        }
    }

    private void ApplyAnswersToButtons()
    {
        List<SpanishCultureAnswerButtonController> buttons = GetCurrentButtonsList();
        if (buttons == null) return;

        for (int i = 0; i < buttons.Count; i++)
        {
            SpanishCultureAnswerButtonController btn = buttons[i];
            if (btn == null) continue;

            bool shouldBeActive = (i < currentRoundAnswers.Count);
            btn.gameObject.SetActive(shouldBeActive);

            if (!shouldBeActive) continue;

            bool isCorrect = (i == currentRoundCorrectIndex);
            btn.Setup(currentRoundAnswers[i], isCorrect);

            if (isCorrect)
                correctAnswerButton = btn;
        }
    }

    public void OnAnswerSelected(SpanishCultureAnswerButtonController button)
    {
        if (button == null) return;
        if (isPaused) return;
        if (attemptEnded) return;
        if (!waitingForAnswer) return;
        if (roundResolved) return;

        if (button.IsCorrect())
        {
            HandleCorrectAnswer();
            return;
        }

        HandleIncorrectAnswer(button);
    }

    private void HandleCorrectAnswer()
    {
        roundResolved = true;
        waitingForAnswer = false;

        if (GameSessionManager.I != null)
        {
            bool firstTry = (incorrectClicksThisRound == 0);
            GameSessionManager.I.AddCorrect(firstTry);
        }

        correctRoundsThisAttempt++;
        roundsPlayedThisAttempt++;
        UpdateRoundsRemainingUI();

        ShowFeedback("¡Correcto!", true);

        bool noMoreRounds = roundsPlayedThisAttempt >= goalCorrectRounds;
        if (noMoreRounds)
        {
            if (failedAnyRoundThisAttempt)
                StartCoroutine(FinalResultsCoroutine(false, "Fin de rondas"));
            else
                StartCoroutine(FinalResultsCoroutine(true, "¡Nivel completado!"));
        }
        else
        {
            StartCoroutine(NextRoundCoroutine());
        }
    }

    private void HandleIncorrectAnswer(SpanishCultureAnswerButtonController button)
    {
        incorrectClicksThisRound++;
        GameSessionManager.I?.AddError();

        button.MarkWrongAndDisable();

        if (currentLevel == SpanishCultureQuizLevel.Level4)
        {
            roundResolved = true;
            waitingForAnswer = false;
            roundsPlayedThisAttempt++;
            UpdateRoundsRemainingUI();
            failedAnyRoundThisAttempt = true;

            chronometerRunning = false;
            timeBarRunning = false;

            ShowFeedback("Respuesta incorrecta", false);
            StartCoroutine(FinalResultsCoroutine(false, "Respuesta incorrecta"));
            return;
        }

        int wrongButtonsAvailable = Mathf.Max(0, optionsCount - 1);
        bool allIncorrectButtonsPressed = incorrectClicksThisRound >= wrongButtonsAvailable;

        if (!allIncorrectButtonsPressed)
        {
            ShowFeedback("Esa no es. Sigue probando.", false);
            return;
        }

        roundResolved = true;
        waitingForAnswer = false;
        failedAnyRoundThisAttempt = true;
        roundsPlayedThisAttempt++;
        UpdateRoundsRemainingUI();

        bool wasLastRequiredRound = (roundsPlayedThisAttempt >= goalCorrectRounds);

        if (wasLastRequiredRound)
            StartCoroutine(RoundLostByDiscardCoroutine(true));
        else
            StartCoroutine(RoundLostByDiscardCoroutine(false));
    }

    private IEnumerator RoundLostByDiscardCoroutine(bool goToResultsAfter)
    {
        ShowFeedback("Esta era la correcta", false);

        if (correctAnswerButton != null)
            yield return StartCoroutine(BlinkCorrectAnswerCoroutine(correctAnswerButton));
        else
            yield return new WaitForSeconds(1.0f);

        if (goToResultsAfter)
        {
            chronometerRunning = false;
            timeBarRunning = false;
            EndAttemptIfStarted(false);
            GoToResults();
        }
        else
        {
            GenerateNewRound();
            UpdateRoundsRemainingUI();

            if (useTimeBar)
                StartTimeBar();
        }
    }

    private IEnumerator BlinkCorrectAnswerCoroutine(SpanishCultureAnswerButtonController button)
    {
        if (button == null) yield break;

        Color green = new Color(0.2f, 1f, 0.2f, 1f);
        const int blinkCount = 3;
        const float stepDuration = 0.18f;

        for (int i = 0; i < blinkCount; i++)
        {
            button.SetBackgroundColor(green);
            yield return new WaitForSeconds(stepDuration);
            button.RestoreDefaultColor();
            yield return new WaitForSeconds(stepDuration);
        }
    }

    private IEnumerator NextRoundCoroutine()
    {
        yield return new WaitForSeconds(1f);

        GenerateNewRound();
        UpdateRoundsRemainingUI();

        if (useTimeBar)
            StartTimeBar();
    }

    private IEnumerator FinalResultsCoroutine(bool completed, string feedbackMessage)
    {
        chronometerRunning = false;
        timeBarRunning = false;

        ShowFeedback(feedbackMessage, completed);

        yield return new WaitForSeconds(1.0f);

        EndAttemptIfStarted(completed);
        GoToResults();
    }

    // =========================================================
    //  CRONÓMETRO
    // =========================================================
    private void UpdateChronometerUI()
    {
        if (textChronometer == null) return;

        int totalSeconds = Mathf.FloorToInt(chronometerTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        textChronometer.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    // =========================================================
    //  BARRA TIEMPO (Nivel 4)
    // =========================================================
    private void StartTimeBar()
    {
        currentTime = timeLimitSeconds;
        timeBarRunning = true;
        UpdateTimeBarUI();
    }

    private void UpdateTimeBarUI()
    {
        if (timeBarFillImage == null) return;
        if (timeLimitSeconds <= 0f) return;

        float ratio = Mathf.Clamp01(currentTime / timeLimitSeconds);
        timeBarFillImage.fillAmount = ratio;

        if (ratio > 0.6f) timeBarFillImage.color = Color.white;
        else if (ratio > 0.3f) timeBarFillImage.color = Color.yellow;
        else timeBarFillImage.color = Color.red;
    }

    private void OnTimeExpired()
    {
        if (attemptEnded) return;
        if (roundResolved) return;

        roundResolved = true;
        waitingForAnswer = false;

        GameSessionManager.I?.AddError();

        failedAnyRoundThisAttempt = true;
        roundsPlayedThisAttempt++;
        UpdateRoundsRemainingUI();

        chronometerRunning = false;
        timeBarRunning = false;

        ShowFeedback("Se acabó el tiempo", false);
        StartCoroutine(TimeOutCoroutine());
    }

    private IEnumerator TimeOutCoroutine()
    {
        yield return new WaitForSeconds(1.0f);

        EndAttemptIfStarted(false);
        GoToResults();
    }

    // =========================================================
    //  FEEDBACK
    // =========================================================
    private void ShowFeedback(string message, bool isCorrect)
    {
        if (textFeedback != null)
            textFeedback.text = message;

        if (imageFeedbackFlash != null)
        {
            Color c = isCorrect ? Color.green : Color.red;
            float maxAlpha = hasAuditoryDifficulty ? 0.7f : 0.4f;
            StartCoroutine(FlashImageCoroutine(c, maxAlpha));
        }
    }

    private IEnumerator FlashImageCoroutine(Color color, float maxAlpha)
    {
        SetFeedbackFlashColor(color, 0f);

        float t = 0f;
        float fadeIn = 0.08f;
        float hold = 0.08f;
        float fadeOut = 0.18f;

        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(0f, maxAlpha, t / fadeIn);
            SetFeedbackFlashColor(color, a);
            yield return null;
        }

        SetFeedbackFlashColor(color, maxAlpha);
        yield return new WaitForSecondsRealtime(hold);

        t = 0f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(maxAlpha, 0f, t / fadeOut);
            SetFeedbackFlashColor(color, a);
            yield return null;
        }

        SetFeedbackFlashColor(color, 0f);
    }

    private void SetFeedbackFlashColor(Color baseColor, float alpha)
    {
        if (imageFeedbackFlash == null) return;

        Color c = baseColor;
        c.a = Mathf.Clamp01(alpha);
        imageFeedbackFlash.color = c;
    }

    // =========================================================
    //  HELPERS UI RESPUESTAS
    // =========================================================
    private void HideAllAnswerContainers()
    {
        if (answersContainer2Options != null) answersContainer2Options.SetActive(false);
        if (answersContainer3Options != null) answersContainer3Options.SetActive(false);
        if (answersContainer4Options != null) answersContainer4Options.SetActive(false);
    }

    private void ActivateContainerForCurrentLevel()
    {
        switch (optionsCount)
        {
            case 2:
                if (answersContainer2Options != null) answersContainer2Options.SetActive(true);
                break;

            case 3:
                if (answersContainer3Options != null) answersContainer3Options.SetActive(true);
                break;

            case 4:
                if (answersContainer4Options != null) answersContainer4Options.SetActive(true);
                break;
        }
    }

    private List<SpanishCultureAnswerButtonController> GetCurrentButtonsList()
    {
        switch (optionsCount)
        {
            case 2: return answerButtons2;
            case 3: return answerButtons3;
            case 4: return answerButtons4;
        }

        return null;
    }

    private void BuildRoundAnswers(SpanishCultureCategorySO.SpanishCultureQuestionData question, int requiredOptions)
    {
        currentRoundAnswers.Clear();
        currentRoundCorrectIndex = -1;

        if (question == null || question.Answers == null || question.Answers.Count < requiredOptions)
            return;

        List<int> availableWrongIndices = new List<int>();

        for (int i = 0; i < question.Answers.Count; i++)
        {
            if (i != question.CorrectAnswerIndex)
                availableWrongIndices.Add(i);
        }

        ShuffleIntList(availableWrongIndices);

        List<int> selectedIndices = new List<int>();
        selectedIndices.Add(question.CorrectAnswerIndex);

        int neededWrongAnswers = requiredOptions - 1;
        for (int i = 0; i < neededWrongAnswers && i < availableWrongIndices.Count; i++)
        {
            selectedIndices.Add(availableWrongIndices[i]);
        }

        ShuffleIntList(selectedIndices);

        for (int i = 0; i < selectedIndices.Count; i++)
        {
            int sourceIndex = selectedIndices[i];
            currentRoundAnswers.Add(question.Answers[sourceIndex]);

            if (sourceIndex == question.CorrectAnswerIndex)
                currentRoundCorrectIndex = i;
        }
    }

    private void ShuffleIntList(List<int> list)
    {
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);
            int temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    // =========================================================
    //  PAUSA
    // =========================================================
    public void PauseGame()
    {
        if (isPaused) return;

        isPaused = true;
        Time.timeScale = 0f;

        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    public void ExitToHub()
    {
        if (saveAttemptOnExit && attemptStarted && !attemptEnded)
            EndAttemptIfStarted(false);

        Time.timeScale = 1f;
        isPaused = false;

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
        else
            Debug.LogError("[SpanishCultureQuiz] hubSceneName no está configurado.");
    }

    // =========================================================
    //  RESULTS
    // =========================================================
    private void GoToResults()
    {
        Time.timeScale = 1f;
        isPaused = false;
        SceneManager.LoadScene(resultsSceneName);
    }
}
