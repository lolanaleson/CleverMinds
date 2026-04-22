using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;


/// <summary>
/// Pantalla de login para usuarios EXISTENTES.
/// - Pide ID (1–4 cifras) y contraseña = AÑO DE NACIMIENTO (4 cifras).
/// - Los campos se rellenan con botones de un teclado numérico (0–9 + borrar).
/// - Al pulsar "Entrar", llama a UserDirectoryService.TryLogin(id, birthYear).
/// </summary>
public class LoginController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField inputId;          // Campo para el ID
    [SerializeField] private TMP_InputField inputBirthYear;   // Campo para la contraseña (año de nacimiento)
    [SerializeField] private TextMeshProUGUI errorLabel;

    [Header("Siguientes escenas")]
    [SerializeField] private string hubScene = "03_MinigameHub";
    [SerializeField] private string gatewayScene = "01a_UserGateway";

    private void Start()
    {
        if (UserDirectoryService.I == null)
        {
            // Si por lo que sea no está inicializado, volvemos al bootstrap (o al gateway)
            SceneManager.LoadScene("00_Bootstrap");
            return;
        }

        if (errorLabel != null) errorLabel.text = "";

#if UNITY_IOS || UNITY_ANDROID
    // Evita la "cajita" nativa en iOS si hubiera foco accidental.
    TouchScreenKeyboard.hideInput = true;
#endif

        if (inputId != null)
        {
            inputId.text = "";
            inputId.readOnly = true;         // lo rellena el teclado (tus botones)
            inputId.interactable = false;    // CLAVE: no seleccionable -> no teclado nativo
            inputId.DeactivateInputField();
            inputId.ForceLabelUpdate();
        }

        if (inputBirthYear != null)
        {
            inputBirthYear.text = "";
            inputBirthYear.readOnly = true;       // lo rellena el teclado (tus botones)
            inputBirthYear.interactable = false;  // CLAVE
            inputBirthYear.DeactivateInputField();
            inputBirthYear.ForceLabelUpdate();
        }

        // Quita el foco de cualquier UI por si algo quedó seleccionado
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    // ===================== MANEJO DEL CAMPO ID =====================

    /// <summary>
    /// Llamado por los botones del teclado numérico para escribir en el campo ID.
    /// </summary>
    public void OnIdDigit(string digit)
    {
        if (inputId == null) return;
        if (string.IsNullOrEmpty(digit)) return;

        // Aceptamos solo caracteres '0'..'9'
        char c = digit[0];
        if (c < '0' || c > '9') return;

        string current = inputId.text ?? "";

        // Máximo 4 caracteres para el ID
        if (current.Length >= 4) return;

        inputId.text = current + c;
    }

    /// <summary>
    /// Borra el último dígito del campo ID.
    /// </summary>
    public void OnIdDelete()
    {
        if (inputId == null) return;
        string current = inputId.text ?? "";
        if (current.Length == 0) return;
        inputId.text = current.Substring(0, current.Length - 1);
    }

    /// <summary>
    /// Limpia por completo el campo ID.
    /// </summary>
    public void OnIdClear()
    {
        if (inputId == null) return;
        inputId.text = "";
    }

    // ===================== MANEJO DEL CAMPO CONTRASEÑA (AÑO) =====================

    /// <summary>
    /// Llamado por los botones del teclado numérico para escribir en el campo contraseña
    /// (año de nacimiento: 4 cifras).
    /// </summary>
    public void OnPwdDigit(string digit)
    {
        if (inputBirthYear == null) return;
        if (string.IsNullOrEmpty(digit)) return;

        char c = digit[0];
        if (c < '0' || c > '9') return;

        string current = inputBirthYear.text ?? "";

        // Máximo 4 caracteres para el año de nacimiento
        if (current.Length >= 4) return;

        inputBirthYear.text = current + c;
    }

    /// <summary>
    /// Borra el último dígito del campo contraseña (año).
    /// </summary>
    public void OnPwdDelete()
    {
        if (inputBirthYear == null) return;
        string current = inputBirthYear.text ?? "";
        if (current.Length == 0) return;
        inputBirthYear.text = current.Substring(0, current.Length - 1);
    }

    /// <summary>
    /// Limpia por completo el campo contraseña (año).
    /// </summary>
    public void OnPwdClear()
    {
        if (inputBirthYear == null) return;
        inputBirthYear.text = "";
    }

    // ===================== LOGIN =====================

    public void OnLogin()
    {
        if (UserDirectoryService.I == null) return;

        string id = inputId != null ? (inputId.text ?? "").Trim() : "";
        string yearStr = inputBirthYear != null ? (inputBirthYear.text ?? "").Trim() : "";

        if (string.IsNullOrEmpty(id))
        {
            SetError("Introduce tu ID.");
            return;
        }

        if (string.IsNullOrEmpty(yearStr))
        {
            SetError("Introduce tu año de nacimiento.");
            return;
        }

        if (!int.TryParse(yearStr, out int birthYear))
        {
            SetError("Año de nacimiento no válido.");
            return;
        }

        if (yearStr.Length != 4)
        {
            // Opcional: exigir 4 cifras
            SetError("El año de nacimiento debe tener 4 cifras (ej: 1950).");
            return;
        }

        if (UserDirectoryService.I.TryLogin(id, birthYear, out string err))
        {
            // Login correcto
            SceneManager.LoadScene(hubScene);
        }
        else
        {
            SetError(err);
        }
    }

    public void OnBack()
    {
        SceneManager.LoadScene(gatewayScene);
    }

    private void SetError(string msg)
    {
        if (errorLabel != null) errorLabel.text = msg;
        Debug.LogWarning("[Login] " + msg);
    }
}
