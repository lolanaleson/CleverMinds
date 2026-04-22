using UnityEngine;
using UnityEngine.UI;

public class IngredientOptionButton : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image ingredientImage;

    private RecipeMemoryGameManager manager;
    private IngredientSO ingredient;

    private void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnClicked);
    }

    public void Setup(RecipeMemoryGameManager gameManager, IngredientSO data)
    {
        manager = gameManager;
        ingredient = data;

        if (ingredientImage != null)
        {
            ingredientImage.sprite = ingredient != null ? ingredient.icon : null;
            ingredientImage.enabled = (ingredientImage.sprite != null);
        }
    }

    private void OnClicked()
    {
        if (manager == null || ingredient == null) return;
        manager.OnIngredientSelected(ingredient, this);
    }

    public void SetInteractable(bool interactable)
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.interactable = interactable;
    }
}
