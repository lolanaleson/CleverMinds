using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class ClockTimeGameManager : MonoBehaviour
{
    // =========================================================
    //  CONFIGURACIÓN DE NIVEL
    // =========================================================
    [Header("Level Config")]
    // 🔥 Integración CleverMinds:
    // - En producción, el nivel se lee desde GameSessionManager.I.currentSelection.level
    // - En modo test, si no hay GameSessionManager, puedes forzar el nivel desde el inspector.
    [SerializeField] private LevelId fallbackLevelForTesting = LevelId.Level1;
    private LevelId currentLevel = LevelId.Level1;

    // Siempre 5 en 5
    private int minuteStep = 5;

    private bool useChronometer = false; // niveles 1-3
    private bool useTimeBar = false;     // nivel 4

    // Lógica de dificultad por nivel (instrucciones)
    private bool allowBackward = false;
    private bool allowHoursInstructions = false;

    // =========================================================
    //  OBJETIVO DEL NIVEL (rondas correctas para completar)
    //  (Mismo patrón que EncajaLaLlaveGameManager)
    // =========================================================
    [Header("Objetivo (nº de rondas correctas para completar el nivel)")]
    [SerializeField] private int goalCorrectRounds_Level1 = 3;
    [SerializeField] private int goalCorrectRounds_Level2 = 4;
    [SerializeField] private int goalCorrectRounds_Level3 = 6;
    [SerializeField] private int goalCorrectRounds_Level4 = 6;
    private int goalCorrectRounds = 3;

    private int correctRoundsThisAttempt = 0;

    // =========================================================
    //  MÉTRICAS (para el sistema)
    // =========================================================
    // Errores acumulados desde el último acierto (para firstTryCorrect)
    private int roundErrors = 0;
    private bool attemptEnded = false;

    // =========================================================
    //  DATOS DEL JUGADOR (ACCESIBILIDAD)
    // =========================================================
    [Header("Singleton / Datos del jugador")]
    [SerializeField] private bool hasVisualDifficulty = false;
    [SerializeField] private bool hasAuditoryDifficulty = false;

    // =========================================================
    //  REFERENCIAS A LA ESCENA
    // =========================================================
    [Header("Clock Root")]
    [SerializeField] private RectTransform clockRoot;
    public RectTransform ClockRoot => clockRoot;

    [Header("Hands (Actual)")]
    [SerializeField] private RectTransform hourHand;
    [SerializeField] private RectTransform minuteHand;

    [Header("Ghost Hands (Hora inicial)")]
    [SerializeField] private RectTransform ghostHourHand;
    [SerializeField] private RectTransform ghostMinuteHand;

    [Header("Draggables")]
    [SerializeField] private ClockHandDraggable hourDraggable;
    [SerializeField] private ClockHandDraggable minuteDraggable;

    [Header("UI General")]
    [SerializeField] private TextMeshProUGUI textMinijuegoTitulo;
    [SerializeField] private TextMeshProUGUI textNivelActual;
    [SerializeField] private TextMeshProUGUI textInstruction;
    [SerializeField] private TextMeshProUGUI textFeedback;

    // ✅ NUEVO: contador de rondas restantes (solo número, como Llaves)
    [Header("UI Rondas (solo número)")]
    [SerializeField] private TextMeshProUGUI roundsRemainingText;

    [Header("UI Feedback Flash")]
    [SerializeField] private Image imageFeedbackFlash;

    [Header("Botones")]
    [SerializeField] private Button resetButton;

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
    //  TIEMPOS
    // =========================================================
    [Header("Timing (Barra de tiempo - Nivel 4)")]
    [SerializeField] private float timeLimitSeconds = 15f;
    private float currentTime = 0f;
    private bool timeBarRunning = false;

    [Header("Timing (Cronómetro - Niveles 1-3)")]
    private float chronometerTime = 0f;
    private bool chronometerRunning = false;

    // =========================================================
    //  ESTADO / PAUSA
    // =========================================================
    private bool isPaused = false;
    public bool IsPaused => isPaused;

    // =========================================================
    //  TIEMPO DEL RELOJ (minutos totales 12h)
    // =========================================================
    // 0..719 (12h * 60)
    private int startTotalMinutes = 0;
    private int targetTotalMinutes = 0;

    // Esto es lo que “quiere” el drag (ya cuantizado a 5)
    private int desiredTotalMinutes = 0;

    // Esto es lo que se está mostrando (para suavizado visual)
    private float displayedHourAngle = 0f;
    private float displayedMinuteAngle = 0f;

    // Suavizado (para que no “teletransporte” entre slots)
    [Header("Smoothing (para drag 5 en 5 sin saltos)")]
    [SerializeField] private float smoothTime = 0.06f;
    private float velHour = 0f;
    private float velMinute = 0f;

    // Para saber qué aguja se arrastra
    private ClockHandDraggable.HandType currentDragging = ClockHandDraggable.HandType.Minute;
    private bool isDragging = false;

    // ✅ FIX: seguimiento del minutero durante drag para detectar cruce de hora
    private int lastDraggedMinuteSnapped = -1;

    // =========================================================
    //  CICLO DE VIDA
    // =========================================================
    private void Awake()
    {
        LoadFromGameSessionManagerOrFallback();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        isPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);

        SetupLevelConfig();
        SetupUI();
        HookButtons();
        HookDraggables();

        // ✅ contador inicial (rondas restantes)
        UpdateRoundsRemainingUI();

        StartNewRound();

        if (useChronometer) StartChronometer();
        if (useTimeBar) StartTimeBar();

        BeginAttemptInSystem();
    }

    private void Update()
    {
        if (isPaused) return;

        // ✅ Integración: tick de tiempo del intento (tiempo total de sesión)
        if (GameSessionManager.I != null && GameSessionManager.I.currentAttempt != null && !attemptEnded)
            GameSessionManager.I.TickTime(Time.deltaTime);

        // Cronómetro ascendente (1-3)
        if (useChronometer && chronometerRunning)
        {
            chronometerTime += Time.deltaTime;
            UpdateChronometerUI();
        }

        // Barra de tiempo descendente (4)
        if (useTimeBar && timeBarRunning)
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

        // Suavizado visual hacia el tiempo deseado (5 en 5)
        ApplyDesiredTimeSmooth();
    }

    // =========================================================
    //  SINGLETON / PERFIL JUGADOR (arquitectura CleverMinds)
    // =========================================================
    private void LoadFromGameSessionManagerOrFallback()
    {
        if (GameSessionManager.I != null && GameSessionManager.I.profile != null)
        {
            var profile = GameSessionManager.I.profile;
            var selection = GameSessionManager.I.currentSelection;

            hasVisualDifficulty = profile.hasVisionIssues;
            hasAuditoryDifficulty = profile.hasHearingIssues;
            currentLevel = selection.level;
        }
        else
        {
            currentLevel = fallbackLevelForTesting;
        }
    }

    // =========================================================
    //  CONFIGURAR NIVEL
    // =========================================================
    private void SetupLevelConfig()
    {
        minuteStep = 5;

        // Reset de progreso del intento (rondas)
        correctRoundsThisAttempt = 0;
        roundErrors = 0;
        attemptEnded = false;

        switch (currentLevel)
        {
            case LevelId.Level1:
                useChronometer = true;
                useTimeBar = false;
                allowBackward = false;
                allowHoursInstructions = false;
                goalCorrectRounds = goalCorrectRounds_Level1;
                break;

            case LevelId.Level2:
                useChronometer = true;
                useTimeBar = false;
                allowBackward = true;
                allowHoursInstructions = false;
                goalCorrectRounds = goalCorrectRounds_Level2;
                break;

            case LevelId.Level3:
                useChronometer = true;
                useTimeBar = false;
                allowBackward = true;
                allowHoursInstructions = true;
                goalCorrectRounds = goalCorrectRounds_Level3;
                break;

            case LevelId.Level4:
                useChronometer = false;
                useTimeBar = true;
                allowBackward = true;
                allowHoursInstructions = true;
                goalCorrectRounds = goalCorrectRounds_Level4;
                break;
        }

        // ✅ Integración: permitir tunear el límite de tiempo desde MiniGameConfig (nivel 4)
        if (useTimeBar && GameSessionManager.I != null)
        {
            var tuning = GameSessionManager.I.GetTuning();
            if (tuning != null && tuning.targetTimeSeconds > 0.1f)
                timeLimitSeconds = tuning.targetTimeSeconds;
        }
    }

    private void SetupUI()
    {
        if (imageFeedbackFlash != null)
        {
            Color c = imageFeedbackFlash.color;
            c.a = 0f;
            imageFeedbackFlash.color = c;
        }

        if (textMinijuegoTitulo != null) textMinijuegoTitulo.text = "Pon el reloj en hora";
        if (textNivelActual != null) textNivelActual.text = $"NIVEL {(int)currentLevel}";
        if (textFeedback != null) textFeedback.text = "";

        if (chronometerContainer != null) chronometerContainer.SetActive(useChronometer);
        if (timeBarContainer != null) timeBarContainer.SetActive(useTimeBar);

        if (textChronometer != null && useChronometer) textChronometer.text = "00:00";

        if (timeBarFillImage != null && useTimeBar)
        {
            timeBarFillImage.fillAmount = 1f;
            timeBarFillImage.color = Color.white;
        }

        if (ghostHourHand != null) ghostHourHand.gameObject.SetActive(true);
        if (ghostMinuteHand != null) ghostMinuteHand.gameObject.SetActive(true);
    }

    private void HookButtons()
    {
        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(ResetToStartTime);
        }
    }

    private void HookDraggables()
    {
        if (hourDraggable != null) hourDraggable.SetManager(this);
        if (minuteDraggable != null) minuteDraggable.SetManager(this);
    }

    // =========================================================
    //  UI RONDAS (solo número)
    // =========================================================
    private void UpdateRoundsRemainingUI()
    {
        if (roundsRemainingText == null) return;
        int remaining = Mathf.Max(0, goalCorrectRounds - correctRoundsThisAttempt);
        roundsRemainingText.text = remaining.ToString();
    }

    // =========================================================
    //  RONDAS
    // =========================================================
    private void StartNewRound()
    {
        startTotalMinutes = GenerateStartTimeMultipleOf5();
        desiredTotalMinutes = startTotalMinutes;

        int delta = GenerateInstructionDeltaMinutes(out string instructionText);
        targetTotalMinutes = Wrap12h(startTotalMinutes + delta);

        if (textInstruction != null) textInstruction.text = instructionText;
        if (textFeedback != null) textFeedback.text = "";

        // Ghost = hora inicial
        ApplyTimeInstant(startTotalMinutes, ghostHourHand, ghostMinuteHand);

        // Inicializar ángulos visuales al estado inicial (para suavizado)
        SetDisplayedAnglesFromTotalMinutes(desiredTotalMinutes);
        ApplyDisplayedAnglesToHands();

        // Reset tracking de drag
        lastDraggedMinuteSnapped = -1;

        // Reset errores de ronda (firstTryCorrect)
        roundErrors = 0;

        if (useTimeBar) StartTimeBar();
    }

    private int GenerateStartTimeMultipleOf5()
    {
        int h = Random.Range(1, 13);     // 1..12
        int m = Random.Range(0, 12) * 5; // 0..55
        return Wrap12h((h % 12) * 60 + m);
    }

    private int GenerateInstructionDeltaMinutes(out string instructionText)
    {
        int sign = 1;
        if (allowBackward)
            sign = (Random.value < 0.5f) ? 1 : -1;

        // =========================
        // NIVEL 1 – Muy fácil
        // =========================
        if (currentLevel == LevelId.Level1)
        {
            int[] minuteOptions = { 5, 10, 15 };
            int minutes = minuteOptions[Random.Range(0, minuteOptions.Length)];

            instructionText = $"Adelanta {minutes} minutos";
            return minutes;
        }

        // =========================
        // NIVEL 2 – Fácil (minutos grandes)
        // =========================
        if (currentLevel == LevelId.Level2)
        {
            int[] minuteOptions = { 20, 30, 45, 50 };
            int minutes = minuteOptions[Random.Range(0, minuteOptions.Length)];

            instructionText = sign > 0
                ? $"Adelanta {minutes} minutos"
                : $"Atrasa {minutes} minutos";

            return sign * minutes;
        }

        // =========================
        // NIVEL 3 y 4 – Medio / Difícil
        // =========================
        int pattern = Random.Range(0, 3);

        // 🔹 HORAS PURAS
        if (pattern == 0)
        {
            int[] hourOptions = { 1, 2, 3, 4 };
            int hours = hourOptions[Random.Range(0, hourOptions.Length)];

            instructionText = sign > 0
                ? $"Adelanta {hours} hora{(hours > 1 ? "s" : "")}"
                : $"Atrasa {hours} hora{(hours > 1 ? "s" : "")}";

            return sign * hours * 60;
        }

        // 🔹 MINUTOS GRANDES
        if (pattern == 1)
        {
            int[] minuteOptions = { 30, 45, 50, 90, 120 };
            int minutes = minuteOptions[Random.Range(0, minuteOptions.Length)];

            instructionText = sign > 0
                ? $"Adelanta {minutes} minutos"
                : $"Atrasa {minutes} minutos";

            return sign * minutes;
        }

        // 🔹 HORAS + MINUTOS
        int[] comboOptions = { 90, 150 }; // 1h30, 2h30
        int comboMinutes = comboOptions[Random.Range(0, comboOptions.Length)];

        int h = comboMinutes / 60;
        int m = comboMinutes % 60;

        instructionText = sign > 0
            ? $"Adelanta {h} horas y {m} minutos"
            : $"Atrasa {h} horas y {m} minutos";

        return sign * comboMinutes;
    }

    // =========================================================
    //  DRAG CALLBACKS (desde ClockHandDraggable)
    // =========================================================
    public void NotifyDragStart(ClockHandDraggable.HandType handType)
    {
        isDragging = true;
        currentDragging = handType;

        if (handType == ClockHandDraggable.HandType.Minute)
            lastDraggedMinuteSnapped = desiredTotalMinutes % 60;
    }

    public void NotifyDragEnd(ClockHandDraggable.HandType handType)
    {
        isDragging = false;
        lastDraggedMinuteSnapped = -1;

        CheckAnswer();
    }

    public void OnHandDragged(ClockHandDraggable.HandType handType, float clockAngle)
    {
        if (isPaused) return;

        if (handType == ClockHandDraggable.HandType.Minute)
        {
            float minuteFloat = clockAngle / 6f;
            int minuteRaw = Mathf.RoundToInt(minuteFloat) % 60;
            int minuteSnapped = SnapToStep(minuteRaw, minuteStep) % 60;

            int currentHour = desiredTotalMinutes / 60; // 0..11

            if (lastDraggedMinuteSnapped >= 0)
            {
                if (lastDraggedMinuteSnapped >= 50 && minuteSnapped <= 10)
                    currentHour = (currentHour + 1) % 12;

                if (lastDraggedMinuteSnapped <= 10 && minuteSnapped >= 50)
                    currentHour = (currentHour - 1 + 12) % 12;
            }

            lastDraggedMinuteSnapped = minuteSnapped;

            desiredTotalMinutes = Wrap12h(currentHour * 60 + minuteSnapped);
        }
        else
        {
            int totalRaw = Mathf.RoundToInt(clockAngle * 2f); // 0.5° por minuto
            desiredTotalMinutes = Wrap12h(SnapToStep(totalRaw, minuteStep));
        }
    }

    private int SnapToStep(int value, int step)
    {
        if (step <= 1) return value;
        return Mathf.RoundToInt(value / (float)step) * step;
    }

    // =========================================================
    //  SUAVIZADO VISUAL
    // =========================================================
    private void ApplyDesiredTimeSmooth()
    {
        GetAnglesFromTotalMinutes(desiredTotalMinutes, out float targetHourAngle, out float targetMinuteAngle);

        displayedMinuteAngle = Mathf.SmoothDampAngle(displayedMinuteAngle, targetMinuteAngle, ref velMinute, smoothTime);
        displayedHourAngle = Mathf.SmoothDampAngle(displayedHourAngle, targetHourAngle, ref velHour, smoothTime);

        ApplyDisplayedAnglesToHands();
    }

    private void SetDisplayedAnglesFromTotalMinutes(int totalMinutes)
    {
        GetAnglesFromTotalMinutes(totalMinutes, out float hourA, out float minuteA);
        displayedHourAngle = hourA;
        displayedMinuteAngle = minuteA;
        velHour = 0f;
        velMinute = 0f;
    }

    private void GetAnglesFromTotalMinutes(int totalMinutes, out float hourAngle, out float minuteAngle)
    {
        int h = totalMinutes / 60; // 0..11
        int m = totalMinutes % 60;

        minuteAngle = m * 6f;
        hourAngle = (h * 30f) + (m * 0.5f);
    }

    private void ApplyDisplayedAnglesToHands()
    {
        if (minuteHand != null) minuteHand.localEulerAngles = new Vector3(0, 0, -displayedMinuteAngle);
        if (hourHand != null) hourHand.localEulerAngles = new Vector3(0, 0, -displayedHourAngle);
    }

    private void ApplyTimeInstant(int totalMinutes, RectTransform hour, RectTransform minute)
    {
        if (hour == null || minute == null) return;
        GetAnglesFromTotalMinutes(totalMinutes, out float hA, out float mA);
        minute.localEulerAngles = new Vector3(0, 0, -mA);
        hour.localEulerAngles = new Vector3(0, 0, -hA);
    }

    // =========================================================
    //  RESET
    // =========================================================
    private void ResetToStartTime()
    {
        desiredTotalMinutes = startTotalMinutes;
        SetDisplayedAnglesFromTotalMinutes(desiredTotalMinutes);
        ApplyDisplayedAnglesToHands();

        lastDraggedMinuteSnapped = -1;

        if (textFeedback != null) textFeedback.text = "";
    }

    // =========================================================
    //  CHECK RESULTADO
    // =========================================================
    private void CheckAnswer()
    {
        if (desiredTotalMinutes == targetTotalMinutes)
        {
            // ✅ Integración: métrica de acierto (a la primera o no)
            if (GameSessionManager.I != null)
                GameSessionManager.I.AddCorrect(firstTry: roundErrors == 0);

            roundErrors = 0;

            // ✅ NUEVO: sumar ronda correcta y actualizar UI
            correctRoundsThisAttempt++;
            UpdateRoundsRemainingUI();

            // ✅ Si hemos alcanzado el objetivo, completamos el nivel (como Llaves)
            if (correctRoundsThisAttempt >= goalCorrectRounds)
            {
                ShowFeedback("¡Nivel completado!", true);
                StartCoroutine(CompleteAndExitCoroutine());
                return;
            }

            // ✅ Feedback positivo al acertar
            ShowFeedback("¡Muy bien!", true);
            StartCoroutine(NextRoundCoroutine());
            return;
        }

        // ❌ Incorrecto
        if (GameSessionManager.I != null)
            GameSessionManager.I.AddError();

        roundErrors++;

        // Solo damos pista en Nivel 1 y 2
        if (currentLevel == LevelId.Level1 || currentLevel == LevelId.Level2)
        {
            string hint = GetHintForLevel1And2(desiredTotalMinutes, targetTotalMinutes);
            ShowFeedback(hint, false);
        }
        else
        {
            ShowFeedback("Casi. Prueba otra vez.", false);
        }
    }

    private IEnumerator CompleteAndExitCoroutine()
    {
        // Pequeña pausa para que se vea el feedback/flash
        yield return new WaitForSeconds(0.8f);

        // ✅ cerrar intento como completado
        EndAttemptInSystem(completed: true);

        // volver al hub
        Time.timeScale = 1f;
        isPaused = false;

        if (!string.IsNullOrEmpty(resultsSceneName))
            GoToResults();
        else
            Debug.LogError("[ClockTime] 'ResultsScene' no está configurado.");
    }

    private int GetShortestSignedDelta(int fromTotalMinutes, int toTotalMinutes)
    {
        fromTotalMinutes = Wrap12h(fromTotalMinutes);
        toTotalMinutes = Wrap12h(toTotalMinutes);

        int diff = toTotalMinutes - fromTotalMinutes;

        if (diff > 360) diff -= 720;
        if (diff < -360) diff += 720;

        return diff;
    }

    private string GetHintForLevel1And2(int current, int target)
    {
        int signedDelta = GetShortestSignedDelta(current, target);
        int absDelta = Mathf.Abs(signedDelta);

        absDelta = SnapToStep(absDelta, minuteStep);

        if (absDelta <= 0)
            return "Casi. Ajusta un poquito.";

        if (currentLevel == LevelId.Level1)
        {
            if (signedDelta > 0)
                return $"Te falta adelantar {absDelta} minutos.";
            else
                return $"Te falta atrasar {absDelta} minutos.";
        }

        if (signedDelta > 0)
            return $"Te falta adelantar {absDelta} minutos.";
        else
            return $"Te falta atrasar {absDelta} minutos.";
    }

    private IEnumerator NextRoundCoroutine()
    {
        yield return new WaitForSeconds(0.8f);
        StartNewRound();
    }

    private void ShowFeedback(string msg, bool isCorrect)
    {
        if (textFeedback != null) textFeedback.text = msg;

        // ✅ SOLO flash verde al acertar
        if (isCorrect && imageFeedbackFlash != null)
        {
            float maxAlpha = hasAuditoryDifficulty ? 0.7f : 0.4f;
            StartCoroutine(FlashGreenCorrectCoroutine(maxAlpha));
        }
    }

    private IEnumerator FlashGreenCorrectCoroutine(float maxAlpha)
    {
        float t = 0f;
        float duration = 0.25f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, maxAlpha, t / duration);
            SetFeedbackFlashColor(Color.green, alpha);
            yield return null;
        }

        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(maxAlpha, 0f, t / duration);
            SetFeedbackFlashColor(Color.green, alpha);
            yield return null;
        }

        SetFeedbackFlashColor(Color.green, 0f);
    }

    private void SetFeedbackFlashColor(Color baseColor, float alpha)
    {
        if (imageFeedbackFlash == null) return;
        Color c = baseColor;
        c.a = alpha;
        imageFeedbackFlash.color = c;
    }

    // =========================================================
    //  CRONÓMETRO (1-3)
    // =========================================================
    private void StartChronometer()
    {
        chronometerTime = 0f;
        chronometerRunning = true;
        UpdateChronometerUI();
    }

    private void UpdateChronometerUI()
    {
        if (textChronometer == null) return;

        int totalSeconds = Mathf.FloorToInt(chronometerTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        textChronometer.text = $"{minutes:00}:{seconds:00}";
    }

    // =========================================================
    //  BARRA TIEMPO (4)
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

        float ratio = Mathf.Clamp01(currentTime / timeLimitSeconds);
        timeBarFillImage.fillAmount = ratio;

        if (ratio > 0.6f) timeBarFillImage.color = Color.white;
        else if (ratio > 0.3f) timeBarFillImage.color = Color.yellow;
        else timeBarFillImage.color = Color.red;
    }

    private void OnTimeExpired()
    {
        ShowFeedback("Se acabó el tiempo. Recolocamos y seguimos.", false);
        ResetToStartTime();
        StartCoroutine(NextRoundCoroutine());
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
        // ✅ Integración: cerrar el intento al salir (no completado)
        EndAttemptInSystem(completed: false);

        Time.timeScale = 1f;
        isPaused = false;

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
        else
            Debug.LogError("[ClockTime] 'hubSceneName' no está configurado.");
    }

    // =========================================================
    //  INTEGRACIÓN CON GAMESESSIONMANAGER (intentos + guardado)
    // =========================================================
    private void BeginAttemptInSystem()
    {
        if (GameSessionManager.I == null) return;
        if (attemptEnded) return;

        float limit = useTimeBar ? Mathf.Max(0.1f, timeLimitSeconds) : 0f;
        GameSessionManager.I.BeginAttempt(limit);
    }

    private void EndAttemptInSystem(bool completed)
    {
        if (attemptEnded) return;
        if (GameSessionManager.I == null) return;
        if (GameSessionManager.I.currentAttempt == null) return;

        if (useChronometer)
            GameSessionManager.I.currentAttempt.timeSeconds = chronometerTime;

        GameSessionManager.I.EndAttempt(completed);
        attemptEnded = true;
    }

    // =========================================================
    //  UTILS
    // =========================================================
    private int Wrap12h(int totalMinutes)
    {
        totalMinutes %= 720;
        if (totalMinutes < 0) totalMinutes += 720;
        return totalMinutes;
    }

    private const string resultsSceneName = "05_Results";

    private void GoToResults()
    {
        Time.timeScale = 1f;
        isPaused = false;
        SceneManager.LoadScene(resultsSceneName);
    }
}
