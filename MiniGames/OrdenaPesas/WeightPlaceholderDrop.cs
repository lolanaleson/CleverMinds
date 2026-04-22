using UnityEngine;
using UnityEngine.EventSystems;

public class WeightPlaceholderDrop : MonoBehaviour, IDropHandler
{
    public bool Occupied => occupied;
    public int CorrectWeight => correctWeight;

    private bool occupied = false;
    private int correctWeight = -1;

    private WeightOrderGameManager gameManager;

    public void SetGameManager(WeightOrderGameManager manager) => gameManager = manager;

    /// <summary>Resetea placeholder para nueva ronda: vacío y objetivo nuevo.</summary>
    public void SetCorrectWeight(int w)
    {
        correctWeight = w;
        occupied = false;

        // Limpia pesos visuales anteriores (si había alguno)
        ClearPlacedWeights();
    }

    /// <summary>Para nivel 2: marcar como ocupado cuando colocamos una pesa fija.</summary>
    public void ForceSetOccupied(bool value) => occupied = value;

    public void OnDrop(PointerEventData eventData)
    {
        if (occupied) return;
        if (eventData.pointerDrag == null) return;

        var dropped = eventData.pointerDrag.GetComponent<WeightDraggable>();
        if (dropped == null) return;

        dropped.MarkDroppedOnZone(true);

        if (dropped.WeightValue != correctWeight)
        {
            gameManager?.OnWrongWeightDropped(dropped.WeightValue, correctWeight);
            dropped.ReturnToStartPosition();
            gameManager?.RebuildSpawnLayout();
            return;
        }

        occupied = true;

        // ✅ Colocar perfecto al centro (igual que PlaceholderDrop de WordFill)
        RectTransform weightRect = dropped.GetComponent<RectTransform>();
        RectTransform dropRect = GetComponent<RectTransform>();
        Vector3 dropWorldCenter = dropRect.TransformPoint(dropRect.rect.center);

        weightRect.SetParent(dropRect, worldPositionStays: true);
        weightRect.position = dropWorldCenter;
        weightRect.localRotation = Quaternion.identity;
        weightRect.localScale = Vector3.one;

        dropped.DisableDrag();
        gameManager?.RebuildSpawnLayout();

        gameManager?.ShowFeedbackCorrect();
        gameManager?.OnCorrectPlaced();
    }

    private void ClearPlacedWeights()
    {
        // Destruye cualquier WeightDraggable que hubiera colgado del placeholder
        var existing = GetComponentsInChildren<WeightDraggable>(includeInactive: true);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null)
                Destroy(existing[i].gameObject);
        }
    }
}
