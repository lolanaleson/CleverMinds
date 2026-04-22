using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

public class WeightDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI leftTMP;
    [SerializeField] private TextMeshProUGUI rightTMP;
    [SerializeField] private CanvasGroup canvasGroup;

    public int WeightValue { get; private set; }

    private WeightOrderGameManager gameManager;

    private RectTransform rectTransform;
    private Canvas parentCanvas;

    private Transform startParent;
    private int startSiblingIndex;
    private Vector2 startAnchoredPosition;

    private bool wasDroppedOnZone = false;
    private Vector2 pointerToItemOffset;

    // ✅ para que el LayoutGroup no la “pelee”
    private LayoutElement layoutElement;
    private bool startParentHasLayoutGroup = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = gameObject.AddComponent<LayoutElement>();
    }

    private void Start()
    {
        CacheStartState();
    }

    public void SetGameManager(WeightOrderGameManager manager) => gameManager = manager;

    public void SetWeight(int w)
    {
        WeightValue = w;
        if (leftTMP) leftTMP.text = w.ToString();
        if (rightTMP) rightTMP.text = w.ToString();
    }

    private void CacheStartState()
    {
        startParent = transform.parent;
        startSiblingIndex = transform.GetSiblingIndex();
        startAnchoredPosition = rectTransform.anchoredPosition;

        startParentHasLayoutGroup =
            startParent != null &&
            startParent.GetComponent<HorizontalOrVerticalLayoutGroup>() != null ||
            startParent != null &&
            startParent.GetComponent<GridLayoutGroup>() != null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (gameManager != null && gameManager.IsPaused) return;

        CacheStartState();
        wasDroppedOnZone = false;

        // Para que el placeholder pueda detectar el drop
        canvasGroup.blocksRaycasts = false;

        // ✅ Mientras arrastras: que el LayoutGroup no la mueva ni reserve hueco
        layoutElement.ignoreLayout = true;

        // ✅ Sácala del contenedor al DragLayer para evitar solapes/peleas
        if (gameManager != null && gameManager.DragLayer != null)
            transform.SetParent(gameManager.DragLayer, worldPositionStays: true);

        transform.SetAsLastSibling();

        // Offset “chincheta”
        RectTransform parentRect = rectTransform.parent as RectTransform;
        Vector2 localPointerPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out localPointerPos
        );
        pointerToItemOffset = rectTransform.anchoredPosition - localPointerPos;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (gameManager != null && gameManager.IsPaused) return;
        if (parentCanvas == null) return;

        RectTransform parentRect = rectTransform.parent as RectTransform;
        if (parentRect == null) return;

        Vector2 localPointerPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out localPointerPos
        );

        rectTransform.anchoredPosition = localPointerPos + pointerToItemOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (gameManager != null && gameManager.IsPaused) return;

        canvasGroup.blocksRaycasts = true;

        if (!wasDroppedOnZone)
            ReturnToStartPosition();
    }

    public void MarkDroppedOnZone(bool value) => wasDroppedOnZone = value;

    public void ReturnToStartPosition()
    {
        // ✅ Vuelve al contenedor y al mismo orden
        if (startParent != null)
        {
            transform.SetParent(startParent, worldPositionStays: false);
            transform.SetSiblingIndex(startSiblingIndex);
        }

        // ✅ Deja que el LayoutGroup recolocque. No forces anchoredPosition si hay layout.
        if (!startParentHasLayoutGroup)
            rectTransform.anchoredPosition = startAnchoredPosition;

        layoutElement.ignoreLayout = false;
        canvasGroup.blocksRaycasts = true;

        gameManager?.RebuildSpawnLayout();
    }

    public void DisableDrag()
    {
        canvasGroup.blocksRaycasts = false;
        enabled = false;
    }

    public void SetAlpha(float a)
    {
        canvasGroup.alpha = Mathf.Clamp01(a);
    }
}
