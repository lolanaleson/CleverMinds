using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CarButtonController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image carImage; // GO padre del prefab (coche)
    [SerializeField] private TextMeshProUGUI plateText;
    [SerializeField] private CanvasGroup canvasGroup;

    private EncuentraElCocheGameManager gameManager;

    private string plateString;
    private string colorName;          // nombre para pistas (ej: "rojo")
    private string modelName;
    private bool isCorrectOption;
    private Color baseCarColor = Color.white;

    private void Awake()
    {
        // Fallbacks (preferible asignar por Inspector)
        if (carImage == null)
        {
            // IMPORTANTE: usar el Image del GO padre, no un hijo (matrícula, overlays, etc.)
            carImage = GetComponent<Image>();
        }

        if (plateText == null)
        {
            plateText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (carImage != null)
            baseCarColor = carImage.color;

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OnButtonClicked);
        }
    }

    public void Setup(
        EncuentraElCocheGameManager manager,
        Sprite sprite,
        string plate,
        string colorNameForHints,
        bool isCorrect,
        string modelNameForData)
    {
        gameManager = manager;
        plateString = plate;
        colorName = colorNameForHints;
        modelName = modelNameForData;
        isCorrectOption = isCorrect;

        if (carImage != null && sprite != null)
        {
            carImage.sprite = sprite;
            carImage.color = Color.white;
            baseCarColor = carImage.color;
        }

        if (plateText != null)
        {
            plateText.text = plateString;
        }
    }

    private void OnButtonClicked()
    {
        if (gameManager == null) return;
        gameManager.OnCarSelected(this);
    }

    public string GetPlate() => plateString;
    public string GetColorName() => colorName;
    public string GetModelName() => modelName;
    public bool IsCorrectOption() => isCorrectOption;

    public void DisableAsFailed(float alpha = 0.25f)
    {
        Button btn = GetComponent<Button>();
        if (btn != null) btn.interactable = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        else if (carImage != null)
        {
            Color c = carImage.color;
            c.a = Mathf.Clamp01(alpha);
            carImage.color = c;
        }
    }

    public void ResetVisualState()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        Button btn = GetComponent<Button>();
        if (btn != null) btn.interactable = true;

        RestoreBaseColor();
    }

    public void SetHighlight(bool active)
    {
        if (carImage == null) return;

        if (active)
        {
            carImage.color = Color.white;
        }
        else
        {
            RestoreBaseColor();
        }
    }

    // Mantengo estos nombres por compatibilidad con el manager previo
    public void SetBackgroundColor(Color color)
    {
        if (carImage == null) return;
        Color c = color;
        c.a = carImage.color.a; // respetar alpha actual
        carImage.color = c;
    }

    public void ResetBackgroundColor()
    {
        RestoreBaseColor();
    }

    public void RestoreBaseColor()
    {
        if (carImage == null) return;
        Color c = baseCarColor;
        if (canvasGroup == null)
        {
            // si no hay CanvasGroup, preservamos alpha actual del propio Image al restaurar color
            c.a = carImage.color.a;
        }
        carImage.color = c;
    }
}
