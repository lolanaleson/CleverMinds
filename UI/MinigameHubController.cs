using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MinigameHubController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI profileInfo;

    [Header("Escenas")]
    [SerializeField] private string selectLevelScene = "04_SelectLevel";
    [SerializeField] private string gatewayScene = "01a_UserGateway";
    [SerializeField] private string mainMenuScene = "01_MainMenu";

    private void Start()
    {
        // Asegurarnos de que hay sesión y usuario
        if (GameSessionManager.I == null || GameSessionManager.I.profile == null)
        {
            SceneManager.LoadScene(gatewayScene);
            return;
        }

        var p = GameSessionManager.I.profile;

        if (profileInfo)
        {
            profileInfo.text =
                $"Apodo: {p.nickname}\n" +
                $"ID: {p.localUserId}\n" +
                $"Edad: {p.age}\n" +
                $"Género: {GenderLabel(p.gender)}\n" +
                $"Visión: {(p.hasVisionIssues ? "Sí" : "No")}\n" +
                $"Audición: {(p.hasHearingIssues ? "Sí" : "No")}";
        }
    }

    private string GenderLabel(string g)
    {
        switch ((g ?? "U").Trim().ToUpperInvariant())
        {
            case "F": return "Mujer";
            case "M": return "Hombre";
            case "O": return "Otro";
            default: return "—";
        }
    }

    // --------------------------------------------------------------------
    // ✅ TU FUNCIÓN CENTRAL (la que querías): Go (pública)
    // --------------------------------------------------------------------
    public void Go(MiniGameId id)
    {
        GameSessionManager.I.SelectMiniGameAndLevel(id, LevelId.Level1);
        SceneManager.LoadScene(selectLevelScene);
    }

    // --------------------------------------------------------------------
    // ✅ Alias genérico por si lo llamas desde celdas
    // --------------------------------------------------------------------
    public void SelectMinigame(MiniGameId id) => Go(id);

    // --------------------------------------------------------------------
    // ✅ Botones rápidos (tu flujo actual)
    // --------------------------------------------------------------------
    public void OnGoDesert() => Go(MiniGameId.Desert);
    public void OnGoMaletas() => Go(MiniGameId.Maletas);
    public void OnGoBurbujas() => Go(MiniGameId.Burbujas);
    public void OnGoBiblioteca() => Go(MiniGameId.Biblioteca);

    /// <summary>
    /// LOS MINIJUEGOS A PARTIR DE AQUÍ SON LOS OFICIALES
    /// </summary>
    public void OnGoEncajaLlave() => Go(MiniGameId.EncajaLlave);
    public void OnGoEncuentraElCoche() => Go(MiniGameId.EncuentraElCoche);
    public void OnGoMemorizaReceta() => Go(MiniGameId.MemorizaReceta);
    public void OnGoPagoExacto() => Go(MiniGameId.PagoExacto);
    public void OnGoMaletaEquivocada() => Go(MiniGameId.MaletaEquivocada);
    public void OnGoCompletaPalabra() => Go(MiniGameId.CompletaPalabra);
    public void OnGoColocaReloj() => Go(MiniGameId.ColocaReloj);
    public void OnGoMapaEspaña() => Go(MiniGameId.MapaEspaña);
    public void OnGoMemorizaBoda() => Go(MiniGameId.MemorizaBoda);
    public void OnGoOrdenaPesas() => Go(MiniGameId.OrdenaPesas);
    public void OnGoAtrapaBT() => Go(MiniGameId.AtrapaBT);
    public void OnGoKaraoke() => Go(MiniGameId.Karaoke);
    public void OnGoCulturaGeneral() => Go(MiniGameId.CulturaGeneral);

    public void OnLogout()
    {
        UserDirectoryService.I?.Logout();
        SceneManager.LoadScene(mainMenuScene);
    }
}


