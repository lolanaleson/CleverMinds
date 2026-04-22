using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CleverMinds/Word Fill/Word Category", fileName = "WC_NewCategory")]
public class WordCategorySO : ScriptableObject
{
    [Header("Nombre de categoría (se muestra en pista)")]
    public string categoryName = "Nueva categoría";

    [Header("Entradas (cada palabra CON su pista)")]
    public List<WordEntry> entries = new List<WordEntry>();

    [Serializable]
    public struct WordEntry
    {
        public string word;  // EJ: "VENTANA"
        public string hint;  // EJ: "Se abre para ver fuera"
    }

    public IEnumerable<WordEntry> GetNormalizedEntries()
    {
        foreach (var e in entries)
        {
            if (string.IsNullOrWhiteSpace(e.word)) continue;

            yield return new WordEntry
            {
                word = e.word.Trim().ToUpperInvariant(),
                hint = string.IsNullOrWhiteSpace(e.hint) ? "" : e.hint.Trim()
            };
        }
    }
}
