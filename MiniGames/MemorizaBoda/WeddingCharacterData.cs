using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeddingCharacterId
{
    Tia,
    Tio,
    Novia,
    Novio,
    Abuela,
    Abuelo,
    Sobrina,
    Sobrino
}

public enum WeddingExpressionId
{
    Neutral,     // o Saludo si prefieres
    Triste,
    Enfadado,
    Sorprendido,
    Asustado
}

public enum WeddingSpriteType
{
    Observe,
    Button
}

[CreateAssetMenu(menuName = "CleverMinds/Wedding/WeddingCharacterData")]
public class WeddingCharacterData : ScriptableObject
{
    [Header("Identity")]
    public WeddingCharacterId characterId;
    public bool isChild;

    [Serializable]
    public class ExpressionEntry
    {
        public WeddingExpressionId expressionId;
        public Sprite observeSprite; // Sprite grande (observación)
        public Sprite buttonSprite;  // Sprite recortado (botón)
    }

    [Header("Expressions (5)")]
    public List<ExpressionEntry> expressions = new List<ExpressionEntry>();

    // Lookup runtime
    private Dictionary<WeddingExpressionId, ExpressionEntry> dict;

    public void BuildRuntimeDictIfNeeded()
    {
        if (dict != null) return;

        dict = new Dictionary<WeddingExpressionId, ExpressionEntry>();
        foreach (var e in expressions)
        {
            if (e == null) continue;
            dict[e.expressionId] = e;
        }
    }

    public bool HasExpression(WeddingExpressionId expr)
    {
        BuildRuntimeDictIfNeeded();
        return dict.ContainsKey(expr);
    }

    public Sprite GetSprite(WeddingExpressionId expr, WeddingSpriteType type)
    {
        BuildRuntimeDictIfNeeded();

        if (!dict.TryGetValue(expr, out var entry) || entry == null)
            return null;

        return type == WeddingSpriteType.Observe ? entry.observeSprite : entry.buttonSprite;
    }
}
