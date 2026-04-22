using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SelectLevelController : MonoBehaviour
{
    [Header("Textos (opcionales)")]
    public TextMeshProUGUI subtitle;

    [Header("Botones de nivel")]
    public Button btnL1;
    public Button btnL2;
    public Button btnL3;
    public Button btnL4;

    [Header("Candados (opcionales)")]
    public GameObject lockL2;
    public GameObject lockL3;
    public GameObject lockL4;

    [Header("Colores")]
    public Color unlockedColor = Color.white;
    public Color lockedColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Router de escenas")]
    public MiniGameSceneRouter sceneRouter;

    private MiniGameId _currentMiniGame;

    private void Start()
    {
        if (GameSessionManager.I == null || GameSessionManager.I.profile == null)
        {
            SceneManager.LoadScene("01a_UserGateway");
            return;
        }

        _currentMiniGame = GameSessionManager.I.currentSelection.miniGame;

        if (subtitle != null) subtitle.text = $"Minijuego: {_currentMiniGame}";

        bool u1 = GameSessionManager.I.IsLevelUnlocked(_currentMiniGame, LevelId.Level1);
        bool u2 = GameSessionManager.I.IsLevelUnlocked(_currentMiniGame, LevelId.Level2);
        bool u3 = GameSessionManager.I.IsLevelUnlocked(_currentMiniGame, LevelId.Level3);
        bool u4 = GameSessionManager.I.IsLevelUnlocked(_currentMiniGame, LevelId.Level4);

        ApplyLevelState(btnL1, u1, null);
        ApplyLevelState(btnL2, u2, lockL2);
        ApplyLevelState(btnL3, u3, lockL3);
        ApplyLevelState(btnL4, u4, lockL4);
    }

    public void PickLevel(int levelInt)
    {
        var level = (LevelId)levelInt;
        GameSessionManager.I.SelectMiniGameAndLevel(_currentMiniGame, level);

        if (sceneRouter == null)
        {
            Debug.LogError("[SelectLevel] Falta 'sceneRouter' asignado en el Inspector.");
            return;
        }

        string sceneName = sceneRouter.GetSceneFor(_currentMiniGame);
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"[SelectLevel] No hay escena configurada para {_currentMiniGame} en el router.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    public void BackToHub() => SceneManager.LoadScene("03_MinigameHub");

    private void ApplyLevelState(Button btn, bool unlocked, GameObject lockIcon)
    {
        if (btn == null) return;

        btn.interactable = unlocked;

        var img = btn.GetComponent<Image>();
        if (img != null) img.color = unlocked ? unlockedColor : lockedColor;

        if (lockIcon != null) lockIcon.SetActive(!unlocked);

        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.color = unlocked ? Color.black : new Color(0.3f, 0.3f, 0.3f, 1f);
    }
}
