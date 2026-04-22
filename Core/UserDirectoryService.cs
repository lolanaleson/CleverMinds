// Assets/Scripts/Core/UserDirectoryService.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

/// <summary>
/// Guarda TODOS los perfiles en un solo JSON en:
/// Application.persistentDataPath/cleverminds_profiles_db.json
///
/// Usamos Json.NET porque JsonUtility NO serializa Dictionary.
/// Esto permite el JSON "matrioska" bonito con claves string.
/// </summary>
public class UserDirectoryService : MonoBehaviour
{
    public static UserDirectoryService I { get; private set; }

    [Header("DB multiusuario")]
    public string dbFileName = "cleverminds_profiles_db.json";

    [Serializable]
    public class ProfilesDB
    {
        public List<PlayerProfile> profiles = new List<PlayerProfile>();
    }

    public ProfilesDB db = new ProfilesDB();

    // Lookup interno: localUserId ("0007") -> perfil
    private readonly Dictionary<string, PlayerProfile> _users = new Dictionary<string, PlayerProfile>();

    public PlayerProfile CurrentProfile { get; private set; }

    private string DbPath => Path.Combine(Application.persistentDataPath, dbFileName);

    private JsonSerializerSettings _jsonSettings;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,

            // IMPORTANTÍSIMO:
            // - Oculta floats = 0 (si tienen [DefaultValue(0f)])
            // - Oculta ints = 0 (si tienen [DefaultValue(0)])
            // - Oculta bools = false (si tienen [DefaultValue(false)])
            // Así: timeLimitSeconds NO sale en niveles 1-3, paymentItemsUsed no sale si no aplica, etc.
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        // Asegurar deviceId desde el inicio (1 vez por instalación)
        DeviceIdentity.EnsureDeviceId();

        LoadDB();
    }

    // =========================
    // Persistencia
    // =========================
    public void LoadDB()
    {
        try
        {
            if (!File.Exists(DbPath))
            {
                db = new ProfilesDB();
                RebuildLookupFromList();
                SaveDB(); // crea el archivo vacío en persistentDataPath
                return;
            }

            string json = File.ReadAllText(DbPath);

            // 1) Intento cargar DB NUEVA (Json.NET)
            ProfilesDB loadedNew = null;
            try
            {
                loadedNew = JsonConvert.DeserializeObject<ProfilesDB>(json, _jsonSettings);
            }
            catch
            {
                // Si revienta, probamos legacy más abajo.
            }

            if (loadedNew != null && loadedNew.profiles != null)
            {
                db = loadedNew;
                RebuildLookupFromList();
                MigrateMissingFieldsIfNeeded();
                SaveDB();
                Debug.Log($"[UserDir] Loaded NEW DB | Users: {_users.Count} | deviceId: {DeviceIdentity.EnsureDeviceId()}");
                return;
            }

            // 2) Si no, intento LEGACY (JsonUtility) y convierto
            var legacy = JsonUtility.FromJson<LegacyProfilesDB>(json);
            if (legacy != null && legacy.profiles != null)
            {
                ConvertLegacyToNew(legacy);
                SaveDB();
                Debug.Log($"[UserDir] Loaded LEGACY DB and migrated | Users: {_users.Count} | deviceId: {DeviceIdentity.EnsureDeviceId()}");
                return;
            }

            // 3) Si no encaja nada, DB vacía segura
            db = new ProfilesDB();
            _users.Clear();
            SaveDB();
        }
        catch (Exception e)
        {
            Debug.LogError($"[UserDir] Error loading DB: {e.Message}");
            db = new ProfilesDB();
            _users.Clear();
        }
    }

    public void SaveDB()
    {
        try
        {
            // Sincroniza lista desde diccionario
            db.profiles.Clear();
            foreach (var kv in _users)
            {
                if (kv.Value != null)
                    db.profiles.Add(kv.Value);
            }

            string json = JsonConvert.SerializeObject(db, _jsonSettings);
            File.WriteAllText(DbPath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UserDir] Error saving DB: {e.Message}");
        }
    }

    private void RebuildLookupFromList()
    {
        _users.Clear();
        if (db == null || db.profiles == null) db = new ProfilesDB();

        foreach (var p in db.profiles)
        {
            if (p == null) continue;

            // Migración: si el perfil antiguo usaba playerId4 como id
            if (string.IsNullOrEmpty(p.localUserId) && !string.IsNullOrEmpty(p.playerId4))
                p.localUserId = NormalizeLocalId(p.playerId4);

            if (!string.IsNullOrEmpty(p.localUserId))
                p.localUserId = NormalizeLocalId(p.localUserId);

            if (string.IsNullOrEmpty(p.localUserId)) continue;

            _users[p.localUserId] = p;
        }
    }

    private void MigrateMissingFieldsIfNeeded()
    {
        string deviceId = DeviceIdentity.EnsureDeviceId();
        string now = DateTime.UtcNow.ToString("o");

        foreach (var kv in _users)
        {
            var p = kv.Value;
            if (p == null) continue;

            // id "user_0007"
            if (string.IsNullOrEmpty(p.id))
                p.id = "user_" + p.localUserId;

            // deviceId por tablet
            if (string.IsNullOrEmpty(p.deviceId))
                p.deviceId = deviceId;

            // globalUserId único
            if (string.IsNullOrEmpty(p.globalUserId))
                p.globalUserId = BuildGlobalUserId(p.deviceId);

            // timestamps
            if (string.IsNullOrEmpty(p.createdAtUtc))
                p.createdAtUtc = now;

            if (string.IsNullOrEmpty(p.lastUpdatedUtc))
                p.lastUpdatedUtc = now;

            // gender string
            if (string.IsNullOrEmpty(p.gender))
                p.gender = "U";

            // progreso
            if (p.progress == null)
                p.progress = new Dictionary<string, MiniGameProgress>();

            // contador attempts
            if (p.nextAttemptNumber <= 0)
                p.nextAttemptNumber = 1;
        }
    }

    // =========================
    // Crear usuario (tu lógica: age -> birthYear)
    // =========================
    public bool CreateUserAutoFromAge(string nickname, int ageYears, Sex sex,
                                      out PlayerProfile profile, out string assignedId, out string error)
    {
        profile = null;
        assignedId = "";
        error = "";

        if (!IsValidNickname(nickname, out error)) return false;
        if (!IsValidAge(ageYears, out error)) return false;

        int currentYear = DateTime.Now.Year;
        int birthYear = currentYear - ageYears;
        if (!IsValidBirthYear(birthYear, out error)) return false;

        string localId = FindFreeLocalId4();
        if (string.IsNullOrEmpty(localId))
        {
            error = "No se pudo asignar un ID nuevo (límite alcanzado).";
            return false;
        }

        string deviceId = DeviceIdentity.EnsureDeviceId();
        string now = DateTime.UtcNow.ToString("o");

        var p = new PlayerProfile
        {
            localUserId = localId,
            id = "user_" + localId,

            deviceId = deviceId,
            globalUserId = BuildGlobalUserId(deviceId),

            nickname = nickname.Trim(),
            age = ageYears,
            gender = SexToGenderString(sex),
            birthYear = birthYear,

            hasVisionIssues = false,
            hasHearingIssues = false,

            createdAtUtc = now,
            lastUpdatedUtc = now,

            progress = new Dictionary<string, MiniGameProgress>(),
            nextAttemptNumber = 1,

            // legacy (por si tu UI aún lo mira)
            playerId4 = localId
        };

        _users[localId] = p;
        CurrentProfile = p;
        SaveDB();

        profile = p;
        assignedId = localId;
        return true;
    }

    // =========================
    // Login (NO cambia): localUserId + birthYear
    // =========================
    public bool TryLogin(string localUserId, int birthYearPassword, out string error)
    {
        error = "";

        if (string.IsNullOrWhiteSpace(localUserId))
        {
            error = "ID vacío";
            return false;
        }

        string key = NormalizeLocalId(localUserId);

        if (!_users.TryGetValue(key, out var p))
        {
            error = "Usuario no encontrado";
            return false;
        }

        if (p.birthYear != birthYearPassword)
        {
            error = "Contraseña incorrecta";
            return false;
        }

        p.lastUpdatedUtc = DateTime.UtcNow.ToString("o");
        CurrentProfile = p;
        SaveDB();
        return true;
    }

    public void Logout()
    {
        CurrentProfile = null;
        SaveDB();
    }

    /// <summary>
    /// Llama a esto cuando cambies cosas del perfil actual desde UI (discapacidades, nickname, etc.)
    /// </summary>
    public void TouchAndSaveCurrent()
    {
        if (CurrentProfile == null) return;
        CurrentProfile.lastUpdatedUtc = DateTime.UtcNow.ToString("o");
        SaveDB();
    }

    // =========================
    // Actualizaciones simples (UI)
    // =========================
    public bool UpdateNickname(string localUserId, string newNickname, out string error)
    {
        error = "";
        string key = NormalizeLocalId(localUserId);

        if (!_users.TryGetValue(key, out var p))
        {
            error = $"No existe usuario con ID {key}.";
            return false;
        }

        if (!IsValidNickname(newNickname, out error)) return false;

        p.nickname = newNickname.Trim();

        // Si el perfil actualizado es el actual, actualizamos CurrentProfile también
        if (CurrentProfile != null && CurrentProfile.localUserId == key)
            CurrentProfile = p;

        TouchAndSaveCurrent();
        return true;
    }

    public bool UpdateVisionIssues(string localUserId, bool hasIssues, out string error)
    {
        error = "";
        string key = NormalizeLocalId(localUserId);

        if (!_users.TryGetValue(key, out var p))
        {
            error = $"No existe el usuario con ID {key}.";
            return false;
        }

        p.hasVisionIssues = hasIssues;
        if (CurrentProfile != null && CurrentProfile.localUserId == key) CurrentProfile = p;

        TouchAndSaveCurrent();
        return true;
    }

    public bool UpdateHearingIssues(string localUserId, bool hasIssues, out string error)
    {
        error = "";
        string key = NormalizeLocalId(localUserId);

        if (!_users.TryGetValue(key, out var p))
        {
            error = $"No existe el usuario con ID {key}.";
            return false;
        }

        p.hasHearingIssues = hasIssues;
        if (CurrentProfile != null && CurrentProfile.localUserId == key) CurrentProfile = p;

        TouchAndSaveCurrent();
        return true;
    }

    // =========================
    // Listado / borrado
    // =========================
    public bool HasAnyUser() => _users.Count > 0;

    public List<PlayerProfile> ListUsers()
    {
        var list = new List<PlayerProfile>();
        foreach (var kv in _users) list.Add(kv.Value);
        return list;
    }

    public bool DeleteUser(string localUserId)
    {
        if (string.IsNullOrWhiteSpace(localUserId))
        {
            Debug.LogWarning("[UserDir] DeleteUser: ID vacío.");
            return false;
        }

        string key = NormalizeLocalId(localUserId);

        if (!_users.ContainsKey(key))
        {
            Debug.LogWarning($"[UserDir] DeleteUser: No existe {key}.");
            return false;
        }

        if (CurrentProfile != null && CurrentProfile.localUserId == key)
            CurrentProfile = null;

        _users.Remove(key);
        SaveDB();
        return true;
    }

    public bool DeleteAllUsers()
    {
        _users.Clear();
        db.profiles.Clear();
        CurrentProfile = null;
        SaveDB();
        return true;
    }

    // =========================
    // Validaciones
    // =========================
    public bool IsValidNickname(string nick, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(nick)) { reason = "Apodo vacío"; return false; }
        if (nick.Length < 3 || nick.Length > 16) { reason = "Apodo: 3-16 caracteres"; return false; }
        return true;
    }

    public bool IsValidAge(int age, out string reason)
    {
        reason = "";
        if (age < 18 || age > 110) { reason = "Edad fuera de rango (18-110)"; return false; }
        return true;
    }

    public bool IsValidBirthYear(int birthYear, out string reason)
    {
        reason = "";
        int currentYear = DateTime.Now.Year;
        if (birthYear < 1900 || birthYear > currentYear)
        {
            reason = "Año de nacimiento no válido";
            return false;
        }
        return true;
    }

    // =========================
    // Helpers internos
    // =========================
    private string FindFreeLocalId4()
    {
        for (int i = 1; i <= 9999; i++)
        {
            string candidate = i.ToString("0000");
            if (!_users.ContainsKey(candidate))
                return candidate;
        }
        return "";
    }

    private static string NormalizeLocalId(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        raw = raw.Trim();

        // "7" -> "0007"
        if (int.TryParse(raw, out int n))
            return n.ToString("0000");

        return raw;
    }

    private static string BuildGlobalUserId(string deviceId)
    {
        string random = Guid.NewGuid().ToString("N").Substring(0, 12);
        return $"CM-{deviceId}-{random}";
    }

    private static string SexToGenderString(Sex sex)
    {
        switch (sex)
        {
            case Sex.Female: return "F";
            case Sex.Male: return "M";
            case Sex.Other: return "O";
            default: return "U";
        }
    }

    // =========================
    // LEGACY -> NEW (migración)
    // =========================
    [Serializable]
    private class LegacyProfilesDB
    {
        public List<LegacyPlayerProfile> profiles = new List<LegacyPlayerProfile>();
    }

    // Esto modela (aprox) lo que solías guardar con JsonUtility
    [Serializable]
    private class LegacyPlayerProfile
    {
        public string playerId4;
        public string nickname;
        public int age;
        public Sex sex;
        public int birthYear;

        public bool hasVisionIssues;
        public bool hasHearingIssues;

        // En legacy normalmente el "progress Dictionary" no se serializaba bien.
        // Lo dejamos por si tu JSON viejo lo trae en alguna forma.
        public Dictionary<int, LegacyMiniGameRecord> progress = new Dictionary<int, LegacyMiniGameRecord>();
    }

    [Serializable]
    private class LegacyMiniGameRecord
    {
        public MiniGameId miniGame;
        public Dictionary<int, LegacyLevelRecord> levels = new Dictionary<int, LegacyLevelRecord>();
    }

    [Serializable]
    private class LegacyLevelRecord
    {
        public LevelId level;
        public List<LegacyAttemptMetrics> attempts = new List<LegacyAttemptMetrics>();
    }

    [Serializable]
    private class LegacyAttemptMetrics
    {
        public bool completed;
        public int correct;
        public int errors;
        public int firstTryCorrect;
        public float timeSeconds;
        public int score;
        public string endedAtUTC;
    }

    private void ConvertLegacyToNew(LegacyProfilesDB legacy)
    {
        _users.Clear();
        db = new ProfilesDB();

        string deviceId = DeviceIdentity.EnsureDeviceId();
        string now = DateTime.UtcNow.ToString("o");

        foreach (var lp in legacy.profiles)
        {
            if (lp == null) continue;
            if (string.IsNullOrEmpty(lp.playerId4)) continue;

            string localId = NormalizeLocalId(lp.playerId4);

            var p = new PlayerProfile
            {
                localUserId = localId,
                id = "user_" + localId,

                deviceId = deviceId,
                globalUserId = BuildGlobalUserId(deviceId),

                nickname = lp.nickname,
                age = lp.age,
                gender = SexToGenderString(lp.sex),
                birthYear = lp.birthYear,

                hasVisionIssues = lp.hasVisionIssues,
                hasHearingIssues = lp.hasHearingIssues,

                createdAtUtc = now,
                lastUpdatedUtc = now,

                progress = new Dictionary<string, MiniGameProgress>(),
                nextAttemptNumber = 1,

                playerId4 = localId
            };

            // Intento migrar progreso si existía de verdad (a veces era vacío por JsonUtility)
            if (lp.progress != null)
            {
                foreach (var mgKvp in lp.progress)
                {
                    var legacyMg = mgKvp.Value;
                    if (legacyMg == null) continue;

                    string mgName = legacyMg.miniGame.ToString();
                    var mgProg = new MiniGameProgress { levels = new Dictionary<string, LevelProgress>() };

                    if (legacyMg.levels != null)
                    {
                        foreach (var lvKvp in legacyMg.levels)
                        {
                            var legacyLv = lvKvp.Value;
                            if (legacyLv == null) continue;

                            string lvKey = ((int)legacyLv.level).ToString();

                            var newLv = new LevelProgress
                            {
                                bestScore = 0f,
                                bestTimeSeconds = 0f,
                                attempts = new List<AttemptMetrics>()
                            };

                            if (legacyLv.attempts != null)
                            {
                                foreach (var la in legacyLv.attempts)
                                {
                                    if (la == null) continue;

                                    var a = new AttemptMetrics
                                    {
                                        attemptId = p.ConsumeNextAttemptId("a"),
                                        startedAtUtc = la.endedAtUTC ?? "",
                                        endedAtUtc = la.endedAtUTC ?? "",

                                        level = (int)legacyLv.level,
                                        completed = la.completed,

                                        timeSeconds = la.timeSeconds,
                                        timeLimitSeconds = 0f,

                                        correct = la.correct,
                                        errors = la.errors,
                                        firstTryCorrect = la.firstTryCorrect,

                                        paymentItemsUsed = 0,

                                        // Snapshot accesibilidad: lo dejamos default false (y no se serializa)
                                        playedWithVisionIssues = false,
                                        playedWithHearingIssues = false,

                                        score = la.score
                                    };

                                    newLv.attempts.Add(a);

                                    if (a.score > newLv.bestScore) newLv.bestScore = a.score;
                                    if (a.completed)
                                    {
                                        if (newLv.bestTimeSeconds <= 0f || a.timeSeconds < newLv.bestTimeSeconds)
                                            newLv.bestTimeSeconds = a.timeSeconds;
                                    }
                                }
                            }

                            mgProg.levels[lvKey] = newLv;
                        }
                    }

                    p.progress[mgName] = mgProg;
                }
            }

            _users[localId] = p;
        }

        // Reconstruye lista
        db.profiles.Clear();
        foreach (var kv in _users)
            db.profiles.Add(kv.Value);
    }
}


