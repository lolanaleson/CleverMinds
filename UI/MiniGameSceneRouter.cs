using UnityEngine;

[CreateAssetMenu(fileName = "MiniGameSceneRouter", menuName = "CleverMinds/MiniGameSceneRouter")]
public class MiniGameSceneRouter : ScriptableObject
{
    [Header("Nombre de escena por minijuego")]
    public string desertScene = "10_Desert";
    public string maletasScene = "11_Maletas";
    public string burbujasScene = "12_Burbujas";
    public string bibliotecaScene = "13_Biblioteca";

    public string encajaLlaveScene = "14_EncajaLlave";
    public string encuentraElCocheScene = "15_EncuentraElCoche";
    public string memorizaRecetaScene = "16_MemorizaReceta";
    public string pagoExactoScene = "17_PagoExacto";
    public string maletaEquivocadaScene = "18_MaletaEquivocada";
    public string completaPalabra = "19_CompletaPalabra";
    public string colocaRelojScene = "20_ColocaReloj";
    public string mapaEspañaScene = "21_MapaEspaña";
    public string memorizaBodaScene = "22_MemorizaBoda";
    public string ordenaPesasScene = "23_OrdenaPesas";
    public string atrapaBTScene = "24_AtrapaBT";
    public string karaokePreguntaScene = "25_KaraokePregunta";
    public string culturaGeneralScene = "26_CulturaGeneral";

    public string GetSceneFor(MiniGameId id)
    {
        switch (id)
        {
            case MiniGameId.Desert: return desertScene;
            case MiniGameId.Maletas: return maletasScene;
            case MiniGameId.Burbujas: return burbujasScene;
            case MiniGameId.Biblioteca: return bibliotecaScene;


            case MiniGameId.EncajaLlave: return encajaLlaveScene;
            case MiniGameId.EncuentraElCoche: return encuentraElCocheScene;
            case MiniGameId.MemorizaReceta: return memorizaRecetaScene;
            case MiniGameId.PagoExacto: return pagoExactoScene;
            case MiniGameId.MaletaEquivocada: return maletaEquivocadaScene;
            case MiniGameId.CompletaPalabra: return completaPalabra;
            case MiniGameId.ColocaReloj: return colocaRelojScene;
            case MiniGameId.MapaEspaña: return mapaEspañaScene;
            case MiniGameId.MemorizaBoda: return memorizaBodaScene;
            case MiniGameId.OrdenaPesas: return ordenaPesasScene;
            case MiniGameId.AtrapaBT: return atrapaBTScene;
            case MiniGameId.Karaoke: return karaokePreguntaScene;
            case MiniGameId.CulturaGeneral: return culturaGeneralScene;


        }
        return null;
    }
}
