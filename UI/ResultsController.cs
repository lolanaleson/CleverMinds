using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class ResultsController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI scoreTMP;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject newRecordGO;

    [Header("Scenes")]
    [SerializeField] private string selectLevelScene = "04_SelectLevel";
    [SerializeField] private string fallbackScene = "03_MinigameHub";

    [Header("Score Count-Up FX")]
    [Tooltip("Duración total del conteo (segundos).")]
    [SerializeField] private float countDuration = 0.75f;

    [Tooltip("Si el score es enorme, este límite evita miles de updates. Ej: 2000.")]
    [SerializeField] private int maxDisplayedSteps = 2000;

    private Coroutine _countRoutine;

    private void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (GameSessionManager.I == null)
        {
            SceneManager.LoadScene(fallbackScene);
            return;
        }

        var r = GameSessionManager.I.lastResult;

        // Nuevo récord
        if (newRecordGO != null)
            newRecordGO.SetActive(r != null && r.isNewRecord);

        // Score + animación
        int finalScoreInt = (r != null) ? Mathf.Max(0, Mathf.RoundToInt(r.score)) : 0;

        if (scoreTMP != null)
        {
            scoreTMP.text = "0";

            if (_countRoutine != null) StopCoroutine(_countRoutine);
            _countRoutine = StartCoroutine(CountUpScore(finalScoreInt));
        }
    }

    private IEnumerator CountUpScore(int finalScore)
    {
        if (finalScore <= 0)
        {
            scoreTMP.text = "0";
            yield break;
        }

        // Queremos que "cuente" 1,2,3... pero si es gigante,
        // hacemos saltos para no spamear 50.000 updates.
        int steps = Mathf.Min(finalScore, Mathf.Max(1, maxDisplayedSteps));
        float duration = Mathf.Max(0.05f, countDuration);

        float t = 0f;
        int lastShown = -1;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // unscaled por si pausarías el juego
            float u = Mathf.Clamp01(t / duration);

            // Ease-out para que al final desacelere un pelín (queda muy bien)
            float eased = 1f - Mathf.Pow(1f - u, 3f);

            int current;
            if (steps == finalScore)
            {
                // Conteo 1 a 1 real
                current = Mathf.RoundToInt(eased * finalScore);
            }
            else
            {
                // Conteo con saltos (pero sigue pareciendo rápido)
                int stepIndex = Mathf.RoundToInt(eased * steps);
                current = Mathf.RoundToInt((stepIndex / (float)steps) * finalScore);
            }

            if (current != lastShown)
            {
                scoreTMP.text = current.ToString();
                lastShown = current;
            }

            yield return null;
        }

        // Asegurar valor final exacto
        scoreTMP.text = finalScore.ToString();
    }

    public void Close()
    {
        if (_countRoutine != null) StopCoroutine(_countRoutine);
        SceneManager.LoadScene(fallbackScene);
    }
}