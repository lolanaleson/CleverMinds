using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public enum SuitcaseGameLevel
{
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
    Level4 = 4
}

public class EncuentraLaMaletaGameManager : MonoBehaviour
{
    // =========================================================
    //  CONFIGURACIÓN DE NIVEL
    // =========================================================
    [Header("Level Config")]
    [SerializeField] private SuitcaseGameLevel currentLevel = SuitcaseGameLevel.Level1;

    [Header("CleverMinds (solo testing si no hay GameSessionManager)")]
    [Tooltip("Si ejecutas esta escena suelta, se usará este nivel.")]
    [SerializeField] private SuitcaseGameLevel fallbackLevelForTesting = SuitcaseGameLevel.Level1;

    // ✅ NUEVO: rondas objetivo por nivel (como EncuentraElCoche / Llaves)
    [Header("Rondas objetivo (por nivel)")]
    [SerializeField] private int goalCorrectRounds_Level1 = 5;
    [SerializeField] private int goalCorrectRounds_Level2 = 6;
    [SerializeField] private int goalCorrectRounds_Level3 = 7;
    [SerializeField] private int goalCorrectRounds_Level4 = 7;

    private int goalCorrectRounds = 5;
    private int correctRoundsThisAttempt = 0;

    private const int SUITCASES_PER_ROUND = 5;

    private bool useChronometer = false; // niveles 1–3
    private bool useTimeBar = false;     // nivel 4

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

    // Para firstTry (si falló alguna vez en la ronda)
    private bool roundHadAnyError = false;

    // =========================================================
    //  SLOTS / PREFAB
    // =========================================================
    [Header("Slots de maletas (5 posiciones fijas)")]
    [Tooltip("Arrastra aquí EXACTAMENTE 5 transforms (Slot_1..Slot_5). Orden = dónde aparecen.")]
    [SerializeField] private List<Transform> suitcaseSlots = new List<Transform>(5);

    [Header("Prefab")]
    [SerializeField] private GameObject suitcaseButtonPrefab;

    [Header("Sprites de maletas (tus 5 colores)")]
    [Tooltip("Arrastra tus 5 sprites. Se asignan aleatoriamente y NO dan pistas.")]
    [SerializeField] private List<Sprite> suitcaseVisualSprites = new List<Sprite>();

    // =========================================================
    //  UI
    // =========================================================
    [Header("UI General")]
    [SerializeField] private TextMeshProUGUI textMinijuegoTitulo;
    [SerializeField] private TextMeshProUGUI textNivelActual;
    [SerializeField] private TextMeshProUGUI textInstruction;
    [SerializeField] private TextMeshProUGUI textFeedback;
    [SerializeField] private Image imageFeedbackFlash;

    // ✅ opcional: contador de rondas restantes (solo número)
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

    private bool isPaused = false;
    private bool inputLocked = false;

    private readonly List<SuitcaseButtonController> spawnedSuitcases = new List<SuitcaseButtonController>();

    // Cache config/tuning (Core)
    private MiniGameConfig cfg;
    private MiniGameConfig.LevelTuning tuning;

    // =========================================================
    //  WORD SETS
    // =========================================================
    [System.Serializable]
    public class WordSet
    {
        public List<string> correctWords = new List<string>(4);
        public string impostorWord;
    }

    [Header("WordSets (autogenerados si vacíos)")]
    [SerializeField] private List<WordSet> level1Sets = new List<WordSet>();
    [SerializeField] private List<WordSet> level2Sets = new List<WordSet>();
    [SerializeField] private List<WordSet> level3Sets = new List<WordSet>();

    // =========================================================
    //  UNITY
    // =========================================================
    private void Awake()
    {
        LoadPlayerDataFromSingleton();
        PullConfigTuningFromCore();

        if (level1Sets.Count == 0 || level2Sets.Count == 0 || level3Sets.Count == 0)
            BuildDefault40Sets();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        isPaused = false;
        inputLocked = false;

        if (pausePanel != null) pausePanel.SetActive(false);

        SetupLevelConfig();
        SetupUI();

        BeginAttemptIfPossible();

        GenerateNewRound();

        if (useChronometer)
            StartChronometer();

        if (useTimeBar)
            StartTimeBar();

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
    //  CLEVERMINDS (perfil/nivel)
    // =========================================================
    private void LoadPlayerDataFromSingleton()
    {
        if (GameSessionManager.I != null && GameSessionManager.I.profile != null)
        {
            hasVisualDifficulty = GameSessionManager.I.profile.hasVisionIssues;
            hasAuditoryDifficulty = GameSessionManager.I.profile.hasHearingIssues;
            currentLevel = (SuitcaseGameLevel)GameSessionManager.I.currentSelection.level;
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
            case SuitcaseGameLevel.Level1:
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level1);
                break;

            case SuitcaseGameLevel.Level2:
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level2);
                break;

            case SuitcaseGameLevel.Level3:
                useChronometer = true;
                useTimeBar = false;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level3);
                break;

            case SuitcaseGameLevel.Level4:
                useChronometer = false;
                useTimeBar = true;
                goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level4);
                break;
        }

        correctRoundsThisAttempt = 0;
    }

    private void SetupUI()
    {
        if (textMinijuegoTitulo != null) textMinijuegoTitulo.text = "¿De quién es esta maleta?";
        if (textNivelActual != null) textNivelActual.text = $"NIVEL {(int)currentLevel}";

        if (textInstruction != null)
            textInstruction.text = "¿Qué maleta no necesitan Pedro y Carmen?";

        if (textFeedback != null) textFeedback.text = "";

        if (imageFeedbackFlash != null)
        {
            var c = imageFeedbackFlash.color;
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

    private void UpdateRoundsRemainingUI()
    {
        if (roundsRemainingText == null) return;
        int remaining = Mathf.Max(0, goalCorrectRounds - correctRoundsThisAttempt);
        roundsRemainingText.text = remaining.ToString();
    }

    // =========================================================
    //  RONDA (Slots fijos)
    // =========================================================
    private void GenerateNewRound()
    {
        inputLocked = false;
        roundHadAnyError = false;

        if (suitcaseButtonPrefab == null)
        {
            Debug.LogError("[Maletas] Falta suitcaseButtonPrefab.");
            return;
        }

        if (suitcaseSlots == null || suitcaseSlots.Count != SUITCASES_PER_ROUND)
        {
            Debug.LogError("[Maletas] Necesitas EXACTAMENTE 5 slots en suitcaseSlots (Slot_1..Slot_5).");
            return;
        }

        if (suitcaseVisualSprites == null || suitcaseVisualSprites.Count == 0)
        {
            Debug.LogError("[Maletas] No hay sprites en suitcaseVisualSprites. Arrastra tus 5 maletas.");
            return;
        }

        // Limpiar hijos de cada slot
        for (int i = 0; i < suitcaseSlots.Count; i++)
        {
            Transform slot = suitcaseSlots[i];
            if (slot == null) continue;

            for (int c = slot.childCount - 1; c >= 0; c--)
                Destroy(slot.GetChild(c).gameObject);
        }

        spawnedSuitcases.Clear();

        WordSet set = GetRandomSetForCurrentLevel();
        if (set == null || set.correctWords == null || set.correctWords.Count < 4 || string.IsNullOrEmpty(set.impostorWord))
        {
            Debug.LogError("[Maletas] WordSet inválido o vacío para este nivel.");
            return;
        }

        List<string> roundWords = new List<string>(SUITCASES_PER_ROUND);
        roundWords.AddRange(set.correctWords);
        roundWords.Add(set.impostorWord);
        Shuffle(roundWords);

        for (int i = 0; i < SUITCASES_PER_ROUND; i++)
        {
            Transform slot = suitcaseSlots[i];
            if (slot == null) continue;

            string word = roundWords[i];
            bool isImpostor = (word == set.impostorWord);

            Sprite visualSprite = PickRandomSuitcaseSprite();

            GameObject go = Instantiate(suitcaseButtonPrefab, slot);

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition3D = Vector3.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
            }
            else
            {
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            SuitcaseButtonController suitcase = go.GetComponent<SuitcaseButtonController>();
            if (suitcase == null)
            {
                Debug.LogError("[Maletas] El prefab no tiene SuitcaseButtonController.");
                return;
            }

            suitcase.Setup(this, word, isImpostor, visualSprite);
            spawnedSuitcases.Add(suitcase);
        }
    }

    private WordSet GetRandomSetForCurrentLevel()
    {
        List<WordSet> list = null;

        switch (currentLevel)
        {
            case SuitcaseGameLevel.Level1: list = level1Sets; break;
            case SuitcaseGameLevel.Level2: list = level2Sets; break;
            case SuitcaseGameLevel.Level3: list = level3Sets; break;
            case SuitcaseGameLevel.Level4: list = level3Sets; break;
        }

        if (list == null || list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    private Sprite PickRandomSuitcaseSprite()
    {
        return suitcaseVisualSprites[Random.Range(0, suitcaseVisualSprites.Count)];
    }

    // =========================================================
    //  INPUT + MÉTRICAS (CleverMinds)
    // =========================================================
    public void OnSuitcaseSelected(SuitcaseButtonController suitcase)
    {
        if (suitcase == null) return;
        if (inputLocked) return;
        if (isPaused) return;
        if (attemptEnded) return;

        inputLocked = true;

        bool isCorrect = suitcase.IsImpostor();

        if (!isCorrect)
        {
            GameSessionManager.I?.AddError();
            roundHadAnyError = true;

            suitcase.SetDiscarded(true);
            ShowFeedback(PickWrongLine(), false);
            StartCoroutine(UnlockInputSoon());
            return;
        }

        if (GameSessionManager.I != null)
        {
            bool firstTry = !roundHadAnyError;
            GameSessionManager.I.AddCorrect(firstTry);
        }

        correctRoundsThisAttempt++;
        UpdateRoundsRemainingUI();

        suitcase.SetHighlight(true);
        ShowFeedback(PickCorrectLine(), true);

        if (correctRoundsThisAttempt >= goalCorrectRounds)
        {
            StartCoroutine(WinCoroutine());
        }
        else
        {
            StartCoroutine(NextRoundCoroutine());
        }
    }

    private IEnumerator WinCoroutine()
    {
        chronometerRunning = false;
        timeBarRunning = false;

        ShowFeedback("¡Nivel completado!", true);

        yield return new WaitForSeconds(1.0f);

        EndAttemptIfStarted(completed: true);
        GoToResults();
    }

    private IEnumerator UnlockInputSoon()
    {
        yield return new WaitForSeconds(0.6f);
        inputLocked = false;
    }

    private IEnumerator NextRoundCoroutine()
    {
        yield return new WaitForSeconds(1.0f);
        GenerateNewRound();
    }

    // =========================================================
    //  FEEDBACK
    // =========================================================
    private string PickCorrectLine()
    {
        string[] lines = { "Muy bien.", "Esta se ha colado.", "Eso no es nuestro." };
        return lines[Random.Range(0, lines.Length)];
    }

    private string PickWrongLine()
    {
        string[] lines =
        {
            "Esa sí podría ser nuestra.",
            "No pasa nada, probemos otra.",
            "Mmm… esta no parece la colada."
        };
        return lines[Random.Range(0, lines.Length)];
    }

    private void ShowFeedback(string message, bool isCorrect)
    {
        if (textFeedback != null) textFeedback.text = message;

        if (imageFeedbackFlash != null)
        {
            Color baseColor = isCorrect ? Color.green : Color.yellow;
            float maxAlpha = hasAuditoryDifficulty ? 0.7f : 0.4f;
            StartCoroutine(FlashImageCoroutine(baseColor, maxAlpha));
        }
    }

    private IEnumerator FlashImageCoroutine(Color color, float maxAlpha)
    {
        float t = 0f;
        float duration = 0.25f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, maxAlpha, t / duration);
            SetFlashColor(color, alpha);
            yield return null;
        }

        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(maxAlpha, 0f, t / duration);
            SetFlashColor(color, alpha);
            yield return null;
        }

        SetFlashColor(color, 0f);
    }

    private void SetFlashColor(Color baseColor, float alpha)
    {
        if (imageFeedbackFlash == null) return;
        Color c = baseColor; c.a = alpha;
        imageFeedbackFlash.color = c;
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
    //  TIEMPO (Nivel 4)
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

        EndAttemptIfStarted(completed: false);
        GoToResults();
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
        if (saveAttemptOnExit && attemptStarted && !attemptEnded)
            EndAttemptIfStarted(completed: false);

        Time.timeScale = 1f;
        isPaused = false;

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
        else
            Debug.LogError("[Maletas] hubSceneName no está configurado.");
    }

    // =========================================================
    //  UTIL
    // =========================================================
    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    private WordSet WS(string a, string b, string c, string d, string impostor)
    {
        WordSet s = new WordSet();
        s.correctWords = new List<string> { a, b, c, d };
        s.impostorWord = impostor;
        return s;
    }

    // =========================================================
    // 40 SETS POR NIVEL (120 total)
    // =========================================================
    private void BuildDefault40Sets()
    {
        level1Sets = new List<WordSet>();
        level2Sets = new List<WordSet>();
        level3Sets = new List<WordSet>();

        // =========================================================
        // NIVEL 1 (40) – ABSURDO EVIDENTE (muy fácil)
        // =========================================================
        level1Sets.Add(WS("camiseta", "pantalón", "calcetines", "zapatillas", "tenedor"));
        level1Sets.Add(WS("pasaporte", "billete", "cartera", "móvil", "sartén"));
        level1Sets.Add(WS("toalla", "chanclas", "bañador", "crema solar", "martillo"));
        level1Sets.Add(WS("pijama", "ropa interior", "cepillo de dientes", "peine", "escoba"));
        level1Sets.Add(WS("gafas", "llaves", "cargador", "pañuelos", "maceta"));
        level1Sets.Add(WS("chaqueta", "bufanda", "guantes", "gorro", "regadera"));
        level1Sets.Add(WS("libro", "auriculares", "agua", "chicles", "microondas"));
        level1Sets.Add(WS("neceser", "champú", "gel", "crema", "lavadora"));
        level1Sets.Add(WS("gorra", "gafas de sol", "crema solar", "toalla", "cortacésped"));
        level1Sets.Add(WS("camiseta", "sudadera", "vaqueros", "cinturón", "aspiradora"));

        level1Sets.Add(WS("pijama", "zapatillas", "antifaz", "tapones", "caja de herramientas"));
        level1Sets.Add(WS("cartera", "tarjeta", "llaves", "móvil", "alfombra"));
        level1Sets.Add(WS("cepillo de dientes", "pasta", "hilo dental", "peine", "fregona"));
        level1Sets.Add(WS("agua", "galletas", "snacks", "pañuelos", "bandeja de horno"));
        level1Sets.Add(WS("paraguas", "chaqueta", "pañuelos", "gorro", "perchero"));
        level1Sets.Add(WS("toalla", "bañador", "chanclas", "gorra", "tabla de planchar"));
        level1Sets.Add(WS("móvil", "cargador", "auriculares", "powerbank", "router"));
        level1Sets.Add(WS("neceser", "colonia", "desodorante", "crema", "rodillo de pintura"));
        level1Sets.Add(WS("libro", "gafas", "cartera", "llaves", "tostadora"));
        level1Sets.Add(WS("camiseta", "pantalón", "calcetines", "zapatos", "cacerola"));

        level1Sets.Add(WS("ropa", "pijama", "calcetines", "zapatillas", "manguera"));
        level1Sets.Add(WS("pasaporte", "billete", "tarjeta", "móvil", "lámpara"));
        level1Sets.Add(WS("toalla", "peine", "neceser", "crema", "sierra"));
        level1Sets.Add(WS("gafas", "gorra", "crema solar", "bañador", "taco de pared"));
        level1Sets.Add(WS("auriculares", "móvil", "cargador", "libro", "archivador"));
        level1Sets.Add(WS("chaqueta", "bufanda", "guantes", "botas", "edredón"));
        level1Sets.Add(WS("cartera", "llaves", "monedas", "tarjeta", "cubo de basura"));
        level1Sets.Add(WS("pijama", "camiseta", "pantalón", "calcetines", "secador grande"));
        level1Sets.Add(WS("neceser", "champú", "gel", "cepillo de dientes", "lejía"));
        level1Sets.Add(WS("gafas", "pañuelos", "agua", "chicles", "sartén grande"));

        // repetimos hasta 40 con variaciones sencillas:
        level1Sets.Add(WS("móvil", "cargador", "gafas", "llaves", "martillo"));
        level1Sets.Add(WS("pijama", "ropa interior", "calcetines", "zapatillas", "tendedero"));
        level1Sets.Add(WS("pasaporte", "billete", "cartera", "tarjeta", "escoba"));
        level1Sets.Add(WS("toalla", "chanclas", "bañador", "crema solar", "silla"));
        level1Sets.Add(WS("libro", "gafas", "auriculares", "agua", "televisor"));
        level1Sets.Add(WS("neceser", "crema", "desodorante", "peine", "aspiradora"));
        level1Sets.Add(WS("chaqueta", "bufanda", "gorro", "guantes", "sombrilla de jardín"));
        level1Sets.Add(WS("camiseta", "pantalón", "cinturón", "zapatos", "fregadero"));
        level1Sets.Add(WS("cepillo de dientes", "pasta", "hilo dental", "enjuague", "batidora"));
        level1Sets.Add(WS("cartera", "llaves", "móvil", "cargador", "caja de tornillos"));

        // =========================================================
        // NIVEL 2 (40) – REALISTA (cuesta un poco)
        // Idea: la impostora ES pequeña y cotidiana, pero NO “de maleta”.
        // =========================================================
        level2Sets.Add(WS("móvil", "cargador", "gafas", "llaves", "cinta aislante"));
        level2Sets.Add(WS("pasaporte", "billete", "cartera", "tarjeta", "taco de pared"));
        level2Sets.Add(WS("cepillo de dientes", "pasta", "desodorante", "peine", "esponja de fregar"));
        level2Sets.Add(WS("pijama", "calcetines", "ropa interior", "zapatillas", "pinzas de tender"));
        level2Sets.Add(WS("auriculares", "libro", "agua", "pañuelos", "grapadora"));
        level2Sets.Add(WS("crema solar", "gafas de sol", "gorra", "bañador", "cúter"));
        level2Sets.Add(WS("neceser", "champú", "gel", "crema", "estropajo"));
        level2Sets.Add(WS("paraguas", "chaqueta", "bufanda", "gorro", "cinta métrica"));
        level2Sets.Add(WS("pastillas", "tiritas", "agua", "pañuelos", "destornillador"));
        level2Sets.Add(WS("móvil", "powerbank", "cable USB", "auriculares", "bridas"));

        level2Sets.Add(WS("cartera", "llaves", "móvil", "gafas", "guantes de limpieza"));
        level2Sets.Add(WS("pijama", "camiseta", "pantalón", "calcetines", "bayeta"));
        level2Sets.Add(WS("neceser", "cepillo de dientes", "pasta", "hilo dental", "limpiacristales"));
        level2Sets.Add(WS("libro", "gafas", "pañuelos", "agua", "tornillos"));
        level2Sets.Add(WS("cargador", "móvil", "tablet", "auriculares", "pilas AA sueltas"));
        level2Sets.Add(WS("crema", "desodorante", "peine", "colonia", "rollo de cinta doble cara"));
        level2Sets.Add(WS("pasaporte", "tarjeta", "billete", "móvil", "llave inglesa pequeña"));
        level2Sets.Add(WS("toalla", "chanclas", "bañador", "gorra", "quitapelusas"));
        level2Sets.Add(WS("mapa", "guía", "billete", "móvil", "regla metálica"));
        level2Sets.Add(WS("cartera", "monedas", "tarjeta", "llaves", "goma de borrar grande"));

        level2Sets.Add(WS("pijama", "zapatillas", "antifaz", "tapones", "gomaespuma"));
        level2Sets.Add(WS("neceser", "gel", "champú", "crema", "ambientador de casa"));
        level2Sets.Add(WS("móvil", "cargador", "auriculares", "powerbank", "alargador"));
        level2Sets.Add(WS("pastillas", "agua", "tiritas", "termómetro", "caja de grapas"));
        level2Sets.Add(WS("libro", "auriculares", "móvil", "cargador", "llaves de repuesto"));
        level2Sets.Add(WS("cepillo de dientes", "pasta", "peine", "crema", "pastilla de lavavajillas"));
        level2Sets.Add(WS("paraguas", "chaqueta", "bufanda", "pañuelos", "bolsa de basura"));
        level2Sets.Add(WS("cartera", "tarjeta", "pasaporte", "billete", "sello de caucho"));
        level2Sets.Add(WS("toalla", "neceser", "desodorante", "peine", "cera para muebles"));
        level2Sets.Add(WS("gafas", "móvil", "llaves", "cargador", "cola blanca"));

        // 10 más (para llegar a 40):
        level2Sets.Add(WS("móvil", "cargador", "gafas", "cartera", "brocha de pintura"));
        level2Sets.Add(WS("pasaporte", "billete", "tarjeta", "llaves", "taco de silicona"));
        level2Sets.Add(WS("pijama", "calcetines", "camiseta", "zapatillas", "recogedor"));
        level2Sets.Add(WS("neceser", "champú", "gel", "crema", "quitagrasas"));
        level2Sets.Add(WS("auriculares", "libro", "agua", "pañuelos", "carpeta archivadora"));
        level2Sets.Add(WS("crema solar", "gorra", "gafas de sol", "bañador", "papel de lija"));
        level2Sets.Add(WS("pastillas", "tiritas", "agua", "pañuelos", "cinta de embalar"));
        level2Sets.Add(WS("paraguas", "chaqueta", "bufanda", "gorro", "spray insecticida"));
        level2Sets.Add(WS("cartera", "tarjeta", "móvil", "llaves", "clavos"));
        level2Sets.Add(WS("cepillo de dientes", "pasta", "hilo dental", "peine", "piedra pómez de suelo"));

        // =========================================================
        // NIVEL 3 (40) – FINO (cuesta bastante)
        // Idea: TODAS son pequeñas y “podrían viajar”.
        // La impostora es la que implica “gestionar la casa / cocina / bricolaje”, pero MUY sutil.
        // =========================================================
        level3Sets.Add(WS("pijama", "pastillas", "cargador", "pañuelos", "esponja de fregar"));
        level3Sets.Add(WS("cepillo de dientes", "pasta", "hilo dental", "enjuague", "pastilla de lavavajillas"));
        level3Sets.Add(WS("móvil", "cargador", "auriculares", "powerbank", "bridas"));
        level3Sets.Add(WS("gafas", "pañuelos", "cartera", "llaves", "cinta aislante"));
        level3Sets.Add(WS("crema", "desodorante", "peine", "colonia", "cera para muebles"));
        level3Sets.Add(WS("tiritas", "alcohol", "pastillas", "termómetro", "cola blanca"));
        level3Sets.Add(WS("libro", "gafas", "agua", "pañuelos", "quitapelusas"));
        level3Sets.Add(WS("pijama", "calcetines", "camiseta", "zapatillas", "pinzas de tender"));
        level3Sets.Add(WS("pasaporte", "billete", "tarjeta", "móvil", "alargador"));
        level3Sets.Add(WS("toalla", "neceser", "gel", "champú", "estropajo"));

        level3Sets.Add(WS("cargador", "móvil", "auriculares", "llaves", "cinta de embalar"));
        level3Sets.Add(WS("pastillas", "agua", "pañuelos", "tiritas", "taco de pared"));
        level3Sets.Add(WS("cepillo de dientes", "pasta", "peine", "crema", "limpiacristales"));
        level3Sets.Add(WS("gafas", "cartera", "tarjeta", "llaves", "grapadora"));
        level3Sets.Add(WS("libro", "auriculares", "móvil", "cargador", "rollo de cinta doble cara"));
        level3Sets.Add(WS("pijama", "ropa interior", "calcetines", "camiseta", "bayeta"));
        level3Sets.Add(WS("neceser", "champú", "gel", "desodorante", "guantes de limpieza"));
        level3Sets.Add(WS("móvil", "cargador", "powerbank", "gafas", "tornillos"));
        level3Sets.Add(WS("pastillas", "tiritas", "crema", "agua", "papel de lija"));
        level3Sets.Add(WS("paraguas", "chaqueta", "bufanda", "pañuelos", "spray insecticida"));

        // 20 más (para llegar a 40):
        level3Sets.Add(WS("pasaporte", "billete", "cartera", "tarjeta", "sello de caucho"));
        level3Sets.Add(WS("toalla", "peine", "crema", "desodorante", "quitagrasas"));
        level3Sets.Add(WS("cargador", "móvil", "auriculares", "tablet", "cúter"));
        level3Sets.Add(WS("gafas", "móvil", "llaves", "cartera", "clavos"));
        level3Sets.Add(WS("cepillo de dientes", "pasta", "hilo dental", "peine", "ambientador de casa"));
        level3Sets.Add(WS("pijama", "calcetines", "zapatillas", "camiseta", "recogedor"));
        level3Sets.Add(WS("pastillas", "agua", "pañuelos", "tiritas", "cinta métrica"));
        level3Sets.Add(WS("libro", "gafas", "auriculares", "agua", "carpeta archivadora"));
        level3Sets.Add(WS("neceser", "champú", "gel", "crema", "piedra pómez de suelo"));
        level3Sets.Add(WS("móvil", "cargador", "auriculares", "powerbank", "llave inglesa pequeña"));

        level3Sets.Add(WS("pasaporte", "billete", "móvil", "cargador", "brocha de pintura"));
        level3Sets.Add(WS("toalla", "chanclas", "bañador", "gafas de sol", "regla metálica"));
        level3Sets.Add(WS("pijama", "ropa interior", "calcetines", "zapatillas", "bolsa de basura"));
        level3Sets.Add(WS("crema", "peine", "desodorante", "colonia", "limpiador de muebles"));
        level3Sets.Add(WS("tiritas", "termómetro", "pastillas", "agua", "cinta aislante gruesa"));
        level3Sets.Add(WS("libro", "auriculares", "móvil", "gafas", "taco de silicona"));
        level3Sets.Add(WS("cepillo de dientes", "pasta", "enjuague", "hilo dental", "pastilla WC"));
        level3Sets.Add(WS("cartera", "tarjeta", "llaves", "móvil", "caja de grapas"));
        level3Sets.Add(WS("neceser", "gel", "champú", "crema", "estropajo metálico"));
        level3Sets.Add(WS("pastillas", "pañuelos", "agua", "crema", "cinta de embalar grande"));
    }

    private const string resultsSceneName = "05_Results";

    private void GoToResults()
    {
        Time.timeScale = 1f;
        isPaused = false;
        SceneManager.LoadScene(resultsSceneName);
    }
}
