using UnityEngine;

[CreateAssetMenu(menuName = "CleverMinds/Recipe Game/Ingredient", fileName = "ING_")]
public class IngredientSO : ScriptableObject
{
    [Header("Datos")]
    public string ingredientName;

    [Header("Visual")]
    public Sprite icon;

    [Header("Audio (opcional)")]
    public AudioClip audioClip;
}
