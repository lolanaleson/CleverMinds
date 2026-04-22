// Assets/Scripts/Core/MiniGameConfig.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MiniGameConfig", menuName = "CleverMinds/MiniGameConfig")]
public class MiniGameConfig : ScriptableObject
{
    public MiniGameId miniGameId;

    public enum ScoringMode
    {
        Standard = 0,
        PagoExacto = 1
    }

    public ScoringMode scoringMode = ScoringMode.Standard;

    [System.Serializable]
    public class LevelTuning
    {
        public LevelId level;

        // =========================
        // SCORING STANDARD (11 juegos)
        // =========================
        [Header("Standard - Puntuación base")]
        public int basePoints = 1000;

        [Header("Standard - Tiempo objetivo")]
        public float targetTimeSeconds = 60f;

        [Header("Standard - Pesos")]
        [Range(0f, 1f)] public float weightCompletion = 0.7f;
        [Range(0f, 1f)] public float weightAccuracy = 0.25f;
        [Range(0f, 1f)] public float weightTime = 0.05f;

        [Header("Standard - Bonos")]
        public int perfectBonus = 100;
        public int firstTryBonusEach = 2;

        [Header("Standard - Aprobado (si lo usas)")]
        [Range(0f, 100f)] public float passAccuracyMinPct = 60f;

        // =========================
        // SCORING PAGO EXACTO
        // =========================
        [Header("PagoExacto - Ajustes")]
        public float payExact_baseScore = 100f;
        public float payExact_penaltyPerItem = 8f;
        public float payExact_penaltyPerOverpayError = 20f;

        [Range(0f, 1f)]
        public float payExact_timeInfluence = 0.15f;
    }

    public List<LevelTuning> levels = new();

    public LevelTuning Get(LevelId id) => levels.Find(l => l.level == id);
}

