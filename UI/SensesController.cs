using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Pantalla para preguntar si el usuario tiene dificultades de visión y/o audición.
/// - Al entrar, NINGÚN botón está seleccionado.
/// - Hasta que no responda a las dos preguntas, el botón Siguiente está desactivado.
/// - Guarda en PlayerPrefs:
///     pending_vision  = 1 (sí) / 0 (no)
///     pending_hearing = 1 (sí) / 0 (no)
/// </summary>
public class SensesController : MonoBehaviour
{
    [Header("UI - Visión")]
    [SerializeField] private Button btnVisionYes;
    [SerializeField] private Button btnVisionNo;

    [Header("UI - Audición")]
    [SerializeField] private Button btnHearingYes;
    [SerializeField] private Button btnHearingNo;

    [Header("Otros UI")]
    [SerializeField] private Button btnNext;
    [SerializeField] private TextMeshProUGUI errorLabel;

    [Header("Colores")]
    [SerializeField] private Color selectedColor = new Color(0.3f, 0.8f, 0.3f, 1f); // verde para seleccionado
    [SerializeField] private Color normalColor = Color.white;                     // color neutro

    [Header("Escenas")]
    [SerializeField] private string prevScene = "02_Demographics";
    [SerializeField] private string nextScene = "02c_ShowCredentials";

    // Estado interno: null = NO ha respondido aún
    private bool? _visionIssue = null;
    private bool? _hearingIssue = null;

    private void Start()
    {
        // Al iniciar, ningún botón seleccionado
        _visionIssue = null;
        _hearingIssue = null;

        if (errorLabel != null) errorLabel.text = "";
        if (btnNext != null) btnNext.interactable = false;

        UpdateVisionButtons();
        UpdateHearingButtons();
    }

    // ================== VISIÓN ==================

    public void OnVisionYes()
    {
        _visionIssue = true;
        UpdateVisionButtons();
        ValidateForm();
    }

    public void OnVisionNo()
    {
        _visionIssue = false;
        UpdateVisionButtons();
        ValidateForm();
    }

    private void UpdateVisionButtons()
    {
        // Recuperar imágenes de los botones
        Image imgYes = btnVisionYes != null ? btnVisionYes.GetComponent<Image>() : null;
        Image imgNo = btnVisionNo != null ? btnVisionNo.GetComponent<Image>() : null;

        // Si no ha respondido todavía → ambos en color normal
        if (_visionIssue == null)
        {
            if (imgYes != null) imgYes.color = normalColor;
            if (imgNo != null) imgNo.color = normalColor;
            return;
        }

        // Si ha respondido:
        if (imgYes != null)
            imgYes.color = (_visionIssue == true) ? selectedColor : normalColor;

        if (imgNo != null)
            imgNo.color = (_visionIssue == false) ? selectedColor : normalColor;
    }

    // ================== AUDICIÓN ==================

    public void OnHearingYes()
    {
        _hearingIssue = true;
        UpdateHearingButtons();
        ValidateForm();
    }

    public void OnHearingNo()
    {
        _hearingIssue = false;
        UpdateHearingButtons();
        ValidateForm();
    }

    private void UpdateHearingButtons()
    {
        Image imgYes = btnHearingYes != null ? btnHearingYes.GetComponent<Image>() : null;
        Image imgNo = btnHearingNo != null ? btnHearingNo.GetComponent<Image>() : null;

        if (_hearingIssue == null)
        {
            if (imgYes != null) imgYes.color = normalColor;
            if (imgNo != null) imgNo.color = normalColor;
            return;
        }

        if (imgYes != null)
            imgYes.color = (_hearingIssue == true) ? selectedColor : normalColor;

        if (imgNo != null)
            imgNo.color = (_hearingIssue == false) ? selectedColor : normalColor;
    }

    // ================== VALIDACIÓN ==================

    private void ValidateForm()
    {
        if (errorLabel != null) errorLabel.text = "";
        if (btnNext != null) btnNext.interactable = false;

        if (_visionIssue == null)
        {
            if (errorLabel != null)
                errorLabel.text = "Indica si tienes dificultades de visión.";
            return;
        }

        if (_hearingIssue == null)
        {
            if (errorLabel != null)
                errorLabel.text = "Indica si tienes dificultades de audición.";
            return;
        }

        // Si ha contestado a las dos, ya puede seguir
        if (btnNext != null) btnNext.interactable = true;
    }

    // ================== NAVEGACIÓN ==================

    public void OnNext()
    {
        // Guardar respuestas: 1 = Sí, 0 = No
        PlayerPrefs.SetInt("pending_vision", _visionIssue == true ? 1 : 0);
        PlayerPrefs.SetInt("pending_hearing", _hearingIssue == true ? 1 : 0);

        SceneManager.LoadScene(nextScene);
    }

    public void OnBack()
    {
        SceneManager.LoadScene(prevScene);
    }
}
