using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements; // (NO hace falta) -> lo quitamos abajo

public class MinigameHubPager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private UnityEngine.UI.Button leftArrow;
    [SerializeField] private UnityEngine.UI.Button rightArrow;

    [Header("Animación")]
    [SerializeField] private float slideDuration = 0.25f;
    [SerializeField] private AnimationCurve slideEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private int currentPage = 0;
    private int pageCount = 1;
    private float pageWidth;
    private Coroutine slideRoutine;

    private void Awake()
    {
        if (leftArrow) leftArrow.onClick.AddListener(GoLeft);
        if (rightArrow) rightArrow.onClick.AddListener(GoRight);
    }

    private void Start()
    {
        Rebuild();
        JumpToPage(0);
    }

    private void OnEnable()
    {
        Rebuild();
        ClampAndRefresh();
    }

    /// <summary>
    /// ✅ En la versión "Wii exacta", el Content tiene PÁGINAS como hijos:
    /// Content
    ///   Page_0 (Grid 3x2)
    ///   Page_1 (Grid 3x2)
    ///   ...
    /// Por tanto, pageCount = content.childCount
    /// </summary>
    public void Rebuild()
    {
        if (!viewport || !content) return;

        // Forzar recalculo de layouts antes de leer sizes (evita pageWidth = 0 en algunos casos)
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        pageWidth = viewport.rect.width;
        pageCount = Mathf.Max(1, content.childCount);

        ClampAndRefresh();

        // Recoloca el content en la página actual (por si cambió el tamaño)
        JumpToPage(currentPage);
    }

    public void GoLeft() => SlideToPage(currentPage - 1);
    public void GoRight() => SlideToPage(currentPage + 1);

    public void SlideToPage(int pageIndex)
    {
        Debug.Log("He cliqueado!!!!");
        if (!viewport || !content) return;

        pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
        if (pageIndex == currentPage) return;

        currentPage = pageIndex;
        RefreshArrows();

        Vector2 from = content.anchoredPosition;
        Vector2 to = new Vector2(-currentPage * pageWidth, from.y);

        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(Slide(from, to));
    }

    public void JumpToPage(int pageIndex)
    {
        if (!viewport || !content) return;

        pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
        currentPage = pageIndex;

        Vector2 pos = content.anchoredPosition;
        content.anchoredPosition = new Vector2(-currentPage * pageWidth, pos.y);

        RefreshArrows();
    }

    private void ClampAndRefresh()
    {
        currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
        RefreshArrows();
    }

    private void RefreshArrows()
    {
        // Tus flechas Wii se quedan tal cual: solo las activamos/desactivamos
        if (leftArrow) leftArrow.gameObject.SetActive(currentPage > 0);
        if (rightArrow) rightArrow.gameObject.SetActive(currentPage < pageCount - 1);
    }

    private IEnumerator Slide(Vector2 from, Vector2 to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / slideDuration;
            float e = slideEase != null ? slideEase.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);
            content.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
            yield return null;
        }

        content.anchoredPosition = to;
        slideRoutine = null;
    }
}

