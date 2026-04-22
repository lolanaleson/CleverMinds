using System.Collections.Generic;
using UnityEngine;

public class AnswerManagerLetras : MonoBehaviour
{
    [SerializeField] private Transform lettersParent;
    [SerializeField] private GameObject letterPrefab;

    public int distractoresExtra = 4;

    private readonly List<GameObject> spawned = new();

    public void SetupLetters(List<char> correctLetters, List<char> alphabet, WordFillGameManager gm)
    {
        Clear();

        List<char> options = new List<char>(correctLetters);

        int safety = 0;
        while (options.Count < correctLetters.Count + distractoresExtra && safety < 999)
        {
            safety++;
            char c = alphabet[Random.Range(0, alphabet.Count)];
            if (options.Contains(c)) continue;
            options.Add(c);
        }

        Shuffle(options);

        foreach (var c in options)
        {
            GameObject go = Instantiate(letterPrefab, lettersParent);
            spawned.Add(go);

            var draggable = go.GetComponent<LetterDraggable>();
            draggable.SetLetter(c);
            draggable.SetGameManager(gm);
        }
    }

    public void Clear()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
            if (spawned[i] != null) Destroy(spawned[i]);

        spawned.Clear();
    }

    private void Shuffle(List<char> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

