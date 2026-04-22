using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class NicknameController : MonoBehaviour
{
    public TMP_InputField inputNickname;
    public TextMeshProUGUI errorLabel;

    void Start()
    {
        if (UserDirectoryService.I == null)
        {
            SceneManager.LoadScene("00_Bootstrap");
            return;
        }
        if (errorLabel) errorLabel.text = "";
    }

    public void OnNext()
    {
        string nick = inputNickname != null ? inputNickname.text.Trim() : "";

        if (!UserDirectoryService.I.IsValidNickname(nick, out var reason))
        {
            if (errorLabel) errorLabel.text = reason;
            return;
        }

        // Guardamos temporalmente el apodo; el usuario se crea más adelante
        PlayerPrefs.SetString("pending_nickname", nick);

        // Ahora vamos a la pantalla de demographics
        SceneManager.LoadScene("02_Demographics");
    }

    public void OnBack()
    {
        SceneManager.LoadScene("01a_UserGateway");
    }
}
