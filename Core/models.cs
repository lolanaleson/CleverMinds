// Assets/Scripts/Core/models.cs
using System;
using System.Collections.Generic;
using System.ComponentModel; // DefaultValue
using UnityEngine;

public enum Sex { Unspecified = 0, Female = 1, Male = 2, Other = 3 }

public enum MiniGameId
{
    Desert = 1, Maletas = 2, Burbujas = 3, Biblioteca = 4,

    EncajaLlave = 5, EncuentraElCoche = 6, MemorizaReceta = 7,
    PagoExacto = 8, MaletaEquivocada = 9, CompletaPalabra = 10,
    ColocaReloj = 11, MapaEspaña = 12, MemorizaBoda = 13,
    AtrapaBT = 14, OrdenaPesas = 15, Karaoke = 16, CulturaGeneral=17
}

public enum LevelId { Level1 = 1, Level2 = 2, Level3 = 3, Level4 = 4 }

[Serializable]
public class PlayerProfile
{
    // IDs
    public string localUserId;   // "0007"
    public string globalUserId;  // "CM-{deviceId}-{randomGuid}"
    public string deviceId;      // "8f3c1a2b9d4e"
    public string id;            // "user_0007"

    // Datos
    public string nickname;
    public int age;
    public string gender;  // "F","M","O","U"
    public int birthYear;  // PIN / contraseña (login)

    // Discapacidades (solo las que tienes)
    public bool hasVisionIssues;
    public bool hasHearingIssues;

    // Timestamps
    public string createdAtUtc;
    public string lastUpdatedUtc;

    // Progreso “matrioska bonita”
    public Dictionary<string, MiniGameProgress> progress = new Dictionary<string, MiniGameProgress>();

    // Attempt incremental
    public int nextAttemptNumber = 1;

    // Legacy
    public string playerId4;

    public string ConsumeNextAttemptId(string prefix = "a")
    {
        string idGen = $"{prefix}_{nextAttemptNumber:0000}";
        nextAttemptNumber++;
        return idGen;
    }
}

[Serializable]
public class MiniGameProgress
{
    public Dictionary<string, LevelProgress> levels = new Dictionary<string, LevelProgress>();
}

[Serializable]
public class LevelProgress
{
    public float bestScore = 0f;
    public float bestTimeSeconds = 0f;
    public List<AttemptMetrics> attempts = new List<AttemptMetrics>();
}

[Serializable]
public class AttemptMetrics
{
    public string attemptId;
    public string startedAtUtc;
    public string endedAtUtc;

    public int level;
    public bool completed;

    public float timeSeconds;

    // Solo para nivel 4 (oculto si 0)
    [DefaultValue(0f)]
    public float timeLimitSeconds;

    // Métricas clave
    public int correct;
    public int errors;
    public int firstTryCorrect;

    // PagoExacto: nº de monedas/billetes usados (oculto si 0)
    [DefaultValue(0)]
    public int paymentItemsUsed;

    // Snapshot accesibilidad (oculto si false)
    [DefaultValue(false)]
    public bool playedWithVisionIssues;

    [DefaultValue(false)]
    public bool playedWithHearingIssues;

    public float score;
}

[Serializable]
public struct MiniGameSelection
{
    public MiniGameId miniGame;
    public LevelId level;
}

