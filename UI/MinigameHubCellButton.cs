using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class MinigameHubCellButton : MonoBehaviour
{
    [SerializeField] private MiniGameId minigameId;
    [SerializeField] private TextMeshProUGUI title; // opcional (si quieres texto)
    [SerializeField] private string overrideTitle;  // opcional

    private Button button;
    private MinigameHubController hub;

    private void Awake()
    {
        button = GetComponent<Button>();
        hub = FindFirstObjectByType<MinigameHubController>();

        button.onClick.AddListener(OnClick);

        if (title)
        {
            title.text = string.IsNullOrWhiteSpace(overrideTitle)
                ? minigameId.ToString()
                : overrideTitle;
        }
    }

    private void OnClick()
    {
        if (hub == null)
        {
            Debug.LogError("[MinigameHubCellButton] No encuentro MinigameHubController en escena.");
            return;
        }

        hub.Go(minigameId);
    }
}

