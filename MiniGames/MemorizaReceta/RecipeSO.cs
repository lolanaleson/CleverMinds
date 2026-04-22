using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CleverMinds/Recipe Game/Recipe", fileName = "REC_")]
public class RecipeSO : ScriptableObject
{
    [Header("Datos")]
    public string recipeName;

    [Header("Visual receta")]
    public Sprite resultDishImage;

    [Header("Ingredientes (en orden)")]
    public List<IngredientSO> ingredients = new List<IngredientSO>();

    [Header("Audio (opcional)")]
    public AudioClip recipeNameAudio;
}