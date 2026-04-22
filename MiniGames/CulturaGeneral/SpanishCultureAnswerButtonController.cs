using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpanishCultureAnswerButtonController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI answerText;

    private SpanishCultureQuizGameManager manager;
    private bool isCorrect;
    private string answerValue;

    private Color defaultBackgroundColor = Color.white;
    private bool cachedDefaults = false;

    private void Awake()
    {
        Button btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(OnClicked);

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (answerText == null)
            answerText = GetComponentInChildren<TextMeshProUGUI>();


        CacheDefaultColors();
    }

    private void CacheDefaultColors()
    {
        if (cachedDefaults) return;

        if (backgroundImage != null)
            defaultBackgroundColor = backgroundImage.color;

        cachedDefaults = true;
    }

    public void Bind(SpanishCultureQuizGameManager gameManager)
    {
        manager = gameManager;
    }

    public void Setup(string answer, bool correct)
    {
        answerValue = answer;
        isCorrect = correct;

        ResetVisualState();

        if (answerText != null)
            answerText.text = answerValue;

        Button btn = GetComponent<Button>();
        if (btn != null)
            btn.interactable = true;
    }

    public void ResetVisualState()
    {
        CacheDefaultColors();

        if (backgroundImage != null)
            backgroundImage.color = defaultBackgroundColor;

    }

    public void MarkWrongAndDisable()
    {
        Color wrongRed = new Color(1f, 0.35f, 0.35f, 1f);

        if (backgroundImage != null)
            backgroundImage.color = wrongRed;

        Button btn = GetComponent<Button>();
        if (btn != null)
            btn.interactable = false;

    }

    public void SetBackgroundColor(Color color)
    {
        if (backgroundImage == null) return;
        backgroundImage.color = color;
    }

    public void RestoreDefaultColor()
    {
        if (backgroundImage == null) return;
        backgroundImage.color = defaultBackgroundColor;
    }

    private void OnClicked()
    {
        if (manager == null) return;
        manager.OnAnswerSelected(this);
    }

    public bool IsCorrect() => isCorrect;
    public string GetAnswerValue() => answerValue;
}
