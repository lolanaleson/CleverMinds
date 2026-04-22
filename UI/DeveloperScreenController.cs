using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class DeveloperScreenController : MonoBehaviour
{
    [Header("Scroll (lista de usuarios)")]
    [SerializeField] private Transform usersContainer;     // Content del ScrollView
    [SerializeField] private GameObject userTextRowPrefab; // Prefab UserTextRow

    [Header("Mensajes")]
    [SerializeField] private TextMeshProUGUI errorLabel;

    [Header("Borrar uno")]
    [SerializeField] private TMP_InputField inputDeleteId;

    [Header("Editar apodo")]
    [SerializeField] private TMP_InputField inputEditId;
    [SerializeField] private TMP_InputField inputNewNickname;

    [Header("Editar visión / audición")]
    [SerializeField] private TMP_InputField inputSenseId;

    [Header("Escenas")]
    [SerializeField] private string mainMenuScene = "01_MainMenu";

    private void Start()
    {
        if (errorLabel != null) errorLabel.text = "";

        if (UserDirectoryService.I == null)
        {
            SetError("Servicio de usuarios no inicializado. Volviendo al menú.");
            SceneManager.LoadScene(mainMenuScene);
            return;
        }

        // Nada más entrar, refrescamos lista
        RefreshUserList();
    }

    // ================== LISTA DE USUARIOS (SCROLL) ==================

    public void RefreshUserList()
    {
        if (usersContainer == null || userTextRowPrefab == null)
        {
            SetError("Faltan referencias: usersContainer o userTextRowPrefab.");
            Debug.LogError("[DevScreen] usersContainer o userTextRowPrefab no asignados.");
            return;
        }

        // Borrar filas anteriores
        for (int i = usersContainer.childCount - 1; i >= 0; i--)
            Destroy(usersContainer.GetChild(i).gameObject);

        var users = UserDirectoryService.I.ListUsers();
        Debug.Log($"[DevScreen] Usuarios encontrados: {users.Count}");

        if (users.Count == 0)
        {
            // Mostrar una fila indicando que no hay usuarios
            var emptyRow = Instantiate(userTextRowPrefab, usersContainer);
            var rowScript = emptyRow.GetComponent<DevUserRow>();
            if (rowScript != null && rowScript.label != null)
            {
                rowScript.label.text = "No hay usuarios registrados.\n-----------------------------------";
            }
            return;
        }

        // Crear una fila por usuario
        foreach (var p in users)
        {
            if (p == null) continue;

            var rowObj = Instantiate(userTextRowPrefab, usersContainer);
            var row = rowObj.GetComponent<DevUserRow>();
            if (row != null)
            {
                row.Setup(p);
            }
            else
            {
                Debug.LogError("[DevScreen] El prefab userTextRowPrefab no tiene DevUserRow.");
            }
        }

        if (errorLabel != null) errorLabel.text = "";
    }

    // ================== Borrar todos ==================

    public void OnDeleteAllUsers()
    {
        bool ok = UserDirectoryService.I.DeleteAllUsers();
        if (ok)
        {
            SetError("Todos los usuarios han sido eliminados.");
            RefreshUserList();
        }
        else
        {
            SetError("Error al eliminar todos los usuarios.");
        }
    }

    // ================== Borrar uno ==================

    public void OnDeleteOneUser()
    {
        if (inputDeleteId == null)
        {
            SetError("Falta el campo de ID a borrar.");
            return;
        }

        string id = inputDeleteId.text.Trim();
        if (string.IsNullOrEmpty(id))
        {
            SetError("Introduce un ID para borrar.");
            return;
        }

        bool ok = UserDirectoryService.I.DeleteUser(id);
        if (ok)
        {
            SetError($"Usuario con ID {id} eliminado correctamente.");
            inputDeleteId.text = "";
            RefreshUserList();
        }
        else
        {
            SetError($"No existe usuario con ID {id}.");
        }
    }

    // ================== Editar apodo ==================

    public void OnUpdateNickname()
    {
        if (inputEditId == null || inputNewNickname == null)
        {
            SetError("Faltan campos para editar apodo.");
            return;
        }

        string id = inputEditId.text.Trim();
        string newNick = inputNewNickname.text.Trim();

        if (string.IsNullOrEmpty(id))
        {
            SetError("Introduce el ID del usuario a editar.");
            return;
        }
        if (string.IsNullOrEmpty(newNick))
        {
            SetError("Introduce el nuevo apodo.");
            return;
        }

        if (UserDirectoryService.I.UpdateNickname(id, newNick, out string err))
        {
            SetError($"Apodo del usuario {id} actualizado correctamente.");
            RefreshUserList();
        }
        else
        {
            SetError(err);
        }
    }

    // ================== Editar visión ==================

    public void OnSetVisionYes() => UpdateVisionForUser(true);
    public void OnSetVisionNo() => UpdateVisionForUser(false);

    private void UpdateVisionForUser(bool value)
    {
        if (inputSenseId == null)
        {
            SetError("Falta el ID para editar la visión.");
            return;
        }

        string id = inputSenseId.text.Trim();
        if (string.IsNullOrEmpty(id))
        {
            SetError("Introduce un ID de usuario.");
            return;
        }

        if (UserDirectoryService.I.UpdateVisionIssues(id, value, out string err))
        {
            SetError($"Visión del usuario {id} actualizada correctamente.");
            RefreshUserList();
        }
        else
        {
            SetError(err);
        }
    }

    // ================== Editar audición ==================

    public void OnSetHearingYes() => UpdateHearingForUser(true);
    public void OnSetHearingNo() => UpdateHearingForUser(false);

    private void UpdateHearingForUser(bool value)
    {
        if (inputSenseId == null)
        {
            SetError("Falta el ID para editar la audición.");
            return;
        }

        string id = inputSenseId.text.Trim();
        if (string.IsNullOrEmpty(id))
        {
            SetError("Introduce un ID de usuario.");
            return;
        }

        if (UserDirectoryService.I.UpdateHearingIssues(id, value, out string err))
        {
            SetError($"Audición del usuario {id} actualizada correctamente.");
            RefreshUserList();
        }
        else
        {
            SetError(err);
        }
    }

    // ================== Volver al menú ==================

    public void OnBackToMenu()
    {
        SceneManager.LoadScene(mainMenuScene);
    }

    private void SetError(string msg)
    {
        if (errorLabel != null)
            errorLabel.text = msg;

        Debug.LogWarning("[DevScreen] " + msg);
    }
}
