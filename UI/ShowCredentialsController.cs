using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ShowCredentialsController : MonoBehaviour
{
    public TextMeshProUGUI txtBody;
    public TextMeshProUGUI txtError;

    private void Start()
    {
        if (UserDirectoryService.I == null)
        {
            SceneManager.LoadScene("00_Bootstrap");
            return;
        }

        if (txtError) txtError.text = "";

        string nick = PlayerPrefs.GetString("pending_nickname", "").Trim();
        int age = PlayerPrefs.GetInt("pending_age", 0);
        Sex sex = (Sex)PlayerPrefs.GetInt("pending_sex", 0);

        int visionFlag = PlayerPrefs.GetInt("pending_vision", 0);
        int hearingFlag = PlayerPrefs.GetInt("pending_hearing", 0);

        if (string.IsNullOrEmpty(nick) || age == 0)
        {
            if (txtError) txtError.text = "Datos incompletos. Vuelve a empezar.";
            return;
        }

        if (!UserDirectoryService.I.CreateUserAutoFromAge(nick, age, sex,
                                                         out var profile, out string assignedId, out string err))
        {
            if (txtError) txtError.text = err;
            return;
        }

        // Sentidos
        profile.hasVisionIssues = (visionFlag == 1);
        profile.hasHearingIssues = (hearingFlag == 1);
        UserDirectoryService.I.TouchAndSaveCurrent();

        // Limpiar temporales
        PlayerPrefs.DeleteKey("pending_nickname");
        PlayerPrefs.DeleteKey("pending_age");
        PlayerPrefs.DeleteKey("pending_sex");
        PlayerPrefs.DeleteKey("pending_vision");
        PlayerPrefs.DeleteKey("pending_hearing");

        if (txtBody != null)
        {
            txtBody.text =
                $"Apodo: {profile.nickname}\n\n" +
                $"Tu ID es: {profile.localUserId}\n\n" +
                $"Tu contraseña será SIEMPRE tu año de nacimiento:\n" +
                $"{profile.birthYear}\n\n" +
                $"Guarda estos datos para poder volver a jugar en esta tablet.";
        }
    }

    public void OnContinue() => SceneManager.LoadScene("03_MinigameHub");
    public void OnBackToMenu() => SceneManager.LoadScene("01_MainMenu");
}

