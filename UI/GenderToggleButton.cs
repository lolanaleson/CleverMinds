using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Botón de género que actúa como toggle independiente:
/// - Al pulsar: cambia a color "seleccionado"
/// - Al volver a pulsar: se deselecciona (vuelve al color base)
/// Notifica al DemographicsController su estado.
/// </summary>
[RequireComponent(typeof(Button), typeof(Image))]
public class GenderToggleButton : MonoBehaviour
{
    [Header("Config")]
    public Sex mySex = Sex.Unspecified; // Asignar en Inspector: Male / Female / Other
    public Color baseColor = new Color(0.85f, 0.85f, 0.85f);   // gris claro
    public Color selectedColor = new Color(0.35f, 0.75f, 0.35f); // verde

    [Header("Refs")]
    public TextMeshProUGUI label; // opcional, sólo para cambiar el texto/estilo si quieres

    // Estado interno
    public bool IsSelected { get; private set; } = false;

    private Image _img;
    private Button _btn;
    private DemographicsController _controller;

    void Awake()
    {
        _img = GetComponent<Image>();
        _btn = GetComponent<Button>();
        _btn.onClick.AddListener(OnClick);
        _img.color = baseColor;
        _controller = FindObjectOfType<DemographicsController>();
        if (_controller == null)
        {
            Debug.LogError("[GenderToggleButton] No se encontró DemographicsController en escena.");
        }
    }

    void OnClick()
    {
        // Toggle local
        IsSelected = !IsSelected;
        _img.color = IsSelected ? selectedColor : baseColor;

        // Notifica al controlador
        if (_controller != null)
        {
            _controller.OnGenderButtonToggled(this);
        }
    }

    /// <summary>
    /// Forzado externo (el controller puede deseleccionar por lógica si lo necesitas).
    /// </summary>
    public void ForceSelect(bool value)
    {
        IsSelected = value;
        if (_img != null) _img.color = IsSelected ? selectedColor : baseColor;
    }
}
