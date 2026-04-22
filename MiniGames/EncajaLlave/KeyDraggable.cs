using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class KeyDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private int keyId;
    public int KeyId => keyId;

    private EncajaLaLlaveGameManager gameManager;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas parentCanvas;

    private Vector2 startAnchoredPosition;
    private Transform startParent;

    // Para saber si esta llave se soltó en la DropZone
    private bool wasDroppedOnZone = false;

    // Colores para el highlight
    private Image mainImage;
    private Color baseColor;        // color normal de la llave
    private bool baseColorSet = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        mainImage = GetComponent<Image>();
        parentCanvas = GetComponentInParent<Canvas>();

        if (canvasGroup == null)
        {
            // Si por lo que sea no se añadió, lo creamos (para evitar errores)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        // Guardamos la posición inicial (dentro del contenedor de llaves)
        startAnchoredPosition = rectTransform.anchoredPosition;
        startParent = transform.parent;

        if (mainImage != null && !baseColorSet)
        {
            baseColor = mainImage.color;
            baseColorSet = true;
        }
    }

    // =========================================================
    //  MÉTODOS DE CONFIGURACIÓN (LLAMADOS DESDE EL GAMEMANAGER)
    // =========================================================
    public void SetGameManager(EncajaLaLlaveGameManager manager)
    {
        gameManager = manager;
    }

    public void SetKeyId(int id)
    {
        keyId = id;
    }

    public void SetBaseColor(Color color)
    {
        // Guardamos el color base para poder hacer highlight
        baseColor = color;
        baseColorSet = true;

        if (mainImage != null)
        {
            mainImage.color = baseColor;
        }
    }

    // =========================================================
    //  INTERFAZ DE DRAG & DROP
    // =========================================================
    public void OnBeginDrag(PointerEventData eventData)
    {
        // Si el juego está en pausa, no dejamos empezar el drag
        if (gameManager != null && gameManager.IsPaused) return;

        Debug.Log("Empiezo a arrastrar la llave " + keyId);
        // Guardamos posición inicial por si hay que volver
        startAnchoredPosition = rectTransform.anchoredPosition;
        startParent = transform.parent;

        wasDroppedOnZone = false; // al empezar a arrastrar, todavía no ha caído en ninguna zona

        // Dejamos que los Raycasts pasen "a través" de este objeto
        // para que la DropZone pueda recibir el OnDrop.
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Bloqueamos el movimiento si el juego está en pausa
        if (gameManager != null && gameManager.IsPaused) return;

        if (parentCanvas == null) return;

        // Movimiento en coordenadas de UI, compensando el scaleFactor del Canvas
        rectTransform.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Si estamos en pausa, ignoramos el final de drag
        if (gameManager != null && gameManager.IsPaused) return;

        // Volvemos a permitir que esta llave reciba Raycasts
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        // Si NO ha sido soltada encima de la DropZone,
        // volvemos a la posición inicial.
        if (!wasDroppedOnZone)
        {
            ReturnToStartPosition();
        }

        // Si ha sido soltada en la DropZone, el GameManager decidirá
        // si la deja encajada (correcta) o la devuelve al sitio (incorrecta).
    }

    // =========================================================
    //  MÉTODOS LLAMADOS DESDE DROPZONE / GAMEMANAGER
    // =========================================================
    public void MarkDroppedOnZone(bool value)
    {
        wasDroppedOnZone = value;
    }

    public void ReturnToStartPosition()
    {
        rectTransform.anchoredPosition = startAnchoredPosition;
    }

    public void SetHighlight(bool active)
    {
        if (mainImage == null || !baseColorSet) return;

        if (active)
        {
            // Subimos un poco el brillo para destacar la llave
            Color c = baseColor * 1.2f;
            c.a = baseColor.a;
            mainImage.color = c;
        }
        else
        {
            // Volvemos al color base
            mainImage.color = baseColor;
        }
    }

    public void PlayCorrectAnimation()
    {
        // Aquí podrías hacer una pequeña animación de escala, etc.
        // De momento lo dejamos vacío para no liar más.
    }
}
