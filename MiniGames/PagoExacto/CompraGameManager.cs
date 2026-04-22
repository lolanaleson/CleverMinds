using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// PagoExacto (Bartolo va a la compra) integrado en la arquitectura CleverMinds.
/// Plantilla de integración: EncajaLaLlaveGameManager.cs
///
/// - Lee nivel + accesibilidad desde GameSessionManager (o fallback en test).
/// - Abre/cierra AttemptMetrics con BeginAttempt/EndAttempt.
/// - Métricas:
///   * errors: cuando intenta pasarse (overpay)
///   * correct / firstTryCorrect: por ronda pagada exacta
///   * paymentItemsUsed: monedas/billetes aceptados (pulsaciones válidas)
/// - Se completa el nivel cuando se alcanzan X rondas correctas (configurable por inspector).
/// - En nivel 4 (si hay barra de tiempo), el timeout cierra el intento como no completado.
/// </summary>
public class BartoloCompraGameManager : MonoBehaviour
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

    [Tooltip("Nivel 4: tiempo límite (si está activado, usa barra de tiempo).")]
    [SerializeField] private bool level4UseTimeLimit = true;

    [Header("Objetivo (nº de rondas correctas para completar el nivel)")]
    [SerializeField] private int goalCorrectRounds_Level1 = 3;
    [SerializeField] private int goalCorrectRounds_Level2 = 4;
    [SerializeField] private int goalCorrectRounds_Level3 = 6;
    [SerializeField] private int goalCorrectRounds_Level4 = 6;
    private int goalCorrectRounds = 3;

    private int correctRoundsThisAttempt = 0;
    private int roundErrorsThisRound = 0;
    private bool attemptEnded = false;

    // =========================================================
    //  DATOS DEL JUGADOR (ACCESIBILIDAD)
    // =========================================================
    [Header("Singleton / Datos del jugador")]
    [SerializeField] private bool hasVisualDifficulty = false;
    [SerializeField] private bool hasAuditoryDifficulty = false;

    // =========================================================
    //  UI GENERAL
    // =========================================================
    [Header("UI General")]
    [SerializeField] private TextMeshProUGUI textNivelActual;

    [Tooltip("DISPLAY de caja registradora. MUESTRA LO QUE QUEDA POR PAGAR.")]
    [SerializeField] private TextMeshProUGUI textCajaDisplay;

    [Tooltip("Texto opcional: lo que lleva pagado (NO es el display).")]
    [SerializeField] private TextMeshProUGUI textPagado;

    [Header("UI Feedback")]
    [SerializeField] private TextMeshProUGUI textFeedback;
    [SerializeField] private Image imageFlash;

    [Header("UI Rondas (solo número, opcional)")]
    [SerializeField] private TextMeshProUGUI roundsRemainingText;

    [Header("Botón Reiniciar pago")]
    [SerializeField] private Button reiniciarPagoButton;

    // =========================================================
    //  CONTENEDORES (NO prefabs)
    // =========================================================
    [Header("Money Containers (ya montados en escena)")]
    [SerializeField] private GameObject moneyContainerLevel1;
    [SerializeField] private GameObject moneyContainerLevel234;

    [Header("Buttons (arrastra aquí TODOS los botones del container)")]
    [Tooltip("Nivel 1: 4 botones (1€, 2€, 5€, 10€).")]
    [SerializeField] private List<MoneyButtonController> level1Buttons = new List<MoneyButtonController>();

    [Tooltip("Nivel 2-3-4: todos los botones (monedas y billetes reales).")]
    [SerializeField] private List<MoneyButtonController> level234Buttons = new List<MoneyButtonController>();

    // =========================================================
    //  TIMERS
    // =========================================================
    [Header("UI Cronómetro (niveles 1-3)")]
    [SerializeField] private GameObject chronometerContainer;
    [SerializeField] private TextMeshProUGUI textChronometer;

    [Header("UI Barra de tiempo (nivel 4)")]
    [SerializeField] private GameObject timeBarContainer;
    [SerializeField] private Image timeBarFillImage;
    [SerializeField] private float timeLimitSeconds = 25f;

    // =========================================================
    //  PAUSA / HUB
    // =========================================================
    [Header("UI Pausa")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private string hubSceneName = "03_MinigameHub";
    [SerializeField] private string resultsScene = "05_Results";

    // =========================================================
    //  ESTADO INTERNO (TODO EN CÉNTIMOS)
    // =========================================================
    private int targetCents = 0; // total compra (NO cambia durante la ronda)
    private int paidCents = 0;   // pagado

    private bool isPaused = false;

    // Timers
    private bool useChronometer = false;
    private float chronometerTime = 0f;
    private bool chronometerRunning = false;

    private bool useTimeBar = false;
    private float currentTime = 0f;
    private bool timeBarRunning = false;

    // =========================================================
    //  UNITY
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
        SetupMoneyUIByLevel();

        BeginAttemptInSystem();
        StartNewRound();
    }

    private void Update()
    {
        if (isPaused) return;

        if (GameSessionManager.I != null && GameSessionManager.I.currentAttempt != null && !attemptEnded)
            GameSessionManager.I.TickTime(Time.deltaTime);

        if (useChronometer && chronometerRunning)
        {
            chronometerTime += Time.deltaTime;
            UpdateChronometerUI();
        }

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
    }

    // =========================================================
    //  SINGLETON / PERFIL JUGADOR
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
    //  CONFIG NIVEL
    // =========================================================
    private void SetupLevelConfig()
    {
        useChronometer = false;
        useTimeBar = false;

        switch (currentLevel)
        {
            case LevelId.Level1:
            case LevelId.Level2:
            case LevelId.Level3:
                useChronometer = true;
                useTimeBar = false;
                break;

            case LevelId.Level4:
                useChronometer = false;
                useTimeBar = level4UseTimeLimit;
                break;
        }

        switch (currentLevel)
        {
            case LevelId.Level1: goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level1); break;
            case LevelId.Level2: goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level2); break;
            case LevelId.Level3: goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level3); break;
            case LevelId.Level4: goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level4); break;
        }
    }

    private void SetupUI()
    {
        if (textNivelActual != null)
            textNivelActual.text = $"NIVEL {(int)currentLevel}";

        if (textFeedback != null) textFeedback.text = "";

        if (imageFlash != null)
        {
            Color c = imageFlash.color;
            c.a = 0f;
            imageFlash.color = c;
        }

        if (chronometerContainer != null) chronometerContainer.SetActive(useChronometer);
        if (textChronometer != null && useChronometer) textChronometer.text = "00:00";

        if (timeBarContainer != null) timeBarContainer.SetActive(useTimeBar);
        if (timeBarFillImage != null && useTimeBar) timeBarFillImage.fillAmount = 1f;

        if (reiniciarPagoButton != null)
        {
            reiniciarPagoButton.onClick.RemoveAllListeners();
            reiniciarPagoButton.onClick.AddListener(ReiniciarPago);
        }

        UpdateRoundsRemainingUI();
    }

    // =========================================================
    //  CONTENEDORES + BOTONES
    // =========================================================
    private void SetupMoneyUIByLevel()
    {
        bool isLevel1 = currentLevel == LevelId.Level1;

        if (moneyContainerLevel1 != null) moneyContainerLevel1.SetActive(isLevel1);
        if (moneyContainerLevel234 != null) moneyContainerLevel234.SetActive(!isLevel1);

        if (isLevel1)
        {
            foreach (var btn in level1Buttons)
                if (btn != null) btn.Bind(this);

            ForceLevel1Denominations();
        }
        else
        {
            foreach (var btn in level234Buttons)
                if (btn != null) btn.Bind(this);
        }
    }

    private void ForceLevel1Denominations()
    {
        // Orden recomendado: 1€,2€,5€,10€
        if (level1Buttons.Count >= 1 && level1Buttons[0] != null) level1Buttons[0].SetDenominationCents(100);
        if (level1Buttons.Count >= 2 && level1Buttons[1] != null) level1Buttons[1].SetDenominationCents(200);
        if (level1Buttons.Count >= 3 && level1Buttons[2] != null) level1Buttons[2].SetDenominationCents(500);
        if (level1Buttons.Count >= 4 && level1Buttons[3] != null) level1Buttons[3].SetDenominationCents(1000);
    }

    // =========================================================
    //  RONDA
    // =========================================================
    private void StartNewRound()
    {
        if (attemptEnded) return;

        targetCents = GenerateTargetForCurrentLevel();
        paidCents = 0;
        roundErrorsThisRound = 0;

        UpdateCashRegisterUI();
        UpdatePaidUI();

        ResetTimers();
        StartTimersIfNeeded();

        if (textFeedback != null) textFeedback.text = "";
    }

    // =========================================================
    //  OBJETIVO
    // =========================================================
    private int GenerateTargetForCurrentLevel()
    {
        switch (currentLevel)
        {
            case LevelId.Level1:
                int euros = Random.Range(3, 31); // 3..30
                return euros * 100;

            case LevelId.Level2:
                int e2 = Random.Range(3, 91);
                int cents2 = GetRandomAllowedCentsForLevel2();
                return e2 * 100 + cents2;

            case LevelId.Level3:
            case LevelId.Level4:
                int e3 = Random.Range(1, 101);
                int c3 = Random.Range(0, 100);
                return e3 * 100 + c3;
        }

        return 100;
    }

    private int GetRandomAllowedCentsForLevel2()
    {
        int[] allowed = new int[] { 0, 1, 2, 5, 10, 20, 30, 40, 50, 60, 70, 80, 90 };
        return allowed[Random.Range(0, allowed.Length)];
    }

    // =========================================================
    //  INPUT DESDE BOTÓN
    // =========================================================
    public void OnMoneyPressed(int denomCents)
    {
        if (attemptEnded) return;
        if (isPaused) return;

        // Si te pasas, NO sumas (regla clave)
        if (paidCents + denomCents > targetCents)
        {
            ShowFeedback("Te has pasado", false);
            Flash(Color.red);

            roundErrorsThisRound++;
            GameSessionManager.I?.AddError();
            return;
        }

        // Sumas
        paidCents += denomCents;
        GameSessionManager.I?.AddPaymentItemUsed(1);

        UpdatePaidUI();
        UpdateCashRegisterUI();

        // ¿Pagado exacto?
        if (paidCents == targetCents)
        {
            ShowFeedback("¡Perfecto!", true);
            Flash(Color.green);

            correctRoundsThisAttempt++;
            UpdateRoundsRemainingUI();

            bool firstTryRound = (roundErrorsThisRound == 0);
            GameSessionManager.I?.AddCorrect(firstTryRound);

            if (correctRoundsThisAttempt >= goalCorrectRounds)
            {
                StartCoroutine(CompleteLevelAndExitCoroutine());
            }
            else
            {
                StartCoroutine(NextRoundCoroutine());
            }
        }
        else
        {
            if (textFeedback != null) textFeedback.text = "";
        }
    }

    public void ReiniciarPago()
    {
        if (attemptEnded) return;

        paidCents = 0;
        UpdatePaidUI();
        UpdateCashRegisterUI();

        ShowFeedback("Pago reiniciado", true);
        Flash(Color.white);
    }

    private IEnumerator NextRoundCoroutine()
    {
        yield return new WaitForSeconds(1f);
        StartNewRound();
    }

    private IEnumerator CompleteLevelAndExitCoroutine()
    {
        yield return new WaitForSeconds(1.0f);
        EndAttemptInSystem(completed: true);
        GoToResults();
    }

    // =========================================================
    //  DISPLAY CAJA = LO QUE QUEDA POR PAGAR
    // =========================================================
    private void UpdateCashRegisterUI()
    {
        if (textCajaDisplay == null) return;

        int remaining = Mathf.Max(0, targetCents - paidCents);
        textCajaDisplay.text = FormatCentsToEuroString(remaining);
    }

    private void UpdatePaidUI()
    {
        if (textPagado != null)
            textPagado.text = FormatCentsToEuroString(paidCents);
    }

    private string FormatCentsToEuroString(int cents)
    {
        int euros = cents / 100;
        int rem = Mathf.Abs(cents % 100);
        return $"{euros},{rem:00} €";
    }

    // =========================================================
    //  FEEDBACK + FLASH
    // =========================================================
    private void ShowFeedback(string message, bool isPositive)
    {
        if (textFeedback == null) return;
        textFeedback.text = message;
    }

    private void Flash(Color baseColor)
    {
        if (imageFlash == null) return;

        float maxAlpha = hasAuditoryDifficulty ? 0.75f : 0.45f;
        StartCoroutine(FlashCoroutine(baseColor, maxAlpha));
    }

    private IEnumerator FlashCoroutine(Color baseColor, float maxAlpha)
    {
        float t = 0f;
        float duration = 0.15f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(0f, maxAlpha, t / duration);
            SetFlashColor(baseColor, a);
            yield return null;
        }

        t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(maxAlpha, 0f, t / duration);
            SetFlashColor(baseColor, a);
            yield return null;
        }

        SetFlashColor(baseColor, 0f);
    }

    private void SetFlashColor(Color baseColor, float alpha)
    {
        if (imageFlash == null) return;
        Color c = baseColor;
        c.a = alpha;
        imageFlash.color = c;
    }

    // =========================================================
    //  TIMERS
    // =========================================================
    private void ResetTimers()
    {
        if (useChronometer)
        {
            chronometerRunning = false;
            chronometerTime = 0f;
            UpdateChronometerUI();
        }

        if (useTimeBar)
        {
            timeBarRunning = false;
            currentTime = timeLimitSeconds;
            UpdateTimeBarUI();
        }
    }

    private void StartTimersIfNeeded()
    {
        if (useChronometer)
        {
            chronometerTime = 0f;
            chronometerRunning = true;
            UpdateChronometerUI();
        }

        if (useTimeBar)
        {
            StartTimeBar();
        }
    }

    private void UpdateChronometerUI()
    {
        if (textChronometer == null) return;

        int totalSeconds = Mathf.FloorToInt(chronometerTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        textChronometer.text = $"{minutes:00}:{seconds:00}";
    }

    private void StartTimeBar()
    {
        currentTime = timeLimitSeconds;
        timeBarRunning = true;
        UpdateTimeBarUI();
    }

    private void UpdateTimeBarUI()
    {
        if (timeBarFillImage == null) return;

        float ratio = (timeLimitSeconds > 0f) ? Mathf.Clamp01(currentTime / timeLimitSeconds) : 0f;
        timeBarFillImage.fillAmount = ratio;
    }

    // =========================================================
    //  UI RONDAS
    // =========================================================
    private void UpdateRoundsRemainingUI()
    {
        if (roundsRemainingText == null) return;
        int remaining = Mathf.Max(0, goalCorrectRounds - correctRoundsThisAttempt);
        roundsRemainingText.text = remaining.ToString();
    }

    // =========================================================
    //  TIEMPO AGOTADO (Nivel 4)
    // =========================================================
    private void OnTimeExpired()
    {
        if (attemptEnded) return;

        ShowFeedback("Se acabó el tiempo", false);
        Flash(Color.red);

        EndAttemptInSystem(completed: false);
        StartCoroutine(ExitAfterDelay(1.0f));
    }

    private IEnumerator ExitAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        GoToResults();
    }

    // =========================================================
    //  PAUSA / HUB
    // =========================================================
    public void PauseGame()
    {
        if (isPaused) return;

        isPaused = true;
        Time.timeScale = 0f;
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    public void ExitToHub()
    {
        EndAttemptInSystem(completed: false);

        Time.timeScale = 1f;
        isPaused = false;

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
        else
            Debug.LogError("[PagoExacto] hubSceneName no está configurado.");
    }

    // =========================================================
    //  INTEGRACIÓN CON GAMESESSIONMANAGER (intentos + guardado)
    // =========================================================
    private void BeginAttemptInSystem()
    {
        if (GameSessionManager.I == null) return;
        if (attemptEnded) return;

        float limit = (useTimeBar) ? Mathf.Max(0.1f, timeLimitSeconds) : 0f;
        GameSessionManager.I.BeginAttempt(limit);
    }

    private void EndAttemptInSystem(bool completed)
    {
        if (attemptEnded) return;
        if (GameSessionManager.I == null) return;
        if (GameSessionManager.I.currentAttempt == null) return;

        if (useChronometer)
        {
            GameSessionManager.I.currentAttempt.timeSeconds = chronometerTime;
        }
        else if (useTimeBar)
        {
            float elapsed = Mathf.Max(0f, timeLimitSeconds - currentTime);
            GameSessionManager.I.currentAttempt.timeSeconds = elapsed;
        }

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

