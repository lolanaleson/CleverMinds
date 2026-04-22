using UnityEngine;
using UnityEngine.UI;

public class WeddingOptionButtonController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image backgroundImage; // fondo del botón
    [SerializeField] private Image iconImage;       // imagen del personaje

    private WeddingFacesGameManager manager;

    private WeddingCharacterId characterId;
    private WeddingExpressionId expressionId;
    private bool isCorrect;

    private Color defaultBgColor = Color.white;
    private Color defaultIconColor = Color.white;
    private bool cachedDefaults = false;

    private void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnClicked);

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (iconImage == null)
        {
            var iconTf = transform.Find("Icon");
            if (iconTf != null)
                iconImage = iconTf.GetComponent<Image>();
        }

        CacheDefaultColors();
    }

    private void CacheDefaultColors()
    {
        if (cachedDefaults) return;

        if (backgroundImage != null)
            defaultBgColor = backgroundImage.color;

        if (iconImage != null)
            defaultIconColor = iconImage.color;

        cachedDefaults = true;
    }

    public void Bind(WeddingFacesGameManager gameManager)
    {
        manager = gameManager;
    }

    public void Setup(
        WeddingCharacterId cid,
        WeddingExpressionId eid,
        Sprite buttonSprite,
        bool correct)
    {
        characterId = cid;
        expressionId = eid;
        isCorrect = correct;

        ResetVisual();

        if (iconImage != null)
        {
            iconImage.sprite = buttonSprite;
            iconImage.enabled = (buttonSprite != null);
        }

        var btn = GetComponent<Button>();
        if (btn != null) btn.interactable = true;
    }

    public void ResetVisual()
    {
        CacheDefaultColors();

        if (backgroundImage != null)
            backgroundImage.color = defaultBgColor;

        if (iconImage != null)
            iconImage.color = defaultIconColor;
    }

    public void MarkWrong()
    {
        // Rojo suave, no agresivo
        Color wrongRed = new Color(1f, 0.35f, 0.35f, 1f);

        if (backgroundImage != null)
            backgroundImage.color = wrongRed;

        if (iconImage != null)
            iconImage.color = wrongRed;
    }

    private void OnClicked()
    {
        if (manager == null) return;
        manager.OnOptionSelected(this);
    }

    public WeddingCharacterId GetCharacterId() => characterId;
    public WeddingExpressionId GetExpressionId() => expressionId;
    public bool IsCorrect() => isCorrect;
}
