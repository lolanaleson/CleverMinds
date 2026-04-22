using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DropZone : MonoBehaviour, IDropHandler
{
    [SerializeField] private int correctKeyId;

    private EncajaLaLlaveGameManager gameManager;
    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    public void SetGameManager(EncajaLaLlaveGameManager manager)
    {
        gameManager = manager;
    }

    public void SetCorrectKeyId(int id)
    {
        correctKeyId = id;
    }

    public void OnDrop(PointerEventData eventData)
    {
        // Este método se llama cuando una llave se suelta encima de esta zona

        if (eventData.pointerDrag == null) return;

        KeyDraggable droppedKey = eventData.pointerDrag.GetComponent<KeyDraggable>();
        if (droppedKey == null) return;

        // Marcamos que esta llave SÍ ha sido soltada en la DropZone
        droppedKey.MarkDroppedOnZone(true);

        // Movemos la llave visualmente al centro de la DropZone
        RectTransform keyRect = droppedKey.GetComponent<RectTransform>();
        RectTransform dropRect = GetComponent<RectTransform>();

        if (keyRect != null && dropRect != null)
        {
            keyRect.position = dropRect.position;
        }

        // Avisamos al GameManager para que compruebe si es correcta o no
        if (gameManager != null)
        {
            gameManager.OnKeyDroppedOnDropZone(droppedKey);
        }
    }
}
