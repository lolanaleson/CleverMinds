using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SuitcaseButtonController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI wordText;

    [Tooltip("Image donde se ve el dibujo de la maleta (sprite de color).")]
    [SerializeField] private Image suitcaseImage;

    private Button button;
    private CanvasGroup canvasGroup;

    private EncuentraLaMaletaGameManager gameManager;
    private bool isImpostor;

    private void Awake()
    {
        if (wordText == null)
            wordText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (suitcaseImage == null)
        {
            Image[] imgs = GetComponentsInChildren<Image>(true);
            if (imgs != null && imgs.Length > 0)
            {
                Image self = GetComponent<Image>(); // fondo del botón
                foreach (var img in imgs)
                {
                    if (img != self) { suitcaseImage = img; break; }
                }
                if (suitcaseImage == null) suitcaseImage = imgs[0];
            }
        }

        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClicked);

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Setup(
        EncuentraLaMaletaGameManager manager,
        string word,
        bool impostor,
        Sprite suitcaseSprite)
    {
        gameManager = manager;
        isImpostor = impostor;

        if (wordText != null)
            wordText.text = word;

        if (suitcaseImage != null)
        {
            suitcaseImage.sprite = suitcaseSprite;
            suitcaseImage.color = Color.white; // por si alguien tocó el color sin querer
        }

        SetDiscarded(false);
        SetHighlight(false);
    }

    private void OnClicked()
    {
        if (gameManager == null) return;
        if (IsDiscarded()) return;

        gameManager.OnSuitcaseSelected(this);
    }

    public bool IsImpostor() => isImpostor;

    public void SetHighlight(bool active)
    {
        Image bg = GetComponent<Image>();
        if (bg == null) return;
        bg.color = active ? Color.yellow : Color.white;
    }

    // ✅ Apagar/descartar al fallar
    public void SetDiscarded(bool discarded)
    {
        if (button != null) button.interactable = !discarded;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = discarded ? 0.35f : 1f;
            canvasGroup.blocksRaycasts = !discarded;
        }
    }

    public bool IsDiscarded()
    {
        return button != null && !button.interactable;
    }
}
