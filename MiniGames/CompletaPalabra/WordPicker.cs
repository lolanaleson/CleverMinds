using System.Collections.Generic;
using UnityEngine;

public class WordPicker : MonoBehaviour
{
    [Header("Arrastra aquí tus WC_*.asset")]
    [SerializeField] private List<WordCategorySO> categories = new List<WordCategorySO>();

    // Evita repetir durante la misma sesión (opcional pero muy útil)
    private HashSet<string> usedThisSession = new HashSet<string>();

    public struct PickResult
    {
        public string word;
        public string category;
        public string hint;
    }

    /// <summary>
    /// Devuelve palabra + categoría + hint.
    /// requireHint=true -> si una entrada no tiene hint, NO entra en el pool.
    /// </summary>
    public PickResult Pick(int minLen, int maxLen, bool requireHint)
    {
        List<(WordCategorySO.WordEntry entry, WordCategorySO cat)> pool = new();

        // 1) Construye pool filtrado
        foreach (var cat in categories)
        {
            if (cat == null) continue;

            foreach (var e in cat.GetNormalizedEntries())
            {
                if (e.word.Length < minLen || e.word.Length > maxLen) continue;
                if (usedThisSession.Contains(e.word)) continue;

                if (requireHint && string.IsNullOrWhiteSpace(e.hint))
                    continue;

                pool.Add((e, cat));
            }
        }

        // 2) Si pool vacío, limpia “used” y reintenta (para no quedarse sin palabras)
        if (pool.Count == 0)
        {
            usedThisSession.Clear();

            foreach (var cat in categories)
            {
                if (cat == null) continue;

                foreach (var e in cat.GetNormalizedEntries())
                {
                    if (e.word.Length < minLen || e.word.Length > maxLen) continue;

                    if (requireHint && string.IsNullOrWhiteSpace(e.hint))
                        continue;

                    pool.Add((e, cat));
                }
            }
        }

        // 3) Si sigue vacío, datos mal (no hay palabras en ese rango con hint)
        if (pool.Count == 0)
        {
            Debug.LogError($"[WordPicker] No hay palabras para longitud {minLen}-{maxLen} con requireHint={requireHint}. " +
                           $"Revisa que tus WordCategorySO tengan entries y hints.");
            return new PickResult { word = "ERROR", category = "ERROR", hint = "Faltan datos" };
        }

        // 4) Elige aleatoria
        var chosen = pool[Random.Range(0, pool.Count)];
        usedThisSession.Add(chosen.entry.word);

        return new PickResult
        {
            word = chosen.entry.word,
            category = chosen.cat.categoryName,
            hint = chosen.entry.hint
        };
    }
}
