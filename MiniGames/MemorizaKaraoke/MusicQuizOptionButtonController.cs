using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MusicQuizOptionButtonController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI label;

    private MusicQuizGameManager manager;
    private string answerText;
    private bool isCorrect;

    private Color defaultBg;
    private Color defaultLabel;
    private bool cached;

    private void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnClicked);

        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);

        CacheDefaults();
    }

    private void CacheDefaults()
    {
        if (cached) return;
        if (backgroundImage != null) defaultBg = backgroundImage.color;
        if (label != null) defaultLabel = label.color;
        cached = true;
    }

    public void Bind(MusicQuizGameManager gm) => manager = gm;

    public void Setup(string text, bool correct)
    {
        answerText = text;
        isCorrect = correct;

        ResetVisual();

        if (label != null) label.text = answerText;

        var btn = GetComponent<Button>();
        if (btn != null) btn.interactable = true;
    }

    public void ResetVisual()
    {
        CacheDefaults();
        if (backgroundImage != null) backgroundImage.color = defaultBg;
        if (label != null) label.color = defaultLabel;
    }

    public void MarkWrong()
    {
        Color wrongRed = new Color(1f, 0.35f, 0.35f, 1f);
        if (backgroundImage != null) backgroundImage.color = wrongRed;
        if (label != null) label.color = wrongRed;
    }

    public void MarkCorrect()
    {
        Color okGreen = new Color(0.35f, 1f, 0.45f, 1f);
        if (backgroundImage != null) backgroundImage.color = okGreen;
        if (label != null) label.color = okGreen;
    }

    public IEnumerator BlinkCorrect(float duration = 0.9f, int blinks = 3)
    {
        CacheDefaults();
        Color okGreen = new Color(0.35f, 1f, 0.45f, 1f);

        float step = duration / (blinks * 2f);
        for (int i = 0; i < blinks; i++)
        {
            if (backgroundImage != null) backgroundImage.color = okGreen;
            if (label != null) label.color = okGreen;
            yield return new WaitForSecondsRealtime(step);

            if (backgroundImage != null) backgroundImage.color = defaultBg;
            if (label != null) label.color = defaultLabel;
            yield return new WaitForSecondsRealtime(step);
        }

        // Lo dejamos verde al final (para que quede claro)
        MarkCorrect();
    }

    private void OnClicked()
    {
        if (manager == null) return;
        manager.OnOptionSelected(this);
    }

    public bool IsCorrect() => isCorrect;
    public string GetAnswer() => answerText;
}

