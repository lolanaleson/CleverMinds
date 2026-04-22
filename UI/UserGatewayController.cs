using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UserGatewayController : MonoBehaviour
{
    public Button btnExisting;

    void Start()
    {
        // Si no existe el servicio de usuarios, volvemos al bootstrap
        if (UserDirectoryService.I == null)
        {
            SceneManager.LoadScene("00_Bootstrap");
            return;
        }

        bool hasAny = UserDirectoryService.I.HasAnyUser();

        // Si no hay ningún usuario, botón de "existente" se desactiva
        if (btnExisting != null)
            btnExisting.interactable = hasAny;
    }

    public void OnNewUser()
    {
        SceneManager.LoadScene("01b_Nickname");
    }

    public void OnExistingUser()
    {
        SceneManager.LoadScene("01c_Login");
    }

    public void OnBack()
    {
        SceneManager.LoadScene("01_MainMenu");
    }
}
