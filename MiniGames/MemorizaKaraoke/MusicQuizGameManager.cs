using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum MusicQuizLevel
{
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
    Level4 = 4
}

public class MusicQuizGameManager : MonoBehaviour
{
    // =========================================================
    //  LEVEL (CleverMinds)
    // =========================================================
    [Header("Level Config")]
    [Tooltip("Solo testing si NO existe GameSessionManager en escena.")]
    [SerializeField] private LevelId fallbackLevelForTesting = LevelId.Level1;

    [SerializeField] private MusicQuizLevel currentLevel = MusicQuizLevel.Level1;

    private int optionsCount;
    private bool useChronometer;
    private bool useTimeBar;

    // =========================================================
    //  DATA
    // =========================================================
    [Header("Songs")]
    [Tooltip("Lista de canciones disponibles. Se elige 1 aleatoria al empezar la partida.")]
    [SerializeField] private List<MusicQuizSongData> songs = new List<MusicQuizSongData>();

    [Tooltip("(Legacy) Si no rellenas 'songs', se usará este único SongData.")]
    [SerializeField] private MusicQuizSongData song;

    private MusicQuizSongData activeSong;
    private int lastRandomSongIndex = -1;

    // =========================================================
    //  AUDIO
    // =========================================================
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    // =========================================================
    //  PANELS
    // =========================================================
    [Header("Start Panel")]
    [SerializeField] private GameObject startPanel;

    [Header("Karaoke Panel")]
    [SerializeField] private GameObject karaokePanel;
    [SerializeField] private KaraokeLyricsController karaokeController;

    [Header("Question Panel")]
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private TextMeshProUGUI questionText;
    [SerializeField] private Button replayButton;

    // =========================================================
    //  OPTIONS (paneles 2/3/4 ya escalados)
    // =========================================================
    [System.Serializable]
    public class OptionsPanel
    {
        public GameObject root;
        public MusicQuizOptionButtonController[] optionButtons;
    }

    [Header("Options Panels (2/3/4)")]
    [SerializeField] private OptionsPanel options2;
    [SerializeField] private OptionsPanel options3;
    [SerializeField] private OptionsPanel options4;

    private OptionsPanel activeOptionsPanel;

    // =========================================================
    //  TIMERS (como WeddingFaces)
    // =========================================================
    [Header("Chronometer (Level 1-3)")]
    [SerializeField] private GameObject chronometerContainer;
    [SerializeField] private TextMeshProUGUI chronometerText;

    [Header("Time Bar (Level 4)")]
    [SerializeField] private GameObject timeBarContainer;
    [SerializeField] private Image timeBarFill;
    [SerializeField] private float timeLimitSeconds = 8f;

    private float chronometerTime;
    private float timeRemaining;
    private bool chronometerRunning;
    private bool timeBarRunning;

    // =========================================================
    //  PAUSE
    // =========================================================
    [Header("Pause")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private string hubSceneName = "03_MinigameHub";
    private bool isPaused;

    // =========================================================
    //  STATE
    // =========================================================
    private bool waitingForAnswer;
    private bool finished;
    private int attemptsRemaining;
    private bool firstTry = true;

    // ✅ Integración: evitar dobles EndAttempt (timeout/click/corutinas)
    private bool attemptEnded = false;

    // =========================================================
    //  UNITY
    // =========================================================
    private void Awake()
    {
        LoadFromGameSessionManagerOrFallback();
    }

    private void Start()
    {
        attemptEnded = false;

        // ✅ Elegimos canción antes del flow
        PickRandomSong();

        ApplyLevelConfig();

        // Si aún no usas StartPanel, arranca directo.
        // Si más adelante lo asignas, automáticamente hará el flow “Start → Karaoke”.
        if (startPanel != null)
        {
            ShowStartPanel();
        }
        else
        {
            StartKaraokePhase();
        }
    }

    // =========================================================
    //  SINGLETON / PERFIL / NIVEL
    // =========================================================
    private void LoadFromGameSessionManagerOrFallback()
    {
        if (GameSessionManager.I != null)
        {
            currentLevel = (MusicQuizLevel)GameSessionManager.I.currentSelection.level;

            // ✅ (Opcional) Nivel 4 lee el límite desde Tuning.targetTimeSeconds
            if (currentLevel == MusicQuizLevel.Level4)
            {
                var tuning = GameSessionManager.I.GetTuning();
                if (tuning != null && tuning.targetTimeSeconds > 0.1f)
                    timeLimitSeconds = tuning.targetTimeSeconds;
            }
        }
        else
        {
            currentLevel = (MusicQuizLevel)fallbackLevelForTesting;
        }
    }

    private void PickRandomSong()
    {
        // Si hay lista válida, elegimos una canción aleatoria.
        // Si no, caemos al campo legacy 'song'.
        if (songs != null && songs.Count > 0)
        {
            // intenta evitar repetir la misma canción consecutivamente (si hay +1)
            int idx = Random.Range(0, songs.Count);
            if (songs.Count > 1 && idx == lastRandomSongIndex)
            {
                idx = (idx + 1) % songs.Count;
            }

            lastRandomSongIndex = idx;
            activeSong = songs[idx];
        }
        else
        {
            activeSong = song;
        }

        if (activeSong == null)
        {
            Debug.LogError("[MusicQuiz] No hay ninguna MusicQuizSongData asignada. Rellena 'songs' o 'song'.");
        }
    }

    private void Update()
    {
        // timers estilo WeddingFaces
        if (!isPaused)
        {
            if (chronometerRunning)
            {
                chronometerTime += Time.deltaTime;
                UpdateChronometerUI();
            }

            if (timeBarRunning)
            {
                timeRemaining -= Time.deltaTime;
                UpdateTimeBarUI();

                if (timeRemaining <= 0f)
                {
                    timeRemaining = 0f;
                    UpdateTimeBarUI();
                    // se acabó el tiempo -> fallo final
                    StartCoroutine(FailByTimeOut());
                }
            }
        }

        // métricas (si existe)
        if (GameSessionManager.I != null && GameSessionManager.I.currentAttempt != null && !attemptEnded)
            GameSessionManager.I.TickTime(Time.deltaTime);
    }

    // =========================================================
    //  LEVEL CONFIG
    // =========================================================
    private void ApplyLevelConfig()
    {
        optionsCount = currentLevel switch
        {
            MusicQuizLevel.Level1 => 2,
            MusicQuizLevel.Level2 => 3,
            _ => 4
        };

        useChronometer = (currentLevel == MusicQuizLevel.Level1 ||
                          currentLevel == MusicQuizLevel.Level2 ||
                          currentLevel == MusicQuizLevel.Level3);

        useTimeBar = (currentLevel == MusicQuizLevel.Level4);
    }

    // =========================================================
    //  START FLOW
    // =========================================================
    private void ShowStartPanel()
    {
        finished = false;
        waitingForAnswer = false;

        if (startPanel) startPanel.SetActive(true);
        if (karaokePanel) karaokePanel.SetActive(false);
        if (questionPanel) questionPanel.SetActive(false);

        SetOptionsPanelActive(null);

        StopAllTimersUI();
    }

    // ✅ Conecta el botón Start del panel a este método
    public void OnStartPressed()
    {
        if (startPanel) startPanel.SetActive(false);

        StartKaraokePhase();
    }

    // =========================================================
    //  KARAOKE PHASE
    // =========================================================
    private void StartKaraokePhase()
    {
        if (karaokePanel) karaokePanel.SetActive(true);
        if (questionPanel) questionPanel.SetActive(false);

        // Audio karaoke normal
        if (audioSource && activeSong && activeSong.karaokeClip)
        {
            audioSource.Stop();
            audioSource.clip = activeSong.karaokeClip;
            audioSource.time = 0f;
            audioSource.Play();
        }

        if (karaokeController && audioSource)
        {
            karaokeController.Init(activeSong, audioSource);
            karaokeController.StartKaraoke();
        }

        // Pasar a pregunta cuando termine el audio (Invoke se congela con pausa si timeScale=0)
        if (activeSong != null && activeSong.karaokeClip != null)
            Invoke(nameof(StartQuestionPhase), activeSong.karaokeClip.length);
    }

    // =========================================================
    //  QUESTION PHASE
    // =========================================================
    private void StartQuestionPhase()
    {
        if (finished) return;

        if (karaokeController) karaokeController.StopKaraoke();
        if (karaokePanel) karaokePanel.SetActive(false);
        if (questionPanel) questionPanel.SetActive(true);

        if (questionText) questionText.text = activeSong ? activeSong.questionText : "Pregunta";

        // Replay solo L1/L2 (y en L3/L4 NO)
        bool allowReplay = (currentLevel == MusicQuizLevel.Level1 || currentLevel == MusicQuizLevel.Level2);
        if (replayButton)
        {
            replayButton.gameObject.SetActive(allowReplay);
            replayButton.interactable = allowReplay;
        }

        // Panel correcto
        activeOptionsPanel = optionsCount switch
        {
            2 => options2,
            3 => options3,
            _ => options4
        };
        SetOptionsPanelActive(activeOptionsPanel);

        // intentos como WeddingFaces: (3 opciones => 2 intentos, 2/4 opciones => 3 intentos)
        attemptsRemaining = (optionsCount == 3) ? 2 : 3;
        firstTry = true;

        SetupOptions();

        waitingForAnswer = true;
        finished = false;

        BeginAttemptInSystem(); // ✅ el intento empieza cuando ARRANCA la fase de respuesta
        StartTimers();          // ✅ empieza cronómetro o timebar al arrancar la fase de respuesta
        PlaySilentQuestionClip();
    }

    private void StartTimers()
    {
        chronometerRunning = false;
        timeBarRunning = false;

        if (chronometerContainer) chronometerContainer.SetActive(useChronometer);
        if (timeBarContainer) timeBarContainer.SetActive(useTimeBar);

        if (useChronometer)
        {
            chronometerTime = 0f;
            chronometerRunning = true;
            UpdateChronometerUI();
        }

        if (useTimeBar)
        {
            timeRemaining = timeLimitSeconds;
            timeBarRunning = true;
            UpdateTimeBarUI();
        }
    }

    private void StopAllTimersUI()
    {
        chronometerRunning = false;
        timeBarRunning = false;

        if (chronometerContainer) chronometerContainer.SetActive(false);
        if (timeBarContainer) timeBarContainer.SetActive(false);
    }

    private void UpdateChronometerUI()
    {
        if (chronometerText == null) return;

        int totalSeconds = Mathf.FloorToInt(chronometerTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        chronometerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void UpdateTimeBarUI()
    {
        if (timeBarFill == null) return;
        float k = (timeLimitSeconds <= 0f) ? 0f : Mathf.Clamp01(timeRemaining / timeLimitSeconds);
        timeBarFill.fillAmount = k;
    }

    // =========================================================
    //  OPTIONS
    // =========================================================
    private void SetupOptions()
    {
        if (activeOptionsPanel == null || activeOptionsPanel.optionButtons == null)
        {
            Debug.LogError("[MusicQuiz] Falta panel de opciones o botones.");
            return;
        }

        // Pool: correcta + distractores
        List<string> pool = new();
        if (activeSong != null)
        {
            pool.Add(activeSong.correctAnswer);

            if (activeSong.wrongAnswers != null)
            {
                foreach (var w in activeSong.wrongAnswers)
                    if (!string.IsNullOrWhiteSpace(w) && !pool.Contains(w))
                        pool.Add(w);
            }
        }

        while (pool.Count < optionsCount) pool.Add("—");

        // Elegimos optionsCount (garantizando correcta) y mezclamos
        List<string> chosen = new();
        if (activeSong != null) chosen.Add(activeSong.correctAnswer);

        for (int i = 0; i < pool.Count && chosen.Count < optionsCount; i++)
        {
            if (activeSong != null && pool[i] == activeSong.correctAnswer) continue;
            chosen.Add(pool[i]);
        }

        Shuffle(chosen);

        // Pintar y bindear
        for (int i = 0; i < activeOptionsPanel.optionButtons.Length; i++)
        {
            bool active = i < optionsCount;
            activeOptionsPanel.optionButtons[i].gameObject.SetActive(active);

            if (!active) continue;

            var btn = activeOptionsPanel.optionButtons[i];
            btn.Bind(this);

            string ans = chosen[i];
            bool correct = (activeSong != null && ans == activeSong.correctAnswer);
            btn.Setup(ans, correct);
        }
    }

    public void OnOptionSelected(MusicQuizOptionButtonController btn)
    {
        if (isPaused) return;
        if (!waitingForAnswer || finished) return;
        if (btn == null) return;

        if (btn.IsCorrect())
        {
            btn.MarkCorrect();
            Finish(success: true);
            return;
        }

        // -------------------------------
        // FALLO
        // -------------------------------
        btn.MarkWrong();

        // desactivar para que no repita la misma
        var uib = btn.GetComponent<Button>();
        if (uib != null) uib.interactable = false;

        if (GameSessionManager.I != null)
            GameSessionManager.I.AddError();

        firstTry = false;

        // ✅ NUEVO: En Nivel 1, si fallas, ya no hay "más opciones" (comportamiento como pides)
        // -> enseñamos la correcta parpadeando en verde y terminamos como derrota.
        if (currentLevel == MusicQuizLevel.Level1)
        {
            attemptsRemaining = 0;
            StartCoroutine(BlinkCorrectAndFinish());
            return;
        }

        // (igual que antes para Level 2-4)
        attemptsRemaining--;

        if (attemptsRemaining > 0)
            return;

        // último intento fallado -> parpadea el correcto en verde
        StartCoroutine(BlinkCorrectAndFinish());
    }

    private IEnumerator BlinkCorrectAndFinish()
    {
        waitingForAnswer = false;
        finished = true;
        chronometerRunning = false;
        timeBarRunning = false;

        // encontrar correcto en el panel activo
        MusicQuizOptionButtonController correctBtn = null;
        foreach (var b in activeOptionsPanel.optionButtons)
            if (b != null && b.gameObject.activeSelf && b.IsCorrect())
                correctBtn = b;

        if (correctBtn != null)
            yield return correctBtn.BlinkCorrect();

        yield return new WaitForSecondsRealtime(0.4f);

        Finish(success: false);
    }

    private IEnumerator FailByTimeOut()
    {
        if (finished) yield break;

        finished = true;
        waitingForAnswer = false;
        chronometerRunning = false;
        timeBarRunning = false;

        // blink correcto
        MusicQuizOptionButtonController correctBtn = null;
        foreach (var b in activeOptionsPanel.optionButtons)
            if (b != null && b.gameObject.activeSelf && b.IsCorrect())
                correctBtn = b;

        if (correctBtn != null)
            yield return correctBtn.BlinkCorrect();

        yield return new WaitForSecondsRealtime(0.4f);

        Finish(false);
    }

    private void Finish(bool success)
    {
        CancelInvoke(nameof(StartQuestionPhase));
        chronometerRunning = false;
        timeBarRunning = false;

        // ✅ Cerrar intento / guardar (igual que antes)
        EndAttemptInSystem(success, firstTry);

        // ✅ Seguridad por si estaba pausado
        Time.timeScale = 1f;
        isPaused = false;
        if (pausePanel) pausePanel.SetActive(false);

        // ✅ Volver al hub de minijuegos
        if (!string.IsNullOrEmpty(resultsSceneName))
            GoToResults();
        else
            SceneManager.LoadScene("05_Results"); // fallback seguro
    }

    // =========================================================
    //  INTEGRACIÓN CON GAMESESSIONMANAGER (intentos + guardado)
    // =========================================================
    private void BeginAttemptInSystem()
    {
        if (attemptEnded) return;
        if (GameSessionManager.I == null) return;

        float limit = (currentLevel == MusicQuizLevel.Level4) ? Mathf.Max(0.1f, timeLimitSeconds) : 0f;
        GameSessionManager.I.BeginAttempt(limit);
    }

    private void EndAttemptInSystem(bool completed, bool wasFirstTry)
    {
        if (attemptEnded) return;
        if (GameSessionManager.I == null) return;
        if (GameSessionManager.I.currentAttempt == null) return;

        if (completed)
            GameSessionManager.I.AddCorrect(wasFirstTry);

        // Asegurar tiempo final consistente (como WeddingFaces)
        if (useChronometer)
            GameSessionManager.I.currentAttempt.timeSeconds = chronometerTime;

        GameSessionManager.I.EndAttempt(completed);
        attemptEnded = true;
    }

    // =========================================================
    //  REPLAY
    // =========================================================
    public void OnReplayPressed()
    {
        if (!(currentLevel == MusicQuizLevel.Level1 || currentLevel == MusicQuizLevel.Level2)) return;
        PlaySilentQuestionClip();
    }

    private void PlaySilentQuestionClip()
    {
        if (audioSource == null || activeSong == null || activeSong.questionSilentClip == null) return;
        audioSource.Stop();
        audioSource.clip = activeSong.questionSilentClip;
        audioSource.time = 0f;
        audioSource.Play();
    }

    // =========================================================
    //  PAUSE
    // =========================================================
    public void TogglePause()
    {
        isPaused = !isPaused;

        if (pausePanel) pausePanel.SetActive(isPaused);

        Time.timeScale = isPaused ? 0f : 1f;

        if (audioSource)
        {
            if (isPaused) audioSource.Pause();
            else audioSource.UnPause();
        }
    }

    public void ResumeFromPause()
    {
        isPaused = false;
        if (pausePanel) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        if (audioSource) audioSource.UnPause();
        if (karaokePanel != null && karaokePanel.activeSelf && karaokeController != null)
            karaokeController.StartKaraoke();
    }

    public void ExitToHub()
    {
        // Si el jugador se sale a mitad, cerramos intento como NO completado (si existe)
        EndAttemptInSystem(completed: false, wasFirstTry: false);

        Time.timeScale = 1f;
        isPaused = false;

        if (!string.IsNullOrEmpty(hubSceneName))
            SceneManager.LoadScene(hubSceneName);
        else
            Debug.LogError("[MusicQuiz] 'hubSceneName' no está configurado.");
    }

    // =========================================================
    //  HELPERS
    // =========================================================
    private void SetOptionsPanelActive(OptionsPanel active)
    {
        if (options2.root) options2.root.SetActive(active != null && active.root == options2.root);
        if (options3.root) options3.root.SetActive(active != null && active.root == options3.root);
        if (options4.root) options4.root.SetActive(active != null && active.root == options4.root);
    }

    private void Shuffle(List<string> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
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

