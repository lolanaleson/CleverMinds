using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CarModelSO", menuName = "CleverMinds/Encuentra el coche/Car Model")]
public class CarModelSO : ScriptableObject
{
    public const string BLANCO = "BLANCO";
    public const string AZUL = "AZUL";
    public const string ROJO = "ROJO";
    public const string AMARILLO = "AMARILLO";
    public const string GRIS = "GRIS";
    public const string NEGRO = "NEGRO";
    public const string NARANJA = "NARANJA";
    public const string VERDE = "VERDE";
    public const string ROSA = "ROSA";

    [System.Serializable]
    public struct ColorSpriteVariant
    {
        public string colorName;
        public Sprite sprite;

        public ColorSpriteVariant(string colorName, Sprite sprite)
        {
            this.colorName = colorName;
            this.sprite = sprite;
        }
    }

    [Header("Datos del modelo")]
    [SerializeField] private string modelName;

    [Header("Sprites por color")]
    [SerializeField] private Sprite blancoSprite;
    [SerializeField] private Sprite azulSprite;
    [SerializeField] private Sprite rojoSprite;
    [SerializeField] private Sprite amarilloSprite;
    [SerializeField] private Sprite grisSprite;
    [SerializeField] private Sprite negroSprite;
    [SerializeField] private Sprite naranjaSprite;
    [SerializeField] private Sprite verdeSprite;
    [SerializeField] private Sprite rosaSprite;

    public string ModelName => modelName;

    public bool TryGetRandomVariant(out ColorSpriteVariant variant)
    {
        List<ColorSpriteVariant> availableVariants = GetAvailableVariants();

        if (availableVariants.Count == 0)
        {
            variant = default;
            return false;
        }

        variant = availableVariants[Random.Range(0, availableVariants.Count)];
        return true;
    }

    public bool TryGetSpriteByColor(string colorName, out Sprite sprite)
    {
        sprite = GetSpriteByColor(colorName);
        return sprite != null;
    }

    private List<ColorSpriteVariant> GetAvailableVariants()
    {
        List<ColorSpriteVariant> variants = new List<ColorSpriteVariant>(9);

        AddVariantIfValid(variants, BLANCO, blancoSprite);
        AddVariantIfValid(variants, AZUL, azulSprite);
        AddVariantIfValid(variants, ROJO, rojoSprite);
        AddVariantIfValid(variants, AMARILLO, amarilloSprite);
        AddVariantIfValid(variants, GRIS, grisSprite);
        AddVariantIfValid(variants, NEGRO, negroSprite);
        AddVariantIfValid(variants, NARANJA, naranjaSprite);
        AddVariantIfValid(variants, VERDE, verdeSprite);
        AddVariantIfValid(variants, ROSA, rosaSprite);

        return variants;
    }

    private void AddVariantIfValid(List<ColorSpriteVariant> variants, string colorName, Sprite sprite)
    {
        if (sprite == null) return;
        variants.Add(new ColorSpriteVariant(colorName, sprite));
    }

    private Sprite GetSpriteByColor(string colorName)
    {
        switch (NormalizeColorName(colorName))
        {
            case BLANCO: return blancoSprite;
            case AZUL: return azulSprite;
            case ROJO: return rojoSprite;
            case AMARILLO: return amarilloSprite;
            case GRIS: return grisSprite;
            case NEGRO: return negroSprite;
            case NARANJA: return naranjaSprite;
            case VERDE: return verdeSprite;
            case ROSA: return rosaSprite;
            default: return null;
        }
    }

    private string NormalizeColorName(string colorName)
    {
        return string.IsNullOrWhiteSpace(colorName)
            ? string.Empty
            : colorName.Trim().ToUpperInvariant();
    }
}
