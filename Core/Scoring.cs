// Assets/Scripts/Core/Scoring.cs
using UnityEngine;

public static class Scoring
{
    public static float Compute(MiniGameConfig cfg, MiniGameConfig.LevelTuning t, AttemptMetrics a)
    {
        if (cfg == null || t == null || a == null) return 0f;

        if (cfg.scoringMode == MiniGameConfig.ScoringMode.PagoExacto)
            return ComputePagoExacto(t, a);

        return ComputeStandard(t, a);
    }

    // ====== Standard (11 juegos) ======
    private static float ComputeStandard(MiniGameConfig.LevelTuning t, AttemptMetrics a)
    {
        int total = a.correct + a.errors;
        float accuracy = (total > 0) ? (float)a.correct / total : 0f;

        float completionScore = a.completed ? 1f : 0f;
        float accuracyScore = accuracy;
        float timeScore = Mathf.Clamp01(t.targetTimeSeconds / Mathf.Max(a.timeSeconds, 0.001f));

        float weighted =
            t.weightCompletion * completionScore +
            t.weightAccuracy * accuracyScore +
            t.weightTime * timeScore;

        float score = t.basePoints * weighted;

        // Bonus por primera a la primera
        score += t.firstTryBonusEach * a.firstTryCorrect;

        // Perfecto (sin errores)
        if (a.errors == 0 && a.completed)
            score += t.perfectBonus;

        if (score < 0f) score = 0f;
        score = Mathf.Round(score * 10f) / 10f;
        return score;
    }

    // ====== PagoExacto ======
    private static float ComputePagoExacto(MiniGameConfig.LevelTuning t, AttemptMetrics a)
    {
        if (!a.completed) return 0f;

        int items = Mathf.Max(0, a.paymentItemsUsed);

        float score = t.payExact_baseScore;

        score -= items * t.payExact_penaltyPerItem;
        score -= a.errors * t.payExact_penaltyPerOverpayError;

        float timeScore = Mathf.Clamp01(t.targetTimeSeconds / Mathf.Max(a.timeSeconds, 0.001f));
        score += t.payExact_baseScore * t.payExact_timeInfluence * timeScore;

        if (score < 0f) score = 0f;
        score = Mathf.Round(score * 10f) / 10f;
        return score;
    }
}


