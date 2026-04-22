using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public enum CarGameLevel
{
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
    Level4 = 4
}

public class EncuentraElCocheGameManager : MonoBehaviour
{
    // =========================================================
    //  CONFIGURACIÓN DE NIVEL
    // =========================================================
    [Header("Level Config")]
    [SerializeField] private CarGameLevel currentLevel = CarGameLevel.Level1;

    [Header("CleverMinds (solo testing si no hay GameSessionManager)")]
    [Tooltip("Si ejecutas esta escena suelta, se usará este nivel.")]
    [SerializeField] private CarGameLevel fallbackLevelForTesting = CarGameLevel.Level1;

    // ✅ NUEVO: rondas objetivo por nivel (igual que Llaves)
    [Header("Rondas objetivo (por nivel)")]
    [SerializeField] private int goalCorrectRounds_Level1 = 5;
    [SerializeField] private int goalCorrectRounds_Level2 = 6;
    [SerializeField] private int goalCorrectRounds_Level3 = 7;
    [SerializeField] private int goalCorrectRounds_Level4 = 7;

    private int goalCorrectRounds = 5;
    private int correctRoundsThisAttempt = 0;
    private int roundsPlayedThisAttempt = 0;   // rondas jugadas (acierto o fallo)
    private bool failedAnyRoundThisAttempt = false;

    private int carsCount = 3;
    private bool useChronometer = false; // niveles 1-3
    private bool useTimeBar = false;     // nivel 4

    // Ayuda de fallos:
    // Nivel 1 -> 1 fallo permitido
    // Nivel 2 -> 2 fallos permitidos
    // Nivel 3-4 -> 0
    private int allowedIncorrectClicks = 0;
    private int incorrectClicksThisRound = 0;

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
    //  MODELOS DE COCHE (SO)
    // =========================================================
    [Header("Modelos de coche (SO)")]
    [SerializeField] private CarModelSO[] carModels;

    // =========================================================
    //  SPAWN
    // =========================================================
    [Header("Spawn")]
    [SerializeField] private Transform carsContainer;
    [SerializeField] private GameObject carButtonPrefab;

    [Header("Fondos y containers por nivel")]
    [Tooltip("Image de fondo que cambiará de sprite según el nivel.")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite level1BackgroundSprite;
    [SerializeField] private Sprite higherLevelsBackgroundSprite;
    [SerializeField] private Transform level1CarsContainer;
    [SerializeField] private Transform higherLevelsCarsContainer;

    // =========================================================
    //  UI
    // =========================================================
    [Header("UI General")]
    [SerializeField] private TextMeshProUGUI textMinijuegoTitulo;
    [SerializeField] private TextMeshProUGUI textNivelActual;
    [SerializeField] private TextMeshProUGUI textHint;
    [SerializeField] private TextMeshProUGUI textFeedback;
    [SerializeField] private Image imageFeedbackFlash;

    // ✅ NUEVO: contador rondas restantes (solo número, opcional)
    [Header("UI Rondas (solo número) - opcional")]
    [SerializeField] private TextMeshProUGUI roundsRemainingText;

    [Header("UI Cronómetro (niveles 1-3)")]
    [SerializeField] private GameObject chronometerContainer;
    [SerializeField] private TextMeshProUGUI textChronometer;

    [Header("UI Barra de Tiempo (nivel 4)")]
    [SerializeField] private GameObject timeBarContainer;
    [SerializeField] private Image timeBarFillImage;

    [Header("UI Pausa")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private string hubSceneName = "03_MinigameHub";

    [Header("Feedback visual coches fallados")]
    [Range(0.05f, 1f)][SerializeField] private float failedCarAlpha = 0.2f;

    [Header("Feedback coche correcto (cuando se pierde ronda por descarte)")]
    [SerializeField] private bool blinkCorrectCarOnRoundLoss = true;
    [SerializeField] private Color correctCarBlinkColor = Color.green;
    [SerializeField] private int correctCarBlinkCount = 3;
    [SerializeField] private float correctCarBlinkStepDuration = 0.12f;

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
    //  ESTADO RONDA
    // =========================================================
    private List<CarButtonController> currentCars = new List<CarButtonController>();
    private CarButtonController correctCar = null;

    // Datos de pista actual
    private int currentHintDigit = -1;
    private char currentHintLetter = '\0';
    private int currentHintDigitSum = -1;
    private string currentHintColorName = "";

    private bool isPaused = false;

    // Cache config/tuning (Core)
    private MiniGameConfig cfg;
    private MiniGameConfig.LevelTuning tuning;

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
        if (pausePanel != null) pausePanel.SetActive(false);

        SetupLevelConfig();
        ApplyLevelVisualLayout();
        SetupUI();

        BeginAttemptIfPossible();

        GenerateNewRound();

        if (useChronometer) StartChronometer();
        if (useTimeBar) StartTimeBar();

        UpdateRoundsRemainingUI();
    }

    private void Update()
    {
        // CleverMinds: tiempo para scoring (no depende del UI de cronómetro)
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
    //  MODELOS DE COCHE (SO)
    // =========================================================
    private bool TryGetRandomCarVariant(out Sprite spriteOut, out string colorNameOut, out string modelNameOut)
    {
        spriteOut = null;
        colorNameOut = string.Empty;
        modelNameOut = string.Empty;

        if (carModels == null || carModels.Length == 0)
            return false;

        int attempts = Mathf.Max(10, carModels.Length * 3);

        for (int i = 0; i < attempts; i++)
        {
            CarModelSO model = carModels[Random.Range(0, carModels.Length)];
            if (model == null) continue;

            if (model.TryGetRandomVariant(out CarModelSO.ColorSpriteVariant variant))
            {
                spriteOut = variant.sprite;
                colorNameOut = variant.colorName;
                modelNameOut = model.ModelName;
                return true;
            }
        }

        return false;
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
            currentLevel = (CarGameLevel)GameSessionManager.I.currentSelection.level;
            return;
        }

        currentLevel = fallbackLevelForTesting;
    }

    private void PullConfigTuningFromCore()
    {
        if (GameSessionManager.I == null) return;

        cfg = GameSessionManager.I.GetConfig();
        tuning = GameSessionManager.I.GetTuning();

        // Nivel 4: usamos targetTimeSeconds como límite de barra.
        if ((LevelId)currentLevel == LevelId.Level4)
        {
            if (tuning != null) timeLimitSeconds = Mathf.Max(1f, tuning.targetTimeSeconds);
            else timeLimitSeconds = fallbackTimeLimitSeconds_Level4;
        }
    }

    private void BeginAttemptIfPossible()
    {
        if (!saveAttemptOnExit) return;
        if (GameSessionManager.I == null || GameSessionManager.I.profile == null) return;

        float limit = ((LevelId)currentLevel == LevelId.Level4) ? timeLimitSeconds : 0f;
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
    //  LEVEL CONFIG + RONDAS
    // =========================================================
    private void SetupLevelConfig()
    {
        switch (currentLevel)
        {
            case CarGameLevel.Level1:
                carsCount = 3;
                useChronometer = true;
                useTimeBar = false;
                allowedIncorrectClicks = 1;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level1);
                break;

            case CarGameLevel.Level2:
                carsCount = 5;
                useChronometer = true;
                useTimeBar = false;
                allowedIncorrectClicks = 2;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level2);
                break;

            case CarGameLevel.Level3:
                carsCount = 6;
                useChronometer = true;
                useTimeBar = false;
                allowedIncorrectClicks = 0;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level3);
                break;

            case CarGameLevel.Level4:
                carsCount = 6;
                useChronometer = false;
                useTimeBar = true;
                allowedIncorrectClicks = 0;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level4);
                break;
        }

        correctRoundsThisAttempt = 0;
        roundsPlayedThisAttempt = 0;
        failedAnyRoundThisAttempt = false;
    }

    private void ApplyLevelVisualLayout()
    {
        bool isLevel1 = currentLevel == CarGameLevel.Level1;

        if (backgroundImage != null)
        {
            Sprite targetSprite = isLevel1 ? level1BackgroundSprite : higherLevelsBackgroundSprite;
            backgroundImage.sprite = targetSprite;
            backgroundImage.enabled = targetSprite != null;
        }

        if (level1CarsContainer != null)
            level1CarsContainer.gameObject.SetActive(isLevel1);

        if (higherLevelsCarsContainer != null)
            higherLevelsCarsContainer.gameObject.SetActive(!isLevel1);

        Transform selectedContainer = isLevel1 ? level1CarsContainer : higherLevelsCarsContainer;
        if (selectedContainer != null)
            carsContainer = selectedContainer;
    }

    private void SetupUI()
    {
        if (textMinijuegoTitulo != null) textMinijuegoTitulo.text = "Encuentra el coche";
        if (textNivelActual != null) textNivelActual.text = $"NIVEL {(int)currentLevel}";

        if (textFeedback != null) textFeedback.text = "";
        if (textHint != null) textHint.text = "";

        if (imageFeedbackFlash != null)
        {
            Color c = imageFeedbackFlash.color;
            c.a = 0f;
            imageFeedbackFlash.color = c;
        }

        if (chronometerContainer != null) chronometerContainer.SetActive(useChronometer);
        if (textChronometer != null && useChronometer) textChronometer.text = "00:00";

        if (timeBarContainer != null) timeBarContainer.SetActive(useTimeBar);
        if (timeBarFillImage != null && useTimeBar)
        {
            timeBarFillImage.fillAmount = 1f;
            timeBarFillImage.color = Color.white;
        }

        UpdateRoundsRemainingUI();
    }

    // ✅ NUEVO: UI rondas restantes (solo número)
    private void UpdateRoundsRemainingUI()
    {
        if (roundsRemainingText == null) return;

        int remaining = Mathf.Max(0, goalCorrectRounds - roundsPlayedThisAttempt);
        roundsRemainingText.text = remaining.ToString();
    }

    // =========================================================
    //  RONDA
    // =========================================================
    private void GenerateNewRound()
    {
        incorrectClicksThisRound = 0;

        foreach (Transform child in carsContainer)
            Destroy(child.gameObject);

        currentCars.Clear();
        correctCar = null;

        if (carButtonPrefab == null || carsContainer == null)
        {
            Debug.LogError("[EncuentraElCoche] Falta carButtonPrefab o carsContainer.");
            return;
        }

        if (carModels == null || carModels.Length == 0)
        {
            Debug.LogError("[EncuentraElCoche] No hay modelos configurados en carModels.");
            return;
        }

        if (!TryGetRandomCarVariant(out Sprite correctSprite, out string correctColorName, out string correctModelName))
        {
            Debug.LogError("[EncuentraElCoche] Ningún CarModelSO tiene sprites de color válidos configurados.");
            return;
        }

        int correctIndex = Random.Range(0, carsCount);

        currentHintColorName = correctColorName;

        string correctPlate = GeneratePlateWithConstraintsForCurrentLevel(
            out int digitSum,
            out char chosenLetter,
            out int chosenDigit);

        currentHintDigitSum = digitSum;
        currentHintLetter = chosenLetter;
        currentHintDigit = chosenDigit;

        List<bool> distractorConditionPlan = BuildDistractorConditionPlan(Mathf.Max(0, carsCount - 1));
        int distractorPlanIndex = 0;
        HashSet<string> usedPlates = new HashSet<string> { correctPlate };

        for (int i = 0; i < carsCount; i++)
        {
            GameObject go = Instantiate(carButtonPrefab, carsContainer);
            CarButtonController car = go.GetComponent<CarButtonController>();

            if (car == null)
            {
                Debug.LogError("[EncuentraElCoche] El prefab no tiene CarButtonController.");
                return;
            }

            bool isCorrect = (i == correctIndex);

            Sprite spriteToUse;
            string plateToUse;
            string colorNameToUse;
            string modelNameToUse;

            if (isCorrect)
            {
                spriteToUse = correctSprite;
                plateToUse = correctPlate;
                colorNameToUse = currentHintColorName;
                modelNameToUse = correctModelName;
            }
            else
            {
                bool useFirstConditionOnly = distractorConditionPlan[distractorPlanIndex];
                distractorPlanIndex++;

                GenerateDistractorForCurrentLevel(
                    correctPlate,
                    currentHintColorName,
                    digitSum,
                    chosenLetter,
                    chosenDigit,
                    useFirstConditionOnly,
                    usedPlates,
                    out spriteToUse,
                    out plateToUse,
                    out colorNameToUse,
                    out modelNameToUse);

                usedPlates.Add(plateToUse);
            }

            car.Setup(
                this,
                spriteToUse,
                plateToUse,
                colorNameToUse,
                isCorrect,
                modelNameToUse);

            currentCars.Add(car);
            if (isCorrect) correctCar = car;
        }

        GenerateHintText();
    }

    // =========================================================
    //  MATRÍCULAS
    // =========================================================
    private string GenerateRandomPlate()
    {
        int d1 = Random.Range(0, 10);
        int d2 = Random.Range(0, 10);
        int d3 = Random.Range(0, 10);
        int d4 = Random.Range(0, 10);

        char l1 = GetRandomLetter();
        char l2 = GetRandomLetter();
        char l3 = GetRandomLetter();

        return $"{d1}{d2}{d3}{d4} {l1}{l2}{l3}";
    }

    private char GetRandomLetter()
    {
        int index = Random.Range(0, 26);
        return (char)('A' + index);
    }

    private int GetDigitSum(string plate)
    {
        int sum = 0;
        foreach (char c in plate)
            if (char.IsDigit(c)) sum += (c - '0');
        return sum;
    }

    private bool PlateContainsLetter(string plate, char letter)
    {
        return plate.Contains(letter.ToString());
    }

    private string GeneratePlateWithConstraintsForCurrentLevel(
        out int digitSum,
        out char chosenLetter,
        out int chosenDigit)
    {
        string plate = "";
        digitSum = 0;
        chosenLetter = 'A';
        chosenDigit = 0;

        bool valid = false;

        while (!valid)
        {
            plate = GenerateRandomPlate();
            digitSum = GetDigitSum(plate);

            switch (currentLevel)
            {
                case CarGameLevel.Level1:
                case CarGameLevel.Level2:
                    valid = true;
                    break;

                case CarGameLevel.Level3:
                case CarGameLevel.Level4:
                    valid = (digitSum <= 12);
                    break;
            }
        }

        chosenLetter = plate[5 + Random.Range(0, 3)];
        char digitChar = plate[Random.Range(0, 4)];
        chosenDigit = digitChar - '0';

        return plate;
    }

    // =========================================================
    //  DISTRACTORES
    // =========================================================
    private List<bool> BuildDistractorConditionPlan(int distractorCount)
    {
        List<bool> plan = new List<bool>(distractorCount);

        for (int i = 0; i < distractorCount; i++)
            plan.Add(i % 2 == 0);

        for (int i = 0; i < plan.Count; i++)
        {
            int randomIndex = Random.Range(i, plan.Count);
            bool temp = plan[i];
            plan[i] = plan[randomIndex];
            plan[randomIndex] = temp;
        }

        return plan;
    }

    private bool TryGetRandomCarVariantWithExactColor(
        string requiredColorName,
        out Sprite spriteOut,
        out string colorNameOut,
        out string modelNameOut)
    {
        spriteOut = null;
        colorNameOut = string.Empty;
        modelNameOut = string.Empty;

        if (carModels == null || carModels.Length == 0)
            return false;

        int attempts = Mathf.Max(10, carModels.Length * 4);

        for (int i = 0; i < attempts; i++)
        {
            CarModelSO model = carModels[Random.Range(0, carModels.Length)];
            if (model == null) continue;

            if (model.TryGetSpriteByColor(requiredColorName, out Sprite sprite) && sprite != null)
            {
                spriteOut = sprite;
                colorNameOut = requiredColorName;
                modelNameOut = model.ModelName;
                return true;
            }
        }

        return false;
    }

    private bool TryGetRandomCarVariantWithDifferentColor(
        string excludedColorName,
        out Sprite spriteOut,
        out string colorNameOut,
        out string modelNameOut)
    {
        spriteOut = null;
        colorNameOut = string.Empty;
        modelNameOut = string.Empty;

        int attempts = Mathf.Max(20, (carModels != null ? carModels.Length : 0) * 6);

        for (int i = 0; i < attempts; i++)
        {
            if (!TryGetRandomCarVariant(out Sprite sprite, out string colorName, out string modelName))
                break;

            if (colorName != excludedColorName)
            {
                spriteOut = sprite;
                colorNameOut = colorName;
                modelNameOut = modelName;
                return true;
            }
        }

        return false;
    }

    private string GeneratePlateForLevel1(bool colorOnlyDistractor, char correctLetter)
    {
        for (int attempt = 0; attempt < 500; attempt++)
        {
            string plate = GenerateRandomPlate();
            bool hasLetter = PlateContainsLetter(plate, correctLetter);

            if (colorOnlyDistractor)
            {
                if (!hasLetter) return plate;
            }
            else
            {
                if (hasLetter) return plate;
            }
        }

        return GenerateRandomPlate();
    }

    private string GeneratePlateForLevel2(bool letterOnlyDistractor, char correctLetter, int correctDigit)
    {
        for (int attempt = 0; attempt < 500; attempt++)
        {
            string plate = GenerateRandomPlate();
            bool hasLetter = PlateContainsLetter(plate, correctLetter);
            bool hasDigit = plate.Contains(correctDigit.ToString());

            if (letterOnlyDistractor)
            {
                if (hasLetter && !hasDigit) return plate;
            }
            else
            {
                if (!hasLetter && hasDigit) return plate;
            }
        }

        return GenerateRandomPlate();
    }

    private string GeneratePlateForLevel3And4(bool sumOnlyDistractor, int correctDigitSum, char correctLetter)
    {
        for (int attempt = 0; attempt < 3000; attempt++)
        {
            string plate = GenerateRandomPlate();
            int sum = GetDigitSum(plate);
            bool hasLetter = PlateContainsLetter(plate, correctLetter);

            if (sumOnlyDistractor)
            {
                if (sum == correctDigitSum && !hasLetter) return plate;
            }
            else
            {
                if (sum != correctDigitSum && hasLetter) return plate;
            }
        }

        return GenerateRandomPlate();
    }

    private void GenerateDistractorForCurrentLevel(
        string correctPlate,
        string correctColorName,
        int correctDigitSum,
        char correctLetter,
        int correctDigit,
        bool useFirstConditionOnly,
        HashSet<string> usedPlates,
        out Sprite spriteOut,
        out string plateOut,
        out string colorNameOut,
        out string modelNameOut)
    {
        spriteOut = null;
        colorNameOut = string.Empty;
        modelNameOut = string.Empty;
        plateOut = string.Empty;

        const int maxAttempts = 200;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            bool variantReady = false;

            switch (currentLevel)
            {
                case CarGameLevel.Level1:
                    if (useFirstConditionOnly)
                    {
                        variantReady = TryGetRandomCarVariantWithExactColor(
                            correctColorName,
                            out spriteOut,
                            out colorNameOut,
                            out modelNameOut);

                        plateOut = GeneratePlateForLevel1(true, correctLetter);
                    }
                    else
                    {
                        variantReady = TryGetRandomCarVariantWithDifferentColor(
                            correctColorName,
                            out spriteOut,
                            out colorNameOut,
                            out modelNameOut);

                        plateOut = GeneratePlateForLevel1(false, correctLetter);
                    }
                    break;

                case CarGameLevel.Level2:
                    variantReady = TryGetRandomCarVariant(
                        out spriteOut,
                        out colorNameOut,
                        out modelNameOut);

                    plateOut = GeneratePlateForLevel2(
                        letterOnlyDistractor: useFirstConditionOnly,
                        correctLetter,
                        correctDigit);
                    break;

                case CarGameLevel.Level3:
                case CarGameLevel.Level4:
                    variantReady = TryGetRandomCarVariant(
                        out spriteOut,
                        out colorNameOut,
                        out modelNameOut);

                    plateOut = GeneratePlateForLevel3And4(
                        sumOnlyDistractor: useFirstConditionOnly,
                        correctDigitSum,
                        correctLetter);
                    break;
            }

            if (!variantReady) continue;
            if (string.IsNullOrEmpty(plateOut)) continue;
            if (plateOut == correctPlate) continue;
            if (usedPlates != null && usedPlates.Contains(plateOut)) continue;

            return;
        }

        if (!TryGetRandomCarVariant(out spriteOut, out colorNameOut, out modelNameOut))
        {
            spriteOut = null;
            colorNameOut = string.Empty;
            modelNameOut = string.Empty;
        }

        plateOut = GenerateRandomPlate();
    }

    // =========================================================
    //  TEXTO DE PISTA
    // =========================================================
    private void GenerateHintText()
    {
        if (textHint == null) return;

        bool forceColor = hasVisualDifficulty && !string.IsNullOrEmpty(currentHintColorName);
        string hint = "";

        switch (currentLevel)
        {
            case CarGameLevel.Level1:
                hint = $"Busca el coche de color {currentHintColorName} cuya matrícula contiene la letra \"{currentHintLetter}\".";
                break;

            case CarGameLevel.Level2:
                hint = $"Busca el coche cuya matrícula contiene la letra \"{currentHintLetter}\" y el número \"{currentHintDigit}\".";
                if (forceColor) hint += $" (Pista extra por visión: el coche es de color {currentHintColorName}).";
                break;

            case CarGameLevel.Level3:
            case CarGameLevel.Level4:
                hint = $"La suma de las cifras de la matrícula es {currentHintDigitSum} y contiene la letra \"{currentHintLetter}\".";
                if (forceColor) hint += $" (Pista extra por visión: el coche es de color {currentHintColorName}).";
                break;
        }

        textHint.text = hint;
    }

    // =========================================================
    //  CRONÓMETRO
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

        ShowFeedback("Se acabó el tiempo", false);
        StartCoroutine(TimeOutCoroutine());
    }

    private IEnumerator TimeOutCoroutine()
    {
        yield return new WaitForSeconds(1.0f);

        // Nivel 4: timeout = intento fallido (como Llaves)
        EndAttemptIfStarted(completed: false);
        GoToResults();
    }

    // =========================================================
    //  RESPUESTAS
    // =========================================================
    public void OnCarSelected(CarButtonController car)
    {
        if (car == null || correctCar == null) return;
        if (isPaused) return;
        if (attemptEnded) return;

        if (car == correctCar)
            HandleCorrectCar(car);
        else
            HandleIncorrectCar(car);
    }

    private void HandleCorrectCar(CarButtonController car)
    {
        // CleverMinds metrics
        if (GameSessionManager.I != null)
        {
            bool firstTry = (incorrectClicksThisRound == 0);
            GameSessionManager.I.AddCorrect(firstTry);
        }

        correctRoundsThisAttempt++;
        roundsPlayedThisAttempt++;
        UpdateRoundsRemainingUI();

        ShowFeedback("¡Correcto!", true);
        car.SetHighlight(true);

        bool noMoreRounds = roundsPlayedThisAttempt >= goalCorrectRounds;
        if (noMoreRounds)
        {
            // Si hubo alguna ronda perdida, el intento termina como no completado.
            if (failedAnyRoundThisAttempt)
                StartCoroutine(FinalResultsCoroutine(false, "Fin de rondas"));
            else
                StartCoroutine(WinCoroutine());
        }
        else
        {
            StartCoroutine(NextRoundCoroutine());
        }
    }

    private IEnumerator FinalResultsCoroutine(bool completed, string feedbackMessage)
    {
        chronometerRunning = false;
        timeBarRunning = false;

        ShowFeedback(feedbackMessage, completed);

        yield return new WaitForSeconds(1.0f);

        EndAttemptIfStarted(completed: completed);
        GoToResults();
    }

    private IEnumerator WinCoroutine()
    {
        // Paramos timers
        chronometerRunning = false;
        timeBarRunning = false;

        ShowFeedback("¡Nivel completado!", true);

        yield return new WaitForSeconds(1.0f);

        // ✅ Guardado en sistema: completado
        EndAttemptIfStarted(completed: true);

        GoToResults();
    }

    private void HandleIncorrectCar(CarButtonController car)
    {
        incorrectClicksThisRound++;

        // CleverMinds metrics
        GameSessionManager.I?.AddError();

        // Descartar visualmente coche fallado (baja opacidad y desactiva botón)
        if (car != null)
        {
            car.SetHighlight(false);
            car.DisableAsFailed(failedCarAlpha);
        }

        // Pasamos de ronda SOLO cuando ya se han fallado todos los coches incorrectos.
        // (Queda únicamente el coche correcto por descarte.)
        int wrongCarsAvailable = Mathf.Max(0, carsCount - 1);
        bool roundExhaustedByFailures = incorrectClicksThisRound >= wrongCarsAvailable;

        if (!roundExhaustedByFailures)
        {
            ShowFeedback("No es ese coche. Sigue probando.", false);
            return;
        }

        bool wasLastRequiredRound = (roundsPlayedThisAttempt >= goalCorrectRounds - 1);

        failedAnyRoundThisAttempt = true;
        roundsPlayedThisAttempt++;
        UpdateRoundsRemainingUI();

        if (wasLastRequiredRound)
        {
            chronometerRunning = false;
            timeBarRunning = false;
            ShowFeedback("Ronda perdida. Fin del intento.", false);
            StartCoroutine(RoundLostByDiscardCoroutine(goToResultsAfter: true));
        }
        else
        {
            ShowFeedback("Ronda perdida. Pasamos a la siguiente.", false);
            StartCoroutine(RoundLostByDiscardCoroutine(goToResultsAfter: false));
        }
    }

    private IEnumerator RoundLostByDiscardCoroutine(bool goToResultsAfter)
    {
        if (blinkCorrectCarOnRoundLoss && correctCar != null)
            yield return StartCoroutine(BlinkCorrectCarCoroutine(correctCar));
        else
            yield return new WaitForSeconds(1.0f);

        if (goToResultsAfter)
        {
            EndAttemptIfStarted(completed: false);
            GoToResults();
        }
        else
        {
            GenerateNewRound();
            UpdateRoundsRemainingUI();
            if (useTimeBar) StartTimeBar();
        }
    }

    private IEnumerator BlinkCorrectCarCoroutine(CarButtonController car)
    {
        int count = Mathf.Max(1, correctCarBlinkCount);
        float step = Mathf.Max(0.03f, correctCarBlinkStepDuration);

        car.ResetVisualState();

        for (int i = 0; i < count; i++)
        {
            car.SetBackgroundColor(correctCarBlinkColor);
            yield return new WaitForSeconds(step);
            car.ResetBackgroundColor();
            yield return new WaitForSeconds(step);
        }

        car.SetBackgroundColor(correctCarBlinkColor);
        yield return new WaitForSeconds(step * 1.2f);
        car.ResetBackgroundColor();
    }

    private IEnumerator RemoveHighlightAfterDelay(CarButtonController car, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (car != null) car.SetHighlight(false);
    }

    private IEnumerator NextRoundCoroutine()
    {
        yield return new WaitForSeconds(1f);

        GenerateNewRound();
        UpdateRoundsRemainingUI();

        if (useTimeBar) StartTimeBar();
    }

    // =========================================================
    //  FEEDBACK
    // =========================================================
    private void ShowFeedback(string message, bool isCorrect)
    {
        if (textFeedback != null) textFeedback.text = message;

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
    //  PAUSA
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
        // Si el usuario sale manualmente (sin win/timeout), guardamos como no completado
        if (saveAttemptOnExit && attemptStarted && !attemptEnded)
            EndAttemptIfStarted(completed: false);

        Time.timeScale = 1f;
        isPaused = false;

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
        else
            Debug.LogError("[EncuentraElCoche] hubSceneName no está configurado.");
    }

    private const string resultsSceneName = "05_Results";

    private void GoToResults()
    {
        Time.timeScale = 1f;
        isPaused = false;
        SceneManager.LoadScene(resultsSceneName);
    }
}
















































































