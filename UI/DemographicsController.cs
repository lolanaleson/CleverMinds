using UnityEngine.EventSystems; 
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pantalla de edad y sexo. Ahora:
/// - Sexo con 3 botones tipo toggle (Hombre/Mujer/Otro) que se pueden deseleccionar.
/// - Edad con teclado numérico en pantalla (máx. 3 dígitos) + borrar.
/// - Validación:
///     * Edad válida según las reglas de UserDirectoryService (18-110).
///     * Sexo debe ser Hombre, Mujer u Otro (no Unspecified).
/// - Al continuar:
///     * NO crea el usuario.
///     * Guarda edad y sexo en PlayerPrefs (pending_age, pending_sex).
///     * Salta a la escena 02c_ShowCredentials, donde se creará el usuario
///       usando CreateUserAutoFromAge (ID automático + contraseña = año nacimiento).
/// </summary>
public class DemographicsController : MonoBehaviour
{
    [Header("UI refs")]
    [SerializeField] private TMP_InputField inputAge; // puesto en ReadOnly desde NumberPad
    [SerializeField] private Button continueButton;
    [SerializeField] private TextMeshProUGUI errorLabel;

    [Header("Gender Buttons")]
    [SerializeField] private GenderToggleButton btnFemale;
    [SerializeField] private GenderToggleButton btnMale;
    [SerializeField] private GenderToggleButton btnOther;

    [Header("Number Pad")]
    [SerializeField] private NumberPad numberPad; // para asegurar referencias (no indispensable)

    [Header("Siguiente escena")]
    public string nextSceneShowCredentials = "02b_Senses";

    [Header("Validation (rango sugerido, pero manda UserDirectoryService)")]
    [SerializeField] private int minAge = 18;
    [SerializeField] private int maxAge = 100;

    // Estado actual
    private Sex _selectedSex = Sex.Unspecified;
    private int _ageCached = 0;

    private void Start()
    {
        if (errorLabel != null) errorLabel.text = "";
        if (continueButton != null) continueButton.interactable = false;

#if UNITY_IOS || UNITY_ANDROID
    TouchScreenKeyboard.hideInput = true;
#endif

        // Ya NO precargamos nada desde GameSessionManager, porque aquí todavía
        // NO hay usuario creado. Este formulario es para NUEVOS usuarios.
        if (inputAge != null)
        {
            inputAge.text = "";
            inputAge.readOnly = true;        // lo rellena tu number pad
            inputAge.interactable = false;   // CLAVE: no seleccionable -> no teclado nativo
            inputAge.DeactivateInputField();
            inputAge.ForceLabelUpdate();
        }

        // Marcamos todos los toggles como deseleccionados visualmente
        btnFemale?.ForceSelect(false);
        btnMale?.ForceSelect(false);
        btnOther?.ForceSelect(false);

        _selectedSex = Sex.Unspecified;
        _ageCached = 0;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        ValidateForm();
    }

    /// <summary>
    /// Llamado por NumberPad cuando cambia el texto en inputAge
    /// </summary>
    public void OnAgeChanged(string newText)
    {
        if (string.IsNullOrEmpty(newText))
        {
            _ageCached = 0;
            ValidateForm();
            return;
        }

        if (!int.TryParse(newText, out int age))
        {
            _ageCached = 0;
            ValidateForm();
            return;
        }

        _ageCached = age;
        ValidateForm();
    }

    /// <summary>
    /// Llamado desde cada GenderToggleButton al togglear.
    /// Implementa la lógica de selección: si ningún botón queda activo, _selectedSex=Unspecified.
    /// Si uno queda activo, _selectedSex toma ese valor (exclusivo).
    /// </summary>
    public void OnGenderButtonToggled(GenderToggleButton who)
    {
        if (who.IsSelected)
        {
            // Exclusivo: si este se activa, desactivamos los otros
            if (btnFemale != null && btnFemale != who) btnFemale.ForceSelect(false);
            if (btnMale != null && btnMale != who) btnMale.ForceSelect(false);
            if (btnOther != null && btnOther != who) btnOther.ForceSelect(false);

            _selectedSex = who.mySex;
        }
        else
        {
            // Si se desactiva el que estaba seleccionado, queda ninguno
            _selectedSex = Sex.Unspecified;
        }

        ValidateForm();
    }

    private void ValidateForm()
    {
        if (errorLabel != null) errorLabel.text = "";
        if (continueButton != null) continueButton.interactable = false;

        // Comprobamos que el servicio de usuarios existe
        if (UserDirectoryService.I == null)
        {
            if (errorLabel != null)
                errorLabel.text = "Error interno: servicio de usuarios no inicializado.";
            return;
        }

        // Validar edad
        if (_ageCached == 0)
        {
            if (errorLabel != null)
                errorLabel.text = "Introduce tu edad con el teclado numérico.";
            return;
        }

        // Usamos la validación oficial del servicio (18-110)
        if (!UserDirectoryService.I.IsValidAge(_ageCached, out var reason))
        {
            if (errorLabel != null)
                errorLabel.text = reason;
            return;
        }

        // (Opcional) coherencia con los campos minAge/maxAge que tenías antes
        if (_ageCached < minAge || _ageCached > maxAge)
        {
            if (errorLabel != null)
                errorLabel.text = $"Edad fuera de rango ({minAge}–{maxAge}).";
            return;
        }

        // Validar sexo
        if (_selectedSex == Sex.Unspecified)
        {
            if (errorLabel != null)
                errorLabel.text = "Selecciona Hombre, Mujer u Otro.";
            return;
        }

        // Todo OK
        if (continueButton != null) continueButton.interactable = true;
    }

    public void OnContinue()
    {
        if (UserDirectoryService.I == null) return;

        // Guardamos la edad y el sexo como "pendientes"
        // El apodo ya debe estar guardado en PlayerPrefs por NicknameController: "pending_nickname"
        PlayerPrefs.SetInt("pending_age", _ageCached);
        PlayerPrefs.SetInt("pending_sex", (int)_selectedSex);

        // NO creamos aquí el usuario.
        // Vamos a la escena que mostrará ID + contraseña (año) y creará el perfil
        SceneManager.LoadScene(nextSceneShowCredentials);
    }

    public void OnBack()
    {
        SceneManager.LoadScene("01b_Nickname");
    }
}
