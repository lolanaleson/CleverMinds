using UnityEngine;
using UnityEngine.UI;

public class IngredientIntroUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image ingredientImage;
    [SerializeField] private GameObject haloObject;

    public void Setup(IngredientSO ingredient)
    {
        if (ingredientImage != null)
        {
            ingredientImage.sprite = ingredient != null ? ingredient.icon : null;
            ingredientImage.enabled = (ingredientImage.sprite != null);
        }

        SetHaloActive(false);
    }

    public void SetHaloActive(bool active)
    {
        if (haloObject != null)
            haloObject.SetActive(active);
    }
}