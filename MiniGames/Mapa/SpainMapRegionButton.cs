using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SpainMapRegionButton : MonoBehaviour
{
    public enum RegionKind { Community, Province }

    [Header("Config")]
    public RegionKind kind = RegionKind.Community;
    public SpainCommunity communityId;
    public SpainProvince provinceId;

    [Header("Visual (Tint)")]
    [Tooltip("La Image del relleno exacto (tu PNG) que quieres teñir y que DEBE recibir el click.")]
    [SerializeField] private Image fillImage;

    [Tooltip("Color base/normal del relleno (incluye el alpha normal).")]
    [SerializeField] private Color baseColor = new Color(1f, 1f, 1f, 0f);


    [Tooltip("Alpha al mostrar feedback (para que NO se mezcle con el mapa).")]
    [Range(0f, 1f)]
    [SerializeField] private float feedbackAlpha = 0.9f;

    [Header("Alpha Raycast (Silueta)")]
    [Tooltip("Umbral de alpha para que el click solo funcione donde hay píxel visible del PNG.")]
    [Range(0f, 1f)]
    [SerializeField] private float alphaRaycastThreshold = 0.15f;

    [Tooltip("Si este GameObject tiene otra Image 'rectangular' (por ejemplo transparente), desactiva su raycast.")]
    [SerializeField] private bool disableRaycastOnOtherImages = true;

    [Header("Refs")]
    [SerializeField] private SpainMapGameManager gameManager;

    private Coroutine tintRoutine;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<SpainMapGameManager>();

        if (fillImage == null)
            fillImage = GetComponent<Image>();

        // Base color: si no lo configuraste, cogemos el actual
        if (fillImage != null && baseColor.a <= 0f)
            baseColor = fillImage.color;

        // 🔥 1) Asegurar que SOLO la fillImage recibe raycast
        if (disableRaycastOnOtherImages)
        {
            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img == null) continue;
                img.raycastTarget = (img == fillImage);
            }
        }
        else
        {
            if (fillImage != null) fillImage.raycastTarget = true;
        }

        // 🔥 2) Alpha hit test por código (click solo donde hay alpha)
        //     Requiere Read/Write Enabled en el import del PNG.
        if (fillImage != null)
            fillImage.alphaHitTestMinimumThreshold = alphaRaycastThreshold;

        // Hook al botón
        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnClicked);
        }

        ResetTint();
    }

    public void SetManager(SpainMapGameManager manager) => gameManager = manager;

    private void OnClicked()
    {
        if (gameManager == null) return;
        gameManager.OnRegionClicked(this);
    }

    public void ResetTint()
    {
        if (fillImage == null) return;

        if (tintRoutine != null)
        {
            StopCoroutine(tintRoutine);
            tintRoutine = null;
        }

        Color c = baseColor;
        c.a = 0f;           // 🔥 reposo invisible
        fillImage.color = c;
    }


    public void TintForFeedback(Color tintColor, float seconds)
    {
        if (fillImage == null) return;

        if (tintRoutine != null)
            StopCoroutine(tintRoutine);

        tintRoutine = StartCoroutine(TintCoroutine(tintColor, seconds));
    }

    private IEnumerator TintCoroutine(Color tintColor, float seconds)
    {
        Color c = tintColor;
        c.a = feedbackAlpha;
        fillImage.color = c;

        yield return new WaitForSeconds(seconds);

        fillImage.color = baseColor;
        tintRoutine = null;
    }

    public void SetTintPersistent(Color tintColor)
    {
        if (fillImage == null) return;

        if (tintRoutine != null)
        {
            StopCoroutine(tintRoutine);
            tintRoutine = null;
        }

        Color c = tintColor;
        c.a = feedbackAlpha;
        fillImage.color = c;
    }

    public void ClearTintToBase()
    {
        ResetTint();
    }

}
