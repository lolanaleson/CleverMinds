using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class RecipeMemoryGameManager : MonoBehaviour
{
    // =========================================================
    //  CONFIGURACIÓN DE NIVEL (CleverMinds)
    // =========================================================
    [Header("Level Config")]
    // En producción: GameSessionManager.I.currentSelection.level
    // En test/editor: fallbackLevelForTesting
    [SerializeField] private LevelId fallbackLevelForTesting = LevelId.Level1;
    private LevelId currentLevel = LevelId.Level1;

    private bool useChronometer = false; // niveles 1-3
    private bool useTimeBar = false;     // nivel 4

    private int missingCount = 1;
    private int optionsCount = 3;

    // Objetivo de nivel (cuántas recetas completas para “pasar”)
    [Header("Objetivo (recetas completas para terminar el nivel)")]
    [SerializeField] private int goalCompletedRecipes_Level1 = 3;
    [SerializeField] private int goalCompletedRecipes_Level2 = 4;
    [SerializeField] private int goalCompletedRecipes_Level3 = 6;
    [SerializeField] private int goalCompletedRecipes_Level4 = 6;
    private int goalCompletedRecipes = 3;

    private int completedRecipesThisAttempt = 0;
    private bool attemptEnded = false;

    // =========================================================
    //  DATOS DEL JUGADOR (ACCESIBILIDAD)
    // =========================================================
    [Header("Singleton / Datos del jugador")]
    [SerializeField] private bool hasVisualDifficulty = false;   // hasVisionIssues
    [SerializeField] private bool hasAuditoryDifficulty = false; // hasHearingIssues

    // =========================================================
    //  DATOS (RECETAS + BANCO GLOBAL)
    // =========================================================
    [Header("Contenido")]
    [SerializeField] private List<RecipeSO> recipes = new List<RecipeSO>();

    [Tooltip("Ingredientes distractores (no relacionados). Se usarán para rellenar opciones.")]
    [SerializeField] private List<IngredientSO> globalIngredientBank = new List<IngredientSO>();

    // =========================================================
    //  UI
    // =========================================================
    [Header("UI General")]
    [SerializeField] private TextMeshProUGUI textMinijuegoTitulo;  // SOLO DURANTE LECTURA
    [SerializeField] private TextMeshProUGUI textNivelActual;

    [Header("UI Receta")]
    [SerializeField] private TextMeshProUGUI textRecipeTitle;      // NOMBRE RECETA (NO TOCAR)
    [SerializeField] private TextMeshProUGUI textRecipeIngredients;

    [Header("Opciones")]
    [SerializeField] private RectTransform optionsContainer;
    [SerializeField] private GameObject ingredientOptionButtonPrefab;

    [Header("Opciones - Posiciones Manuales (slots)")]
    [Tooltip("Arrastra aquí tus RectTransform vacíos (Pos_01..Pos_07), hijos de OptionsContainer.")]
    [SerializeField] private List<RectTransform> optionPositions = new List<RectTransform>();

    [Tooltip("Si quieres que el botón copie también escala/rotación del punto (normalmente no hace falta).")]
    [SerializeField] private bool copyRotationAndScaleFromPosition = false;

    [Header("UI Feedback")]
    [SerializeField] private TextMeshProUGUI textFeedback;

    [Header("UI Cronómetro (niveles 1-3)")]
    [SerializeField] private GameObject chronometerContainer;
    [SerializeField] private TextMeshProUGUI textChronometer;

    [Header("UI Barra de Tiempo (nivel 4)")]
    [SerializeField] private GameObject timeBarContainer;
    [SerializeField] private Image timeBarFillImage;
    [SerializeField] private float timeLimitSeconds = 20f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float extraWaitAfterAudio = 1f;

    [Header("UI Pausa")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private string hubSceneName = "03_MinigameHub";

    [Header("UI Start")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private Button startButton;

    [Header("UI Intro Visual")]
    [SerializeField] private Image recipeResultImage;
    [SerializeField] private IngredientIntroUI ingredientIntroPrefab;

    [Tooltip("Posiciones manuales alrededor del plato. Índice 0 = ingrediente 0.")]
    [SerializeField] private List<RectTransform> ingredientIntroPositions = new List<RectTransform>();

    [Header("Halo parpadeante")]
    [SerializeField] private float ingredientHaloBlinkInterval = 0.2f;

    // =========================================================
    //  ESTADO INTERNO
    // =========================================================
    private RecipeSO currentRecipe;
    private List<int> missingIndices = new List<int>();
    private int currentMissingPointer = 0;
    private HashSet<IngredientSO> alreadyPlaced = new HashSet<IngredientSO>();

    private readonly List<IngredientIntroUI> introIngredientInstances = new List<IngredientIntroUI>();
    private Coroutine introReadCoroutine;
    private bool gameStartedFromStartPanel = false;

    private bool isInputLocked = false;
    private bool isPaused = false;

    // Errores por hueco actual (para firstTryCorrect)
    private int slotErrors = 0;

    // Timers
    private float chronometerTime = 0f;
    private bool chronometerRunning = false;

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
        SetupStartPanel();
    }

    private void SetupStartPanel()
    {
        if (startPanel != null)
            startPanel.SetActive(true);

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartButtonPressed);
            startButton.onClick.AddListener(OnStartButtonPressed);
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartButtonPressed);
    }

    private void OnStartButtonPressed()
    {
        if (gameStartedFromStartPanel) return;
        gameStartedFromStartPanel = true;

        if (startPanel != null)
            startPanel.SetActive(false);

        BeginAttemptInSystem();
        StartNewRound();
    }

    private void Update()
    {
        if (isPaused) return;

        // TickTime SOLO cuando el “tiempo de juego” está realmente corriendo
        if (GameSessionManager.I != null && GameSessionManager.I.currentAttempt != null && !attemptEnded)
        {
            if (chronometerRunning || timeBarRunning)
                GameSessionManager.I.TickTime(Time.deltaTime);
        }

        // Cronómetro (1-3)
        if (useChronometer && chronometerRunning)
        {
            chronometerTime += Time.deltaTime;
            UpdateChronometerUI();
        }

        // Barra tiempo (nivel 4)
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
        switch (currentLevel)
        {
            case LevelId.Level1:
                missingCount = 1;
                optionsCount = 3;
                useChronometer = true;
                useTimeBar = false;
                goalCompletedRecipes = Mathf.Max(1, goalCompletedRecipes_Level1);
                break;

            case LevelId.Level2:
                missingCount = 2;
                optionsCount = 5;
                useChronometer = true;
                useTimeBar = false;
                goalCompletedRecipes = Mathf.Max(1, goalCompletedRecipes_Level2);
                break;

            case LevelId.Level3:
                missingCount = 3;
                optionsCount = 7;
                useChronometer = true;
                useTimeBar = false;
                goalCompletedRecipes = Mathf.Max(1, goalCompletedRecipes_Level3);
                break;

            case LevelId.Level4:
                missingCount = 3;
                optionsCount = 7;
                useChronometer = false;
                useTimeBar = true;
                goalCompletedRecipes = Mathf.Max(1, goalCompletedRecipes_Level4);
                break;
        }

        // Si es nivel 4 y hay tuning del sistema, lo usamos como “límite”
        if (useTimeBar && GameSessionManager.I != null)
        {
            var tuning = GameSessionManager.I.GetTuning();
            if (tuning != null && tuning.targetTimeSeconds > 0.1f)
                timeLimitSeconds = tuning.targetTimeSeconds;
        }

        completedRecipesThisAttempt = 0;
        attemptEnded = false;
        slotErrors = 0;
    }

    private void SetupUI()
    {
        if (textMinijuegoTitulo != null)
        {
            textMinijuegoTitulo.text = "MEMORIZA LA RECETA";
            textMinijuegoTitulo.enabled = false; // solo durante lectura
        }

        if (textNivelActual != null) textNivelActual.text = $"NIVEL {(int)currentLevel}";
        if (textFeedback != null) textFeedback.text = "";

        if (chronometerContainer != null) chronometerContainer.SetActive(useChronometer);
        if (textChronometer != null && useChronometer) textChronometer.text = "00:00";

        if (timeBarContainer != null) timeBarContainer.SetActive(useTimeBar);
        if (timeBarFillImage != null && useTimeBar) timeBarFillImage.fillAmount = 1f;
    }

    // =========================================================
    //  RONDA
    // =========================================================
    private void StartNewRound()
    {
        if (textFeedback != null) textFeedback.text = "";

        isInputLocked = true;
        ClearOptions();
        ClearIntroVisuals();

        currentRecipe = PickRandomRecipe();
        if (currentRecipe == null)
        {
            Debug.LogError("[RecipeMemory] No hay recetas asignadas.");
            return;
        }

        if (textRecipeTitle != null) textRecipeTitle.text = currentRecipe.recipeName;

        alreadyPlaced.Clear();
        RenderRecipeText(showBlanks: false);

        ResetTimersUIOnly();
        BuildIntroVisuals();

        bool hasAnyAudio = (audioSource != null) &&
                           (currentRecipe.recipeNameAudio != null ||
                            currentRecipe.ingredients.Any(i => i != null && i.audioClip != null));

        if (textMinijuegoTitulo != null)
            textMinijuegoTitulo.enabled = hasAnyAudio;

        if (introReadCoroutine != null)
            StopCoroutine(introReadCoroutine);

        introReadCoroutine = StartCoroutine(ShowAndReadThenHideCoroutine());
    }

    private RecipeSO PickRandomRecipe()
    {
        if (recipes == null || recipes.Count == 0) return null;
        return recipes[Random.Range(0, recipes.Count)];
    }

    private IEnumerator ShowAndReadThenHideCoroutine()
    {
        // 1) Audio en cadena, respetando pausa
        if (audioSource != null)
        {
            if (currentRecipe.recipeNameAudio != null)
            {
                audioSource.clip = currentRecipe.recipeNameAudio;
                audioSource.Play();

                while (audioSource.isPlaying || isPaused)
                    yield return null;
            }

            for (int i = 0; i < currentRecipe.ingredients.Count; i++)
            {
                IngredientSO ing = currentRecipe.ingredients[i];
                IngredientIntroUI introUI = (i < introIngredientInstances.Count) ? introIngredientInstances[i] : null;

                if (ing == null || ing.audioClip == null)
                {
                    if (introUI != null)
                        introUI.SetHaloActive(false);

                    continue;
                }

                Coroutine blinkCoroutine = null;

                if (introUI != null)
                    blinkCoroutine = StartCoroutine(BlinkHaloWhileAudio(introUI));

                audioSource.clip = ing.audioClip;
                audioSource.Play();

                while (audioSource.isPlaying || isPaused)
                    yield return null;

                if (blinkCoroutine != null)
                    StopCoroutine(blinkCoroutine);

                if (introUI != null)
                    introUI.SetHaloActive(false);
            }
        }

        // 2) Espera extra tras audio
        float t = 0f;
        while (t < extraWaitAfterAudio)
        {
            if (!isPaused)
                t += Time.unscaledDeltaTime;

            yield return null;
        }

        // NUEVO: aquí limpiamos la intro visual
        ClearIntroVisuals();

        // Ocultamos SOLO el título del minijuego
        if (textMinijuegoTitulo != null)
            textMinijuegoTitulo.enabled = false;

        // 3) Elegir qué ingredientes desaparecen
        PrepareMissingIndices();

        // 4) Render con huecos
        alreadyPlaced.Clear();
        RenderRecipeText(showBlanks: true);

        // 5) Opciones (botones)
        currentMissingPointer = 0;
        slotErrors = 0;
        SpawnOptionsForCurrentMissing();

        // Ahora empieza el tiempo cuando ya puedes jugar
        StartTimersNow();

        isInputLocked = false;
    }

    private void BuildIntroVisuals()
    {
        if (recipeResultImage != null)
        {
            recipeResultImage.sprite = currentRecipe != null ? currentRecipe.resultDishImage : null;
            recipeResultImage.enabled = (recipeResultImage.sprite != null);
        }

        if (ingredientIntroPrefab == null) return;
        if (ingredientIntroPositions == null || ingredientIntroPositions.Count == 0) return;
        if (currentRecipe == null || currentRecipe.ingredients == null) return;

        int count = Mathf.Min(currentRecipe.ingredients.Count, ingredientIntroPositions.Count);

        for (int i = 0; i < count; i++)
        {
            RectTransform slot = ingredientIntroPositions[i];
            if (slot == null) continue;

            IngredientIntroUI intro = Instantiate(ingredientIntroPrefab, slot);

            RectTransform rt = intro.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
            }

            intro.Setup(currentRecipe.ingredients[i]);
            introIngredientInstances.Add(intro);
        }
    }

    private void ClearIntroVisuals()
    {
        for (int i = 0; i < introIngredientInstances.Count; i++)
        {
            if (introIngredientInstances[i] != null)
                Destroy(introIngredientInstances[i].gameObject);
        }

        introIngredientInstances.Clear();

        if (recipeResultImage != null)
        {
            recipeResultImage.sprite = null;
            recipeResultImage.enabled = false;
        }
    }

    private IEnumerator BlinkHaloWhileAudio(IngredientIntroUI introUI)
    {
        if (introUI == null) yield break;

        float waitTime = Mathf.Max(0.01f, ingredientHaloBlinkInterval);
        bool visible = false;

        introUI.SetHaloActive(false);

        while (true)
        {
            visible = !visible;
            introUI.SetHaloActive(visible);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void PrepareMissingIndices()
    {
        missingIndices.Clear();

        int total = currentRecipe.ingredients.Count;
        int safeMissing = Mathf.Clamp(missingCount, 1, Mathf.Max(1, total - 1));

        List<int> indices = Enumerable.Range(0, total).OrderBy(_ => Random.value).ToList();
        for (int i = 0; i < safeMissing; i++)
            missingIndices.Add(indices[i]);

        missingIndices.Sort();
    }

    // =========================================================
    //  TEXTO DE RECETA
    // =========================================================
    private void RenderRecipeText(bool showBlanks)
    {
        if (textRecipeIngredients == null) return;

        List<string> lines = new List<string>();

        for (int i = 0; i < currentRecipe.ingredients.Count; i++)
        {
            IngredientSO ing = currentRecipe.ingredients[i];
            string ingredientName = ing != null ? ing.ingredientName.ToUpper() : "???";

            bool isMissing =
                showBlanks &&
                missingIndices.Contains(i) &&
                ing != null &&
                !alreadyPlaced.Contains(ing);

            if (isMissing)
                lines.Add(hasVisualDifficulty ? "- ______" : "- _____");
            else
                lines.Add("- " + ingredientName);
        }

        textRecipeIngredients.text = string.Join("\n", lines);
    }

    // =========================================================
    //  OPCIONES (slots)
    // =========================================================
    private void SpawnOptionsForCurrentMissing()
    {
        ClearOptions();

        if (ingredientOptionButtonPrefab == null || optionsContainer == null)
        {
            Debug.LogError("[RecipeMemory] Falta prefab o container de opciones.");
            return;
        }

        if (optionPositions == null || optionPositions.Count == 0)
        {
            Debug.LogError("[RecipeMemory] No has asignado optionPositions en el inspector.");
            return;
        }

        IngredientSO target = GetCurrentMissingIngredient();
        if (target == null) return;

        // nuevo hueco => reset de errores para firstTryCorrect
        slotErrors = 0;

        List<IngredientSO> pool = BuildOptionsPool(target, optionsCount)
            .OrderBy(_ => Random.value)
            .ToList();

        int needed = Mathf.Min(pool.Count, optionPositions.Count);
        List<RectTransform> chosenSlots = optionPositions
            .OrderBy(_ => Random.value)
            .Take(needed)
            .ToList();

        foreach (var slot in chosenSlots)
        {
            for (int c = slot.childCount - 1; c >= 0; c--)
                Destroy(slot.GetChild(c).gameObject);
        }

        for (int i = 0; i < needed; i++)
        {
            IngredientSO ing = pool[i];
            RectTransform slot = chosenSlots[i];

            GameObject go = Instantiate(ingredientOptionButtonPrefab, slot);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            if (copyRotationAndScaleFromPosition)
            {
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
            }

            IngredientOptionButton btn = go.GetComponent<IngredientOptionButton>();
            if (btn != null) btn.Setup(this, ing);
        }

        if (textFeedback != null)
            textFeedback.text = "Elige el ingrediente que falta.";
    }

    private IngredientSO GetCurrentMissingIngredient()
    {
        if (currentRecipe == null) return null;
        if (currentMissingPointer < 0 || currentMissingPointer >= missingIndices.Count) return null;

        int index = missingIndices[currentMissingPointer];
        return currentRecipe.ingredients[index];
    }

    private List<IngredientSO> BuildOptionsPool(IngredientSO correct, int desiredCount)
    {
        var result = new List<IngredientSO> { correct };
        HashSet<IngredientSO> recipeSet = new HashSet<IngredientSO>(currentRecipe.ingredients);

        foreach (var ing in globalIngredientBank.OrderBy(_ => Random.value))
        {
            if (result.Count >= desiredCount) break;
            if (ing == null || ing == correct) continue;
            if (recipeSet.Contains(ing)) continue;
            if (result.Contains(ing)) continue;
            result.Add(ing);
        }

        if (result.Count < desiredCount && recipes != null)
        {
            foreach (var r in recipes.OrderBy(_ => Random.value))
            {
                if (r == null || r.ingredients == null) continue;

                foreach (var ing in r.ingredients.OrderBy(_ => Random.value))
                {
                    if (result.Count >= desiredCount) break;
                    if (ing == null || ing == correct) continue;
                    if (recipeSet.Contains(ing)) continue;
                    if (result.Contains(ing)) continue;
                    result.Add(ing);
                }

                if (result.Count >= desiredCount) break;
            }
        }

        return result;
    }

    private void ClearOptions()
    {
        if (optionPositions == null) return;

        foreach (var slot in optionPositions)
        {
            if (slot == null) continue;

            for (int i = slot.childCount - 1; i >= 0; i--)
                Destroy(slot.GetChild(i).gameObject);
        }
    }

    // =========================================================
    //  INPUT DESDE BOTÓN (métricas integradas)
    // =========================================================
    public void OnIngredientSelected(IngredientSO selected, IngredientOptionButton clickedButton)
    {
        if (isInputLocked || selected == null) return;

        IngredientSO target = GetCurrentMissingIngredient();
        if (target == null) return;

        if (selected == target)
        {
            // ✅ métricas: acierto + firstTry
            if (GameSessionManager.I != null)
                GameSessionManager.I.AddCorrect(firstTry: slotErrors == 0);

            alreadyPlaced.Add(target);

            if (textFeedback != null)
                textFeedback.text = "¡Correcto!";

            currentMissingPointer++;

            RenderRecipeText(showBlanks: true);

            if (currentMissingPointer >= missingIndices.Count)
                StartCoroutine(RecipeCompletedCoroutine());
            else
                SpawnOptionsForCurrentMissing();
        }
        else
        {
            // ❌ métricas: error
            if (GameSessionManager.I != null)
                GameSessionManager.I.AddError();

            slotErrors++;

            if (textFeedback != null)
                textFeedback.text = "Casi, prueba otra opción.";

            if (clickedButton != null)
                clickedButton.SetInteractable(false);
        }
    }

    private IEnumerator RecipeCompletedCoroutine()
    {
        isInputLocked = true;

        completedRecipesThisAttempt++;

        yield return new WaitForSeconds(0.8f);

        // ¿Nivel completado?
        if (completedRecipesThisAttempt >= goalCompletedRecipes)
        {
            chronometerRunning = false;
            timeBarRunning = false;

            if (textFeedback != null)
                textFeedback.text = "¡Nivel completado!";

            yield return new WaitForSeconds(0.8f);

            EndAttemptInSystem(completed: true);
            GoToResults();
            yield break;
        }

        // Si no, siguiente receta
        StartNewRound();
    }

    // =========================================================
    //  TIMERS: reset y start cuando tocan
    // =========================================================
    private void ResetTimersUIOnly()
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

    private void StartTimersNow()
    {
        if (useChronometer)
            StartChronometer();

        if (useTimeBar)
            StartTimeBar();
    }

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
    }

    private void OnTimeExpired()
    {
        chronometerRunning = false;
        timeBarRunning = false;

        if (textFeedback != null)
            textFeedback.text = "Se acabó el tiempo. ¡Otra vez!";

        StartCoroutine(TimeOutCoroutine());
    }

    private IEnumerator TimeOutCoroutine()
    {
        yield return new WaitForSeconds(0.8f);

        EndAttemptInSystem(completed: false);
        GoToResults();
    }

    // =========================================================
    //  PAUSA: pausa audio real + congela juego
    // =========================================================
    public void PauseGame()
    {
        if (isPaused) return;

        isPaused = true;
        Time.timeScale = 0f;

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Pause();

        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;

        if (audioSource != null)
            audioSource.UnPause();

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    public void ExitToHub()
    {
        EndAttemptInSystem(completed: false);
        ExitToHubInternal();
    }

    private void ExitToHubInternal()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (audioSource != null)
            audioSource.Stop();

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
        else
            Debug.LogError("[RecipeMemory] hubSceneName no está configurado.");
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

        // Ajustar timeSeconds a la medición real del juego
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
        if (audioSource != null)
            audioSource.Stop();
        SceneManager.LoadScene(resultsSceneName);

        
    }
}

