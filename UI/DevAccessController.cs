using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controla el panel de acceso de desarrolladora:
/// - Muestra/oculta el panel.
/// - Comprueba la contraseña.
/// - Si es correcta, carga la escena 99_DevTools.
/// - Permite escribir la contraseña con un teclado numérico (0–9 + borrar).
/// </summary>
public class DevAccessController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;       // Panel_DevAccess
    [SerializeField] private TMP_InputField inputPassword;
    [SerializeField] private TextMeshProUGUI errorLabel;

    [Header("Config")]
    [SerializeField] private string devSceneName = "99_DevTools";

    // OJO: aquí defines la contraseña que QUIERAS
    [SerializeField] private string devPassword = "1963";

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);  // asegurarnos de que empieza oculto

        if (errorLabel != null)
            errorLabel.text = "";

        // Si quieres que el Input esté en modo contraseña (puntitos),
        // en el Inspector pon Content Type = Password (ya es suficiente).
        if (inputPassword != null)
        {
            inputPassword.text = "";
            inputPassword.readOnly = true; // lo escribe el teclado numérico
        }
    }

    /// <summary>
    /// Llamado desde el botón del menú principal "Developer".
    /// Muestra el panel y limpia el estado.
    /// </summary>
    public void ShowPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (inputPassword != null)
        {
            inputPassword.text = "";
            inputPassword.readOnly = true; // seguimos usando sólo los botones
        }

        if (errorLabel != null)
            errorLabel.text = "";
    }

    /// <summary>
    /// Llamado desde el botón "Volver" del panel.
    /// Oculta el panel sin hacer nada más.
    /// </summary>
    public void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (errorLabel != null)
            errorLabel.text = "";
    }

    /// <summary>
    /// Llamado desde el botón "Entrar".
    /// Comprueba la contraseña y, si es correcta, entra a la escena de desarrollo.
    /// </summary>
    public void OnConfirm()
    {
        if (inputPassword == null) return;

        string entered = (inputPassword.text ?? "").Trim();

        if (string.IsNullOrEmpty(entered))
        {
            SetError("Introduce una contraseña.");
            return;
        }

        if (entered == devPassword)
        {
            // Contraseña correcta → ir a la escena de desarrollo
            SceneManager.LoadScene(devSceneName);
        }
        else
        {
            SetError("Contraseña incorrecta.");
            inputPassword.text = "";
        }
    }

    private void SetError(string msg)
    {
        if (errorLabel != null)
            errorLabel.text = msg;

        Debug.LogWarning("[DevAccess] " + msg);
    }

    // ===================== TECLADO NUMÉRICO =====================

    /// <summary>
    /// Llamado por los botones del teclado numérico (0–9).
    /// </summary>
    public void OnKeyDigit(string digit)
    {
        if (inputPassword == null) return;
        if (string.IsNullOrEmpty(digit)) return;

        char c = digit[0];
        if (c < '0' || c > '9') return; // sólo permitimos números

        string current = inputPassword.text ?? "";

        // Opcional: limitar longitud, por ejemplo 8 caracteres
        // o a la longitud de la contraseña real.
        int maxLength = Mathf.Max(devPassword.Length, 8); // mínimo 8 por si cambias
        if (current.Length >= maxLength) return;

        inputPassword.text = current + c;
    }

    /// <summary>
    /// Borra el último carácter del campo de contraseña.
    /// </summary>
    public void OnKeyDelete()
    {
        if (inputPassword == null) return;

        string current = inputPassword.text ?? "";
        if (current.Length == 0) return;

        inputPassword.text = current.Substring(0, current.Length - 1);
    }

    /// <summary>
    /// (Opcional) Limpia todo el campo de contraseña.
    /// Úsalo si quieres un botón "CLR".
    /// </summary>
    public void OnKeyClear()
    {
        if (inputPassword == null) return;
        inputPassword.text = "";
    }
}
