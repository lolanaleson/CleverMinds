using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class LetterDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private TextMeshProUGUI letterTMP;
    [SerializeField] private CanvasGroup canvasGroup;

    public char Letter { get; private set; }

    private WordFillGameManager gameManager;

    private RectTransform rectTransform;
    private Canvas parentCanvas;

    private Vector2 startAnchoredPosition;
    private Transform startParent;

    private bool wasDroppedOnZone = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        startAnchoredPosition = rectTransform.anchoredPosition;
        startParent = transform.parent;
    }

    public void SetGameManager(WordFillGameManager manager) => gameManager = manager;

    public void SetLetter(char c)
    {
        Letter = c;
        if (letterTMP != null) letterTMP.text = c.ToString();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (gameManager != null && gameManager.IsPaused) return;

        startAnchoredPosition = rectTransform.anchoredPosition;
        startParent = transform.parent;

        wasDroppedOnZone = false;

        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (gameManager != null && gameManager.IsPaused) return;
        if (parentCanvas == null) return;

        // CERO OFFSET (como KeyDraggable)
        rectTransform.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (gameManager != null && gameManager.IsPaused) return;

        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = true;

        if (!wasDroppedOnZone)
            ReturnToStartPosition();
    }

    public void MarkDroppedOnZone(bool value) => wasDroppedOnZone = value;

    public void ReturnToStartPosition()
    {
        if (startParent != null) transform.SetParent(startParent, true);
        rectTransform.anchoredPosition = startAnchoredPosition;

        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
    }

    public void DisableDrag()
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        enabled = false;
    }
}
