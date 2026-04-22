// Assets/Scripts/Core/GameSessionManager.cs
using UnityEngine;
using System;
using System.Collections.Generic;

public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager I { get; private set; }

    [Header("Configs por minijuego (ScriptableObjects)")]
    public List<MiniGameConfig> configs = new();
    private Dictionary<int, MiniGameConfig> _byId = new();

    [Header("Estado de sesión")]
    public MiniGameSelection currentSelection;
    public AttemptMetrics currentAttempt;

    [Header("Último resultado (para 05_Results)")]
    public LastLevelResult lastResult;

    public PlayerProfile profile => UserDirectoryService.I != null ? UserDirectoryService.I.CurrentProfile : null;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        IndexConfigs();
    }

    private void IndexConfigs()
    {
        _byId.Clear();
        foreach (var c in configs)
        {
            if (c == null) continue;
            _byId[(int)c.miniGameId] = c;
        }
    }

    public void SelectMiniGameAndLevel(MiniGameId mg, LevelId lv)
    {
        currentSelection = new MiniGameSelection { miniGame = mg, level = lv };
    }

    public MiniGameConfig GetConfig()
    {
        int key = (int)currentSelection.miniGame;
        if (!_byId.TryGetValue(key, out var cfg) || cfg == null)
        {
            Debug.LogError($"[GSM] No hay MiniGameConfig asignado para {currentSelection.miniGame}");
            return null;
        }
        return cfg;
    }

    public MiniGameConfig.LevelTuning GetTuning()
    {
        var cfg = GetConfig();
        if (cfg == null) return null;

        var t = cfg.Get(currentSelection.level);
        if (t == null) Debug.LogError($"[GSM] Falta LevelTuning para {currentSelection.level} en {cfg.name}.");
        return t;
    }

    // ========================= Progreso matrioska =========================
    private MiniGameProgress GetOrCreateMiniGameProgress(string miniGameName)
    {
        if (profile == null)
        {
            Debug.LogError("[GSM] No hay usuario activo (profile)");
            return null;
        }

        if (profile.progress == null)
            profile.progress = new Dictionary<string, MiniGameProgress>();

        if (!profile.progress.TryGetValue(miniGameName, out var mg))
        {
            mg = new MiniGameProgress { levels = new Dictionary<string, LevelProgress>() };
            profile.progress[miniGameName] = mg;
        }

        if (mg.levels == null)
            mg.levels = new Dictionary<string, LevelProgress>();

        return mg;
    }

    private LevelProgress GetOrCreateLevelProgress(MiniGameProgress mg, string levelKey)
    {
        if (!mg.levels.TryGetValue(levelKey, out var lp))
        {
            lp = new LevelProgress
            {
                bestScore = 0f,
                bestTimeSeconds = 0f,
                attempts = new List<AttemptMetrics>()
            };
            mg.levels[levelKey] = lp;
        }

        if (lp.attempts == null) lp.attempts = new List<AttemptMetrics>();
        return lp;
    }

    // ========================= Attempt API =========================
    public void BeginAttempt(float timeLimitSeconds = 0f)
    {
        if (profile == null)
        {
            Debug.LogError("[GSM] BeginAttempt: no hay usuario logueado.");
            return;
        }

        string now = DateTime.UtcNow.ToString("o");
        int levelInt = (int)currentSelection.level;

        currentAttempt = new AttemptMetrics
        {
            attemptId = profile.ConsumeNextAttemptId("a"),
            startedAtUtc = now,
            endedAtUtc = "",

            level = levelInt,
            completed = false,

            timeSeconds = 0f,
            timeLimitSeconds = timeLimitSeconds,

            correct = 0,
            errors = 0,
            firstTryCorrect = 0,

            paymentItemsUsed = 0,

            // Snapshot accesibilidad: con qué perfil se jugó este intento
            playedWithVisionIssues = profile.hasVisionIssues,
            playedWithHearingIssues = profile.hasHearingIssues,

            score = 0f
        };
    }

    public void TickTime(float delta)
    {
        if (currentAttempt != null) currentAttempt.timeSeconds += delta;
    }

    public void AddCorrect(bool firstTry)
    {
        if (currentAttempt == null) return;
        currentAttempt.correct++;
        if (firstTry) currentAttempt.firstTryCorrect++;
    }

    public void AddError()
    {
        if (currentAttempt == null) return;
        currentAttempt.errors++;
    }

    // PagoExacto: cada moneda/billete usado
    public void AddPaymentItemUsed(int count = 1)
    {
        if (currentAttempt == null) return;
        if (count < 0) count = 0;
        currentAttempt.paymentItemsUsed += count;
    }

    public void EndAttempt(bool completed)
    {
        if (currentAttempt == null || profile == null) return;

        string now = DateTime.UtcNow.ToString("o");
        currentAttempt.completed = completed;
        currentAttempt.endedAtUtc = now;

        // Regla cerrada: timeout nivel 4 => timeSeconds = timeLimitSeconds
        if ((LevelId)currentAttempt.level == LevelId.Level4 && !completed)
        {
            if (currentAttempt.timeLimitSeconds > 0f)
                currentAttempt.timeSeconds = currentAttempt.timeLimitSeconds;
        }

        var cfg = GetConfig();
        var tuning = GetTuning();

        currentAttempt.score = (cfg != null && tuning != null)
            ? Scoring.Compute(cfg, tuning, currentAttempt)
            : 0f;

        string mgName = currentSelection.miniGame.ToString();
        string levelKey = ((int)currentSelection.level).ToString();

        var mgProg = GetOrCreateMiniGameProgress(mgName);
        var lvlProg = GetOrCreateLevelProgress(mgProg, levelKey);

        // 👉 Para saber si es récord: comparamos contra el bestScore ANTES de actualizarlo
        float prevBestScore = lvlProg.bestScore;
        float prevBestTime = lvlProg.bestTimeSeconds;

        lvlProg.attempts.Add(currentAttempt);

        if (currentAttempt.score > lvlProg.bestScore)
            lvlProg.bestScore = currentAttempt.score;

        if (currentAttempt.completed)
        {
            if (lvlProg.bestTimeSeconds <= 0f || currentAttempt.timeSeconds < lvlProg.bestTimeSeconds)
                lvlProg.bestTimeSeconds = currentAttempt.timeSeconds;
        }

        // 👉 Guardamos “lo último” para la escena 05_Results
        lastResult = new LastLevelResult
        {
            miniGame = currentSelection.miniGame,
            level = currentSelection.level,
            completed = currentAttempt.completed,
            score = currentAttempt.score,
            isNewRecord = currentAttempt.score > prevBestScore,
            timeSeconds = currentAttempt.timeSeconds,
            bestScoreAfter = lvlProg.bestScore,
            bestTimeAfter = lvlProg.bestTimeSeconds
        };

        profile.lastUpdatedUtc = now;
        UserDirectoryService.I?.TouchAndSaveCurrent();
    }

    [Serializable]
    public class LastLevelResult
    {
        public MiniGameId miniGame;
        public LevelId level;
        public bool completed;

        public float score;
        public bool isNewRecord;

        public float timeSeconds;

        // Por si luego quieres mostrar “Tu mejor” en results
        public float bestScoreAfter;
        public float bestTimeAfter;
    }

    public bool IsLevelUnlocked(MiniGameId mg, LevelId level)
    {
        // Nivel 1 siempre desbloqueado
        if (level == LevelId.Level1) return true;

        if (profile == null || profile.progress == null) return false;

        string mgKey = mg.ToString();
        if (!profile.progress.TryGetValue(mgKey, out var mgProg) || mgProg == null || mgProg.levels == null)
            return false;

        int prevLevelInt = ((int)level) - 1;
        string prevKey = prevLevelInt.ToString();

        if (!mgProg.levels.TryGetValue(prevKey, out var prevProg) || prevProg == null || prevProg.attempts == null)
            return false;

        // Desbloquea si el nivel anterior tiene AL MENOS un intento completado
        for (int i = 0; i < prevProg.attempts.Count; i++)
        {
            if (prevProg.attempts[i] != null && prevProg.attempts[i].completed)
                return true;
        }

        return false;
    }
}