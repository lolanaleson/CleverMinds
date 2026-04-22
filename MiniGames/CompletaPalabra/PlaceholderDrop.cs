using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class PlaceholderDrop : MonoBehaviour, IDropHandler
{
    public bool Occupied => occupied;

    [Header("UI (el TMP que tiene '?')")]
    [SerializeField] private TextMeshProUGUI symbolTMP;

    private bool occupied = false;
    private char correctLetter;
    private WordFillGameManager gameManager;

    private Coroutine blinkRoutine;

    public void SetGameManager(WordFillGameManager manager) => gameManager = manager;

    public void SetCorrectLetter(char c)
    {
        correctLetter = char.ToUpperInvariant(c);
        occupied = false;
        StopBlinkIfAny();

        if (symbolTMP != null)
        {
            symbolTMP.text = "?";
            var col = symbolTMP.color; col.a = 1f;
            symbolTMP.color = col;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (occupied) return;
        if (eventData.pointerDrag == null) return;

        var dropped = eventData.pointerDrag.GetComponent<LetterDraggable>();
        if (dropped == null) return;

        dropped.MarkDroppedOnZone(true);

        if (char.ToUpperInvariant(dropped.Letter) != correctLetter)
        {
            gameManager?.ShowFeedbackWrong();
            dropped.ReturnToStartPosition();
            gameManager?.RebuildAnswersLayout();
            return;
        }

        occupied = true;

        // Colocar perfecto (mundo)
        RectTransform letterRect = dropped.GetComponent<RectTransform>();
        RectTransform dropRect = GetComponent<RectTransform>();
        Vector3 dropWorldCenter = dropRect.TransformPoint(dropRect.rect.center);

        letterRect.SetParent(dropRect, worldPositionStays: true);
        letterRect.position = dropWorldCenter;
        letterRect.localRotation = Quaternion.identity;
        letterRect.localScale = Vector3.one;

        dropped.DisableDrag();
        gameManager?.RebuildAnswersLayout();

        // Oculta el "?" si quieres (opcional)
        if (symbolTMP != null) symbolTMP.text = "";

        gameManager?.ShowFeedbackCorrect();
        gameManager?.OnCorrectPlaced();
    }

    // ====== NIVEL 4: mostrar letra correcta si se acaba el tiempo ======
    public void RevealCorrectLetterWithBlink(float duration = 1.6f, float blinkHz = 6f)
    {
        if (occupied) return;
        if (symbolTMP == null) return;

        StopBlinkIfAny();
        symbolTMP.text = correctLetter.ToString();
        blinkRoutine = StartCoroutine(BlinkCoroutine(duration, blinkHz));
    }

    private IEnumerator BlinkCoroutine(float duration, float blinkHz)
    {
        float t = 0f;
        float period = 1f / Mathf.Max(0.01f, blinkHz);

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            // alterna alpha 0/1
            float phase = Mathf.Repeat(t, period);
            float alpha = (phase < period * 0.5f) ? 1f : 0f;

            var c = symbolTMP.color;
            c.a = alpha;
            symbolTMP.color = c;

            yield return null;
        }

        // al final la dejamos visible
        var finalC = symbolTMP.color;
        finalC.a = 1f;
        symbolTMP.color = finalC;

        blinkRoutine = null;
    }

    private void StopBlinkIfAny()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }
    }
}
