using UnityEngine;
using UnityEngine.EventSystems;

public class ClockHandDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum HandType { Hour, Minute }

    [Header("Config")]
    [SerializeField] private HandType handType = HandType.Minute;
    [SerializeField] private ClockTimeGameManager gameManager;

    public void SetManager(ClockTimeGameManager manager) => gameManager = manager;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (gameManager != null && gameManager.IsPaused) return;
        if (gameManager != null) gameManager.NotifyDragStart(handType);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (gameManager == null || gameManager.IsPaused) return;
        if (gameManager.ClockRoot == null) return;

        RectTransform clockRoot = gameManager.ClockRoot;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                clockRoot, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            return;

        // atan2 -> grados, 0 a la derecha, CCW
        float angle = Mathf.Atan2(localPoint.y, localPoint.x) * Mathf.Rad2Deg;

        // Lo convertimos a ángulo tipo reloj: 0 arriba, aumenta horario
        float clockAngle = 90f - angle;
        clockAngle = (clockAngle % 360f + 360f) % 360f;

        gameManager.OnHandDragged(handType, clockAngle);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (gameManager != null && gameManager.IsPaused) return;
        if (gameManager != null) gameManager.NotifyDragEnd(handType);
    }
}

