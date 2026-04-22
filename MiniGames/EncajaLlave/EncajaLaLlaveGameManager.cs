using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class EncajaLaLlaveGameManager : MonoBehaviour
{
    // =========================================================
    //  CONFIGURACIÓN DE NIVEL
    // =========================================================
    [Header("Level Config")]
    [SerializeField] private LevelId fallbackLevelForTesting = LevelId.Level1;
    private LevelId currentLevel = LevelId.Level1;

    private int keysCount = 3;
    private bool useChronometer = false;   // niveles 1-3
    private bool useTimeBar = false;       // nivel 4

    // =========================================================
    //  DATOS DEL JUGADOR (ACCESIBILIDAD)
    // =========================================================
    [Header("Singleton / Datos del jugador")]
    [SerializeField] private bool hasVisualDifficulty = false;    // hasVisionIssues
    [SerializeField] private bool hasAuditoryDifficulty = false;  // hasHearingIssues

    // =========================================================
    //  OBJETIVO DEL NIVEL (para poder cerrar el intento)
    // =========================================================
    [Header("Objetivo (nº de aciertos para completar el nivel)")]
    [SerializeField] private int goalCorrectRounds_Level1 = 3;
    [SerializeField] private int goalCorrectRounds_Level2 = 4;
    [SerializeField] private int goalCorrectRounds_Level3 = 6;
    [SerializeField] private int goalCorrectRounds_Level4 = 6;
    private int goalCorrectRounds = 3;

    private int correctRoundsThisAttempt = 0;
    private int roundErrors = 0;

    private bool attemptEnded = false;

    // Ayuda de color
    private bool colorAidForcedByVision = false;
    private bool colorAidActivatedByButton = false;
    private bool usedOptionalColorAid = false;

    private bool IsColorAidActive => colorAidForcedByVision || colorAidActivatedByButton;

    // =========================================================
    //  REFERENCIAS A LA ESCENA
    // =========================================================
    [Header("Key Setup")]
    [SerializeField] private Transform keysContainer;
    [SerializeField] private GameObject keyPrefab;

    [Header("Key Sprites (formas de llave)")]
    [SerializeField] private Sprite[] keyShapes;

    [Header("Colores base (sin ayudas)")]
    [SerializeField] private Color[] baseKeyColors;

    [Header("Colores llamativos (ayudas)")]
    [SerializeField] private Color[] highlightColors;

    [Header("DropZone (silueta objetivo)")]
    [SerializeField] private DropZone dropZone;
    [SerializeField] private Image dropZoneImage;

    [Header("UI General")]
    [SerializeField] private TextMeshProUGUI textMinijuegoTitulo;
    [SerializeField] private TextMeshProUGUI textNivelActual;

    // ✅ NUEVO: contador de rondas restantes (solo número)
    [Header("UI Rondas (solo número)")]
    [SerializeField] private TextMeshProUGUI roundsRemainingText;

    [Header("UI Cronómetro (niveles 1-3)")]
    [SerializeField] private GameObject chronometerContainer;
    [SerializeField] private TextMeshProUGUI textChronometer;

    [Header("UI Barra de Tiempo (nivel 4)")]
    [SerializeField] private GameObject timeBarContainer;
    [SerializeField] private Image timeBarFillImage;

    [Header("UI Feedback")]
    [SerializeField] private TextMeshProUGUI textFeedback;
    [SerializeField] private Image imageFeedbackFlash;

    [Header("Botón de ayuda de color (FUTURO)")]
    [SerializeField] private Button colorAidButton;

    [Header("Barrido visual (imágenes de foco)")]
    [SerializeField] private Image sweepHighlightKeyImage;
    [SerializeField] private Image sweepHighlightTargetImage;

    [Header("UI Pausa")]
    [SerializeField] private GameObject pausePanel;

    [Header("Escena a la que volver al salir")]
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

    [Header("Barrido visual (duraciones)")]
    [SerializeField] private float sweepHighlightDuration = 0.5f;
    [SerializeField] private float sweepDelayBetweenKeys = 0.2f;

    // =========================================================
    //  ESTADO INTERNO
    // =========================================================
    private List<KeyDraggable> currentKeys = new List<KeyDraggable>();
    private KeyDraggable correctKey;

    private bool isPaused = false;
    public bool IsPaused => isPaused;

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
        if (pausePanel != null)
            pausePanel.SetActive(false);

        SetupLevelConfig();
        SetupUI();

        // ✅ NUEVO: pintar contador al inicio
        UpdateRoundsRemainingUI();

        SpawnKeysAndSetupDropZone();

        if (useChronometer) StartChronometer();
        if (useTimeBar) StartTimeBar();

        if (colorAidButton != null)
        {
            if (!hasVisualDifficulty)
            {
                // FUTURO: colorAidButton.onClick.AddListener(OnColorAidButtonClicked);
            }
        }

        BeginAttemptInSystem();
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

        colorAidForcedByVision = hasVisualDifficulty;
    }

    // =========================================================
    //  CONFIGURAR NIVEL
    // =========================================================
    private void SetupLevelConfig()
    {
        switch (currentLevel)
        {
            case LevelId.Level1:
                keysCount = 3;
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level1);
                break;

            case LevelId.Level2:
                keysCount = 4;
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level2);
                break;

            case LevelId.Level3:
                keysCount = 6;
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level3);
                break;

            case LevelId.Level4:
                keysCount = 6;
                useChronometer = false;
                useTimeBar = true;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level4);
                break;
        }

        if (useTimeBar && GameSessionManager.I != null)
        {
            var tuning = GameSessionManager.I.GetTuning();
            if (tuning != null && tuning.targetTimeSeconds > 0.1f)
                timeLimitSeconds = tuning.targetTimeSeconds;
        }

        correctRoundsThisAttempt = 0;
        roundErrors = 0;
        attemptEnded = false;
    }

    private void SetupUI()
    {
        if (textMinijuegoTitulo != null)
            textMinijuegoTitulo.text = "Encaja la llave";

        if (textNivelActual != null)
            textNivelActual.text = $"NIVEL {(int)currentLevel}";

        if (textFeedback != null)
            textFeedback.text = string.Empty;

        if (imageFeedbackFlash != null)
        {
            Color c = imageFeedbackFlash.color;
            c.a = 0f;
            imageFeedbackFlash.color = c;
        }

        if (chronometerContainer != null)
            chronometerContainer.SetActive(useChronometer);

        if (textChronometer != null && useChronometer)
            textChronometer.text = "00:00";

        if (timeBarContainer != null)
            timeBarContainer.SetActive(useTimeBar);

        if (timeBarFillImage != null && useTimeBar)
        {
            timeBarFillImage.fillAmount = 1f;
            timeBarFillImage.color = Color.white;
        }

        if (sweepHighlightKeyImage != null)
            sweepHighlightKeyImage.gameObject.SetActive(false);

        if (sweepHighlightTargetImage != null)
            sweepHighlightTargetImage.gameObject.SetActive(false);
    }

    // ✅ NUEVO: calcula y pinta “rondas restantes” (solo número)
    private void UpdateRoundsRemainingUI()
    {
        if (roundsRemainingText == null) return;

        int remaining = Mathf.Max(0, goalCorrectRounds - correctRoundsThisAttempt);
        roundsRemainingText.text = remaining.ToString();
    }

    // =========================================================
    //  CREAR LLAVES Y CONFIGURAR DROPZONE
    // =========================================================
    private void SpawnKeysAndSetupDropZone()
    {
        foreach (Transform child in keysContainer) Destroy(child.gameObject);
        currentKeys.Clear();
        correctKey = null;

        int correctIndex = Random.Range(0, keysCount);

        int correctShapeIndex = 0;
        if (keyShapes != null && keyShapes.Length > 0)
            correctShapeIndex = Random.Range(0, keyShapes.Length);

        if (dropZoneImage != null && keyShapes != null && keyShapes.Length > 0)
            dropZoneImage.sprite = keyShapes[correctShapeIndex];

        List<int> availableShapeIndices = new List<int>();
        if (keyShapes != null)
        {
            for (int i = 0; i < keyShapes.Length; i++)
                if (i != correctShapeIndex) availableShapeIndices.Add(i);
        }

        Color correctHighlightColor = Color.yellow;
        int correctHighlightIndex = 0;
        List<int> otherHighlightIndices = new List<int>();

        if (highlightColors != null && highlightColors.Length > 0)
        {
            correctHighlightIndex = Random.Range(0, highlightColors.Length);
            correctHighlightColor = highlightColors[correctHighlightIndex];

            for (int i = 0; i < highlightColors.Length; i++)
                if (i != correctHighlightIndex) otherHighlightIndices.Add(i);
        }

        if (dropZoneImage != null)
        {
            dropZoneImage.color = IsColorAidActive ? correctHighlightColor : Color.black;
        }

        for (int i = 0; i < keysCount; i++)
        {
            GameObject keyGO = Instantiate(keyPrefab, keysContainer);
            KeyDraggable key = keyGO.GetComponent<KeyDraggable>();
            key.SetGameManager(this);

            int keyId = i;
            key.SetKeyId(keyId);

            Image img = keyGO.GetComponent<Image>();

            if (img != null && keyShapes != null && keyShapes.Length > 0)
            {
                if (i == correctIndex)
                {
                    img.sprite = keyShapes[correctShapeIndex];
                }
                else
                {
                    if (availableShapeIndices.Count > 0)
                    {
                        int randomListIndex = Random.Range(0, availableShapeIndices.Count);
                        int shapeIndex = availableShapeIndices[randomListIndex];
                        img.sprite = keyShapes[shapeIndex];
                        availableShapeIndices.RemoveAt(randomListIndex);
                    }
                    else
                    {
                        img.sprite = keyShapes[0];
                    }
                }
            }

            Color finalColor = Color.white;

            if (IsColorAidActive)
            {
                if (i == correctIndex)
                {
                    finalColor = correctHighlightColor;
                }
                else
                {
                    if (highlightColors != null && highlightColors.Length > 0)
                    {
                        int colorIndex;

                        if (otherHighlightIndices.Count > 0)
                        {
                            int randomIdx = Random.Range(0, otherHighlightIndices.Count);
                            colorIndex = otherHighlightIndices[randomIdx];
                            otherHighlightIndices.RemoveAt(randomIdx);
                        }
                        else
                        {
                            colorIndex = correctHighlightIndex;
                        }

                        finalColor = highlightColors[colorIndex];
                    }
                    else
                    {
                        finalColor = Color.gray;
                    }
                }
            }
            else
            {
                if (baseKeyColors != null && baseKeyColors.Length > 0)
                {
                    finalColor = baseKeyColors[i % baseKeyColors.Length];
                }
                else
                {
                    finalColor = Color.gray;
                }
            }

            if (img != null) img.color = finalColor;
            key.SetBaseColor(finalColor);

            currentKeys.Add(key);
            if (i == correctIndex) correctKey = key;
        }

        if (dropZone != null && correctKey != null)
        {
            dropZone.SetGameManager(this);
            dropZone.SetCorrectKeyId(correctKey.KeyId);
        }
    }

    // =========================================================
    //  CRONÓMETRO (NIVELES 1-3)
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

        int minutes = Mathf.FloorToInt(chronometerTime / 60f);
        int seconds = Mathf.FloorToInt(chronometerTime % 60f);

        textChronometer.text = $"{minutes:00}:{seconds:00}";
    }

    // =========================================================
    //  BARRA DE TIEMPO (NIVEL 4)
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
        chronometerRunning = false;
        timeBarRunning = false;

        ShowFeedback("Se acabó el tiempo", isCorrect: false);
        StartCoroutine(TimeOutCoroutine());
    }

    private IEnumerator TimeOutCoroutine()
    {
        yield return new WaitForSeconds(1.0f);
        EndAttemptInSystem(completed: false);
        GoToResults();
    }

    // =========================================================
    //  FEEDBACK Y RESULTADOS
    // =========================================================
    public void OnKeyDroppedOnDropZone(KeyDraggable keyDropped)
    {
        bool isCorrect = (keyDropped.KeyId == correctKey.KeyId);

        if (isCorrect) HandleCorrectKey(keyDropped);
        else HandleIncorrectKey(keyDropped);
    }

    private void HandleCorrectKey(KeyDraggable key)
    {
        if (GameSessionManager.I != null)
            GameSessionManager.I.AddCorrect(firstTry: roundErrors == 0);

        correctRoundsThisAttempt++;
        roundErrors = 0;

        // ✅ NUEVO: actualizar contador (baja 1)
        UpdateRoundsRemainingUI();

        ShowFeedback("¡Correcto!", isCorrect: true);
        key.PlayCorrectAnimation();

        if (correctRoundsThisAttempt >= goalCorrectRounds)
        {
            StartCoroutine(WinCoroutine());
        }
        else
        {
            StartCoroutine(NextRoundCoroutine());
        }
    }

    private void HandleIncorrectKey(KeyDraggable key)
    {
        if (GameSessionManager.I != null)
            GameSessionManager.I.AddError();

        roundErrors++;

        ShowFeedback("Prueba otra vez", isCorrect: false);
        key.ReturnToStartPosition();
    }

    private IEnumerator NextRoundCoroutine()
    {
        yield return new WaitForSeconds(1f);

        SpawnKeysAndSetupDropZone();

        // (Opcional) por seguridad, repintamos el contador
        UpdateRoundsRemainingUI();

        if (useTimeBar) StartTimeBar();
    }

    private IEnumerator WinCoroutine()
    {
        chronometerRunning = false;
        timeBarRunning = false;

        ShowFeedback("¡Nivel completado!", isCorrect: true);
        yield return new WaitForSeconds(1.0f);

        EndAttemptInSystem(completed: true);
        GoToResults();
    }

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
        float t = 0f;
        float duration = 0.3f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, maxAlpha, t / duration);
            SetFeedbackFlashColor(color, alpha);
            yield return null;
        }

        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(maxAlpha, 0f, t / duration);
            SetFeedbackFlashColor(color, alpha);
            yield return null;
        }

        SetFeedbackFlashColor(color, 0f);
    }

    private void SetFeedbackFlashColor(Color baseColor, float alpha)
    {
        if (imageFeedbackFlash == null) return;

        Color c = baseColor;
        c.a = alpha;
        imageFeedbackFlash.color = c;
    }

    // =========================================================
    //  PAUSA DEL MINIJUEGO
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
        EndAttemptInSystem(completed: false);

        Time.timeScale = 1f;
        isPaused = false;

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
        else
            Debug.LogError("[EncajaLaLlave] 'hubSceneName' no está configurado.");
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
