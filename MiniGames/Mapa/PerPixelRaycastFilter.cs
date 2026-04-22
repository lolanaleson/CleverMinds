using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class PerPixelRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
{
    [Range(0f, 1f)]
    public float alphaThreshold = 0.15f;

    private Image img;
    private Sprite sprite;
    private Texture2D tex;

    private void Awake()
    {
        img = GetComponent<Image>();
        sprite = img.sprite;
        if (sprite != null) tex = sprite.texture;
    }

    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        if (img == null || img.sprite == null) return true;

        // Convertimos punto de pantalla a local dentro del rect
        RectTransform rt = img.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, sp, eventCamera, out Vector2 local))
            return false;

        Rect rect = rt.rect;

        // Normalizamos (0..1) dentro del rect
        float xNorm = (local.x - rect.x) / rect.width;
        float yNorm = (local.y - rect.y) / rect.height;

        // Fuera
        if (xNorm < 0f || xNorm > 1f || yNorm < 0f || yNorm > 1f)
            return false;

        Sprite s = img.sprite;
        Texture2D t = s.texture;
        if (t == null) return true;

        // Pasamos a coords dentro del rect del sprite (por si viene de atlas)
        Rect tr = s.textureRect;
        float u = (tr.x + tr.width * xNorm) / t.width;
        float v = (tr.y + tr.height * yNorm) / t.height;

        // Sample alpha
        Color c = t.GetPixelBilinear(u, v);

        return c.a >= alphaThreshold;
    }
}
