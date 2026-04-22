using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public enum SpainMapLevel
{
    Level1 = 1, // CCAA
    Level2 = 2, // Provincia con pista de comunidad
    Level3 = 3, // Provincias
    Level4 = 4  // Provincias + tiempo
}

public enum SpainCommunity
{
    Andalucia,
    Aragon,
    Asturias,
    Baleares,
    Canarias,
    Cantabria,
    CastillaLaMancha,
    CastillaYLeon,
    Cataluna,
    ComunidadValenciana,
    Extremadura,
    Galicia,
    LaRioja,
    Madrid,
    Murcia,
    Navarra,
    PaisVasco,
    Ceuta,
    Melilla
}

public enum SpainProvince
{
    // Andalucía
    Almeria, Cadiz, Cordoba, Granada, Huelva, Jaen, Malaga, Sevilla,

    // Aragón
    Huesca, Teruel, Zaragoza,

    // Asturias
    Asturias,

    // Baleares
    Baleares,

    // Canarias
    LasPalmas, SantaCruzDeTenerife,

    // Cantabria
    Cantabria,

    // Castilla-La Mancha
    Albacete, CiudadReal, Cuenca, Guadalajara, Toledo,

    // Castilla y León
    Avila, Burgos, Leon, Palencia, Salamanca, Segovia, Soria, Valladolid, Zamora,

    // Cataluña
    Barcelona, Girona, Lleida, Tarragona,

    // Comunidad Valenciana
    Alicante, Castellon, Valencia,

    // Extremadura
    Badajoz, Caceres,

    // Galicia
    ACoruna, Lugo, Ourense, Pontevedra,

    // La Rioja
    LaRioja,

    // Madrid
    Madrid,

    // Murcia
    Murcia,

    // Navarra
    Navarra,

    // País Vasco
    Alava, Guipuzcoa, Vizcaya,

    // Ceuta/Melilla
    Ceuta, Melilla
}

public class SpainMapGameManager : MonoBehaviour
{
    // =========================================================
    //  CONFIGURACIÓN DE NIVEL
    // =========================================================
    [Header("Level Config")]
    // 🔥 Integración CleverMinds:
    // - En producción, el nivel se lee desde GameSessionManager.I.currentSelection.level
    // - En modo test (sin sesión), puedes forzarlo desde el inspector.
    [SerializeField] private LevelId fallbackLevelForTesting = LevelId.Level1;

    [SerializeField] private SpainMapLevel currentLevel = SpainMapLevel.Level1;

    private bool useChronometer = false; // niveles 1-3
    private bool useTimeBar = false;     // nivel 4

    // =========================================================
    //  MAPAS (2 IMAGES) + BOTONES (2 ROOTS)
    // =========================================================
    [Header("Map Images (Canvas)")]
    [SerializeField] private Image imageMapCommunities; // Nivel 1
    [SerializeField] private Image imageMapProvinces;   // Nivel 2-4

    [Header("Buttons Roots")]
    [SerializeField] private GameObject buttonsCommunitiesRoot; // Nivel 1
    [SerializeField] private GameObject buttonsProvincesRoot;   // Nivel 2-4

    // =========================================================
    //  UI GENERAL
    // =========================================================
    [Header("UI General")]
    [SerializeField] private TextMeshProUGUI textMinijuegoTitulo;
    [SerializeField] private TextMeshProUGUI textNivelActual;
    [SerializeField] private TextMeshProUGUI textInstruction;
    [SerializeField] private TextMeshProUGUI textFeedback;

    [Header("Round Transition Flash (WHITE Overlay)")]
    [Tooltip("Image blanca encima del mapa (alpha 0 al inicio). Debe tener Raycast Target OFF.")]
    [SerializeField] private Image roundTransitionFlashOverlay;

    // =========================================================
    //  FEEDBACK / TIMINGS
    // =========================================================
    [Header("Feedback Timings")]
    [SerializeField] private float correctHoldSeconds = 1.4f;
    [SerializeField] private float wrongHoldSeconds = 0.45f;
    [SerializeField] private float transitionFlashSeconds = 0.22f;

    // =========================================================
    //  FILTROS ACCESIBILIDAD
    // =========================================================
    [Header("Accessibility Filters")]
    [Tooltip("Nivel 1: excluir Ceuta y Melilla para evitar errores de pulsación.")]
    [SerializeField] private bool excludeSmallCommunities = true;

    [Tooltip("Niveles 2-4: excluir provincias pequeñas/difíciles (islas, País Vasco, Ceuta, Melilla).")]
    [SerializeField] private bool excludeSmallProvinces = true;

    private HashSet<SpainCommunity> excludedCommunities = new HashSet<SpainCommunity>
    {
        SpainCommunity.Ceuta,
        SpainCommunity.Melilla
    };

    private HashSet<SpainProvince> excludedProvinces = new HashSet<SpainProvince>
    {
        // Baleares
        SpainProvince.Baleares,

        // Canarias
        SpainProvince.LasPalmas,
        SpainProvince.SantaCruzDeTenerife,

        // País Vasco
        SpainProvince.Alava,
        SpainProvince.Guipuzcoa,
        SpainProvince.Vizcaya,

        // Ceuta y Melilla
        SpainProvince.Ceuta,
        SpainProvince.Melilla
    };

    // =========================================================
    //  UI CRONÓMETRO / BARRA TIEMPO
    // =========================================================
    [Header("UI Cronómetro (niveles 1-3)")]
    [SerializeField] private GameObject chronometerContainer;
    [SerializeField] private TextMeshProUGUI textChronometer;

    [Header("UI Barra de Tiempo (nivel 4)")]
    [SerializeField] private GameObject timeBarContainer;
    [SerializeField] private Image timeBarFillImage;

    // =========================================================
    //  PAUSA
    // =========================================================
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
    //  ESTADO
    // =========================================================
    private bool isPaused = false;
    public bool IsPaused => isPaused;

    private bool roundLocked = false;

    // =========================================================
    //  INTEGRACIÓN (INTENTOS / MÉTRICAS)
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

    // En este minijuego el nivel 4 tiene barra por ronda (mecánica original).
    // Para el scoring del intento guardamos un tiempo total de intento.
    private float attemptTotalTime = 0f;

    // =========================================================
    //  OBJETIVOS
    // =========================================================
    private SpainCommunity targetCommunity;
    private SpainProvince targetProvince;
    private SpainCommunity hintCommunityForLevel2;

    private Dictionary<SpainProvince, SpainCommunity> provinceToCommunity;
    private readonly List<SpainMapRegionButton> allRegionButtons = new List<SpainMapRegionButton>();

    // =========================================================
    //  CICLO DE VIDA
    // =========================================================
    private void Awake()
    {
        LoadFromGameSessionManagerOrFallback();
        BuildProvinceMappings();
        CacheAndHookAllRegionButtons();

        if (roundTransitionFlashOverlay != null)
        {
            Color c = roundTransitionFlashOverlay.color;
            c.a = 0f;
            roundTransitionFlashOverlay.color = c;
        }
    }

    private void Start()
    {
        Time.timeScale = 1f;
        isPaused = false;
        roundLocked = false;

        if (pausePanel != null) pausePanel.SetActive(false);

        SetupLevelConfig();
        SetupUI();

        StartNewRound();

        if (useChronometer) StartChronometer();
        if (useTimeBar) StartTimeBar();

        BeginAttemptInSystem();
    }

    private void Update()
    {
        if (isPaused) return;

        attemptTotalTime += Time.deltaTime;
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
    //  CONFIG
    // =========================================================
    private void SetupLevelConfig()
    {
        // Objetivo por nivel (para poder cerrar intento como el resto)
        switch (currentLevel)
        {
            case SpainMapLevel.Level1: goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level1); break;
            case SpainMapLevel.Level2: goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level2); break;
            case SpainMapLevel.Level3: goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level3); break;
            case SpainMapLevel.Level4: goalCorrectRounds = Mathf.Max(1, goalCorrectRounds_Level4); break;
        }

        switch (currentLevel)
        {
            case SpainMapLevel.Level1:
            case SpainMapLevel.Level2:
            case SpainMapLevel.Level3:
                useChronometer = true;
                useTimeBar = false;
                break;

            case SpainMapLevel.Level4:
                useChronometer = false;
                useTimeBar = true;
                break;
        }
    }

    private void SetupUI()
    {
        if (textMinijuegoTitulo != null) textMinijuegoTitulo.text = "Geografía de España";
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

        ApplyVisibilityForCurrentLevel();
    }

    private void ApplyVisibilityForCurrentLevel()
    {
        bool isLevel1 = (currentLevel == SpainMapLevel.Level1);

        if (imageMapCommunities != null) imageMapCommunities.gameObject.SetActive(isLevel1);
        if (imageMapProvinces != null) imageMapProvinces.gameObject.SetActive(!isLevel1);

        if (buttonsCommunitiesRoot != null) buttonsCommunitiesRoot.SetActive(isLevel1);
        if (buttonsProvincesRoot != null) buttonsProvincesRoot.SetActive(!isLevel1);
    }

    private void CacheAndHookAllRegionButtons()
    {
        allRegionButtons.Clear();

        if (buttonsCommunitiesRoot != null)
        {
            var regions = buttonsCommunitiesRoot.GetComponentsInChildren<SpainMapRegionButton>(true);
            foreach (var r in regions)
            {
                r.SetManager(this);
                allRegionButtons.Add(r);
            }
        }

        if (buttonsProvincesRoot != null)
        {
            var regions = buttonsProvincesRoot.GetComponentsInChildren<SpainMapRegionButton>(true);
            foreach (var r in regions)
            {
                r.SetManager(this);
                allRegionButtons.Add(r);
            }
        }
    }

    private void ResetAllRegionTints()
    {
        for (int i = 0; i < allRegionButtons.Count; i++)
            if (allRegionButtons[i] != null) allRegionButtons[i].ResetTint();
    }

    // =========================================================
    //  RONDAS
    // =========================================================
    private void StartNewRound()
    {
        roundLocked = false;
        roundErrors = 0;

        ApplyVisibilityForCurrentLevel();
        ResetAllRegionTints();

        if (textFeedback != null) textFeedback.text = "";

        if (useTimeBar) StartTimeBar();

        if (currentLevel == SpainMapLevel.Level1)
        {
            targetCommunity = GetRandomCommunityFiltered();
            if (textInstruction != null)
                textInstruction.text = $"¿Dónde está {PrettyCommunity(targetCommunity)}?";
        }
        else if (currentLevel == SpainMapLevel.Level2)
        {
            targetProvince = GetRandomProvinceFiltered();
            hintCommunityForLevel2 = provinceToCommunity[targetProvince];

            if (textInstruction != null)
                textInstruction.text =
                    $"Sabiendo que {PrettyProvince(targetProvince)} está en {PrettyCommunity(hintCommunityForLevel2)}, ¿dónde está {PrettyProvince(targetProvince)}?";
        }
        else // Level3 y Level4
        {
            targetProvince = GetRandomProvinceFiltered();
            if (textInstruction != null)
                textInstruction.text = $"¿Dónde está {PrettyProvince(targetProvince)}?";
        }
    }

    private SpainCommunity GetRandomCommunityFiltered()
    {
        var values = (SpainCommunity[])System.Enum.GetValues(typeof(SpainCommunity));

        if (!excludeSmallCommunities)
            return values[Random.Range(0, values.Length)];

        List<SpainCommunity> valid = new List<SpainCommunity>();
        for (int i = 0; i < values.Length; i++)
        {
            if (!excludedCommunities.Contains(values[i]))
                valid.Add(values[i]);
        }

        if (valid.Count == 0)
        {
            Debug.LogError("[SpainMap] No hay comunidades válidas tras el filtrado.");
            return values[0];
        }

        return valid[Random.Range(0, valid.Count)];
    }

    private SpainProvince GetRandomProvinceFiltered()
    {
        var values = (SpainProvince[])System.Enum.GetValues(typeof(SpainProvince));

        if (!excludeSmallProvinces)
            return values[Random.Range(0, values.Length)];

        List<SpainProvince> valid = new List<SpainProvince>();
        for (int i = 0; i < values.Length; i++)
        {
            if (!excludedProvinces.Contains(values[i]))
                valid.Add(values[i]);
        }

        if (valid.Count == 0)
        {
            Debug.LogError("[SpainMap] No hay provincias válidas tras el filtrado.");
            return values[0];
        }

        return valid[Random.Range(0, valid.Count)];
    }

    // =========================================================
    //  CLICK REGIÓN
    // =========================================================
    public void OnRegionClicked(SpainMapRegionButton region)
    {
        if (region == null) return;
        if (isPaused) return;
        if (roundLocked) return;

        if (currentLevel == SpainMapLevel.Level1)
        {
            if (region.kind != SpainMapRegionButton.RegionKind.Community) return;

            bool correct = (region.communityId == targetCommunity);
            ResolveAnswer(region, correct);
        }
        else
        {
            if (region.kind != SpainMapRegionButton.RegionKind.Province) return;

            bool correct = (region.provinceId == targetProvince);
            ResolveAnswer(region, correct);
        }
    }

    private void ResolveAnswer(SpainMapRegionButton clickedRegion, bool isCorrect)
    {
        if (isCorrect)
        {
            if (GameSessionManager.I != null)
                GameSessionManager.I.AddCorrect(firstTry: roundErrors == 0);

            correctRoundsThisAttempt++;
            roundErrors = 0;

            if (textFeedback != null) textFeedback.text = "¡Muy bien!";
            clickedRegion.TintForFeedback(Color.green, correctHoldSeconds);

            roundLocked = true;

            if (correctRoundsThisAttempt >= goalCorrectRounds)
                StartCoroutine(WinCoroutine());
            else
                StartCoroutine(CorrectThenTransitionCoroutine());
        }
        else
        {
            if (GameSessionManager.I != null)
                GameSessionManager.I.AddError();

            roundErrors++;

            if (textFeedback != null) textFeedback.text = "Casi. Prueba otra vez.";
            clickedRegion.SetTintPersistent(Color.red); // 🔥 rojo fijo, no parpadea
        }
    }

    private IEnumerator CorrectThenTransitionCoroutine()
    {
        yield return new WaitForSeconds(correctHoldSeconds);

        if (roundTransitionFlashOverlay != null)
            yield return StartCoroutine(WhiteFlashCoroutine());

        StartNewRound();
    }

    private IEnumerator WhiteFlashCoroutine()
    {
        float d = Mathf.Max(0.05f, transitionFlashSeconds);
        float t = 0f;

        // Fade in
        while (t < d)
        {
            t += Time.deltaTime;
            SetOverlayAlpha(Mathf.Lerp(0f, 0.9f, t / d));
            yield return null;
        }

        // Fade out
        t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            SetOverlayAlpha(Mathf.Lerp(0.9f, 0f, t / d));
            yield return null;
        }

        SetOverlayAlpha(0f);
    }

    private void SetOverlayAlpha(float a)
    {
        if (roundTransitionFlashOverlay == null) return;
        Color c = roundTransitionFlashOverlay.color;
        c.a = a;
        roundTransitionFlashOverlay.color = c;
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
        if (textFeedback != null) textFeedback.text = "Se acabó el tiempo. Seguimos.";

        // Mecánica original: no termina el juego, solo penaliza y pasa de ronda.
        if (GameSessionManager.I != null)
            GameSessionManager.I.AddError();

        roundErrors++;
        roundLocked = true;
        StartCoroutine(CorrectThenTransitionCoroutine());
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
        Time.timeScale = 1f;
        isPaused = false;

        EndAttemptInSystem(completed: false);

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
        else
            Debug.LogError("[SpainMap] 'hubSceneName' no está configurado.");
    }

    // =========================================================
    //  INTEGRACIÓN CON GAMESESSIONMANAGER (intentos + guardado)
    // =========================================================
    private void LoadFromGameSessionManagerOrFallback()
    {
        if (GameSessionManager.I != null && GameSessionManager.I.profile != null)
        {
            var selection = GameSessionManager.I.currentSelection;
            currentLevel = (SpainMapLevel)(int)selection.level;
        }
        else
        {
            currentLevel = (SpainMapLevel)(int)fallbackLevelForTesting;
        }
    }

    private void BeginAttemptInSystem()
    {
        if (GameSessionManager.I == null) return;
        if (attemptEnded) return;

        // En este juego la barra del nivel 4 es por ronda, no por intento.
        // Así que dejamos timeLimitSeconds=0 (no aplica a intento).
        GameSessionManager.I.BeginAttempt(timeLimitSeconds: 0f);
    }

    private void EndAttemptInSystem(bool completed)
    {
        if (attemptEnded) return;
        if (GameSessionManager.I == null) return;
        if (GameSessionManager.I.currentAttempt == null) return;

        // Guardamos tiempo del intento:
        // - Niveles 1-3: el cronómetro del HUD
        // - Nivel 4: tiempo total de intento (no la barra por ronda)
        if (useChronometer)
            GameSessionManager.I.currentAttempt.timeSeconds = chronometerTime;
        else
            GameSessionManager.I.currentAttempt.timeSeconds = attemptTotalTime;

        GameSessionManager.I.EndAttempt(completed);
        attemptEnded = true;
    }

    private IEnumerator WinCoroutine()
    {
        chronometerRunning = false;
        timeBarRunning = false;
        roundLocked = true;

        if (textFeedback != null) textFeedback.text = "¡Nivel completado!";
        yield return new WaitForSeconds(1.0f);

        EndAttemptInSystem(completed: true);
        GoToResults();
    }

    // =========================================================
    //  MAPEOS PROVINCIA -> COMUNIDAD
    // =========================================================
    private void BuildProvinceMappings()
    {
        provinceToCommunity = new Dictionary<SpainProvince, SpainCommunity>
        {
            // Andalucía
            { SpainProvince.Almeria, SpainCommunity.Andalucia },
            { SpainProvince.Cadiz, SpainCommunity.Andalucia },
            { SpainProvince.Cordoba, SpainCommunity.Andalucia },
            { SpainProvince.Granada, SpainCommunity.Andalucia },
            { SpainProvince.Huelva, SpainCommunity.Andalucia },
            { SpainProvince.Jaen, SpainCommunity.Andalucia },
            { SpainProvince.Malaga, SpainCommunity.Andalucia },
            { SpainProvince.Sevilla, SpainCommunity.Andalucia },

            // Aragón
            { SpainProvince.Huesca, SpainCommunity.Aragon },
            { SpainProvince.Teruel, SpainCommunity.Aragon },
            { SpainProvince.Zaragoza, SpainCommunity.Aragon },

            // Asturias
            { SpainProvince.Asturias, SpainCommunity.Asturias },

            // Baleares
            { SpainProvince.Baleares, SpainCommunity.Baleares },

            // Canarias
            { SpainProvince.LasPalmas, SpainCommunity.Canarias },
            { SpainProvince.SantaCruzDeTenerife, SpainCommunity.Canarias },

            // Cantabria
            { SpainProvince.Cantabria, SpainCommunity.Cantabria },

            // Castilla-La Mancha
            { SpainProvince.Albacete, SpainCommunity.CastillaLaMancha },
            { SpainProvince.CiudadReal, SpainCommunity.CastillaLaMancha },
            { SpainProvince.Cuenca, SpainCommunity.CastillaLaMancha },
            { SpainProvince.Guadalajara, SpainCommunity.CastillaLaMancha },
            { SpainProvince.Toledo, SpainCommunity.CastillaLaMancha },

            // Castilla y León
            { SpainProvince.Avila, SpainCommunity.CastillaYLeon },
            { SpainProvince.Burgos, SpainCommunity.CastillaYLeon },
            { SpainProvince.Leon, SpainCommunity.CastillaYLeon },
            { SpainProvince.Palencia, SpainCommunity.CastillaYLeon },
            { SpainProvince.Salamanca, SpainCommunity.CastillaYLeon },
            { SpainProvince.Segovia, SpainCommunity.CastillaYLeon },
            { SpainProvince.Soria, SpainCommunity.CastillaYLeon },
            { SpainProvince.Valladolid, SpainCommunity.CastillaYLeon },
            { SpainProvince.Zamora, SpainCommunity.CastillaYLeon },

            // Cataluña
            { SpainProvince.Barcelona, SpainCommunity.Cataluna },
            { SpainProvince.Girona, SpainCommunity.Cataluna },
            { SpainProvince.Lleida, SpainCommunity.Cataluna },
            { SpainProvince.Tarragona, SpainCommunity.Cataluna },

            // Comunidad Valenciana
            { SpainProvince.Alicante, SpainCommunity.ComunidadValenciana },
            { SpainProvince.Castellon, SpainCommunity.ComunidadValenciana },
            { SpainProvince.Valencia, SpainCommunity.ComunidadValenciana },

            // Extremadura
            { SpainProvince.Badajoz, SpainCommunity.Extremadura },
            { SpainProvince.Caceres, SpainCommunity.Extremadura },

            // Galicia
            { SpainProvince.ACoruna, SpainCommunity.Galicia },
            { SpainProvince.Lugo, SpainCommunity.Galicia },
            { SpainProvince.Ourense, SpainCommunity.Galicia },
            { SpainProvince.Pontevedra, SpainCommunity.Galicia },

            // La Rioja
            { SpainProvince.LaRioja, SpainCommunity.LaRioja },

            // Madrid
            { SpainProvince.Madrid, SpainCommunity.Madrid },

            // Murcia
            { SpainProvince.Murcia, SpainCommunity.Murcia },

            // Navarra
            { SpainProvince.Navarra, SpainCommunity.Navarra },

            // País Vasco
            { SpainProvince.Alava, SpainCommunity.PaisVasco },
            { SpainProvince.Guipuzcoa, SpainCommunity.PaisVasco },
            { SpainProvince.Vizcaya, SpainCommunity.PaisVasco },

            // Ceuta/Melilla
            { SpainProvince.Ceuta, SpainCommunity.Ceuta },
            { SpainProvince.Melilla, SpainCommunity.Melilla },
        };
    }

    // =========================================================
    //  NOMBRES BONITOS
    // =========================================================
    private string PrettyCommunity(SpainCommunity c)
    {
        switch (c)
        {
            case SpainCommunity.CastillaLaMancha: return "Castilla-La Mancha";
            case SpainCommunity.CastillaYLeon: return "Castilla y León";
            case SpainCommunity.ComunidadValenciana: return "Comunidad Valenciana";
            case SpainCommunity.PaisVasco: return "País Vasco";
            case SpainCommunity.LaRioja: return "La Rioja";
            case SpainCommunity.Cataluna: return "Cataluña";
            default: return c.ToString();
        }
    }

    private string PrettyProvince(SpainProvince p)
    {
        switch (p)
        {
            case SpainProvince.ACoruna: return "A Coruña";
            case SpainProvince.CiudadReal: return "Ciudad Real";
            case SpainProvince.SantaCruzDeTenerife: return "Santa Cruz de Tenerife";
            case SpainProvince.Guipuzcoa: return "Guipúzcoa";
            case SpainProvince.Alava: return "Álava";
            default: return p.ToString();
        }
    }

    private const string resultsSceneName = "05_Results";

    private void GoToResults()
    {
        Time.timeScale = 1f;
        isPaused = false;
        SceneManager.LoadScene(resultsSceneName);
    }
}

