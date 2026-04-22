using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MoneyButtonController : MonoBehaviour
{
    [Header("UI (opcional)")]
    [SerializeField] private TextMeshProUGUI textValue;

    [Header("Config")]
    [SerializeField] private int denominationCents; // ejemplo: 100 = 1€, 50 = 50c

    private BartoloCompraGameManager manager;

    private void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnClicked);
    }

    // El manager te “inyecta” aquí, para que el botón sepa a quién llamar
    public void Bind(BartoloCompraGameManager gameManager)
    {
        manager = gameManager;
        RefreshLabel();
    }

    public int GetDenominationCents() => denominationCents;

    public void SetDenominationCents(int cents)
    {
        denominationCents = cents;
        RefreshLabel();
    }

    private void OnClicked()
    {
        if (manager == null) return;
        manager.OnMoneyPressed(denominationCents);
    }

    private void RefreshLabel()
    {
        if (textValue == null) return;

        // Formato visual del botón
        if (denominationCents >= 100)
        {
            int euros = denominationCents / 100;
            textValue.text = $"{euros} €";
        }
        else
        {
            textValue.text = $"{denominationCents:00} c";
        }
    }
}
