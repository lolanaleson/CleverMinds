// Assets/Scripts/Core/BootstrapLoader.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Loader inicial:
/// - asegura deviceId antes de cargar el menú
/// </summary>
public class BootstrapLoader : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "01_MainMenu";
    [SerializeField] private bool loadAsync = true;

    private IEnumerator Start()
    {
        Debug.Log("persistentDataPath = " + Application.persistentDataPath);

        DeviceIdentity.EnsureDeviceId(); // importantísimo: 1 vez por instalación
        yield return null;

        if (loadAsync)
        {
            var op = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
        }
        else
        {
            SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
        }
    }
}


