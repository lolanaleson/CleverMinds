using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KaraokeLyricsController : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("Si asignas Current/Next + LinesContainer, se usará el modo 'Spotify' (scroll). Si no, cae al modo antiguo (fade en un solo texto).")]
    [SerializeField] private bool preferSpotifyStyle = true;

    // =========================================================
    //  SONG TITLE UI
    // =========================================================
    [Header("Song Title UI")]
    [Tooltip("TMP arriba del panel de karaoke para mostrar el título de la canción.")]
    [SerializeField] private TextMeshProUGUI songTitleText;

    [Tooltip("Si songTitle está vacío, permite fallback al nombre del clip (karaokeClip o AudioSource.clip).")]
    [SerializeField] private bool fallbackToClipNameIfNoTitle = false;

    // =========================================================
    //  IMAGES SLIDESHOW (Panel Lyrics)
    // =========================================================
    [Header("Karaoke Images Slideshow (optional)")]
    [Tooltip("Primera Image (superpuesta a B). Se usa para crossfade.")]
    [SerializeField] private Image slideshowImageA;

    [Tooltip("Segunda Image (superpuesta a A). Se usa para crossfade.")]
    [SerializeField] private Image slideshowImageB;

    [Tooltip("Sprites que irán rotando en el panel.")]
    [SerializeField] private Sprite[] slideshowSprites;

    [Tooltip("Tiempo que una imagen se queda fija antes de pasar a la siguiente.")]
    [SerializeField] private float slideshowInterval = 4f;

    [Tooltip("Duración del crossfade entre imágenes (suave, sin flashazo).")]
    [SerializeField] private float slideshowFadeDuration = 0.6f;

    [Tooltip("Usar tiempo no escalado para que siga animando aunque uses Time.timeScale=0.")]
    [SerializeField] private bool slideshowUseUnscaledTime = true;

    [Tooltip("Si está ON, el orden de imágenes será aleatorio.")]
    [SerializeField] private bool slideshowRandom = false;

    [Tooltip("Activa/desactiva el slideshow.")]
    [SerializeField] private bool enableSlideshow = true;

    private Coroutine _slideshowCoroutine;
    private int _slideshowIndex;
    private bool _slideshowUsingA = true;

    // Colores base (incluida alpha) tal cual están en el Inspector
    private Color _baseColorA;
    private Color _baseColorB;
    private bool _slideshowBaseColorsCaptured;

    // =========================================================
    //  SPOTIFY STYLE (2 lines + smooth scroll)
    // =========================================================
    [Header("Spotify Style UI (recommended)")]
    [SerializeField] private RectTransform linesContainer;          // Padre que se moverá hacia arriba en la transición
    [SerializeField] private TextMeshProUGUI currentLineText;       // Línea actual (opaca)
    [SerializeField] private TextMeshProUGUI nextLineText;          // Siguiente línea (transparente)

    [Range(0f, 1f)]
    [SerializeField] private float nextLineAlpha = 0.35f;

    [Tooltip("Duración del desplazamiento entre versos (scroll).")]
    [SerializeField] private float scrollDuration = 0.25f;

    [Tooltip("Usar tiempo no escalado (recomendado si usas Time.timeScale=0 en pausa).")]
    [SerializeField] private bool useUnscaledTimeForScroll = true;

    // =========================================================
    //  FALLBACK (old single text + fade)
    // =========================================================
    [Header("Fallback (old single text + fade)")]
    [SerializeField] private TextMeshProUGUI lyricsText;
    [SerializeField] private CanvasGroup lyricsCanvasGroup; // opcional para fades
    [SerializeField] private float fadeDuration = 0.15f;

    // =========================================================
    //  Runtime
    // =========================================================
    private MusicQuizSongData _song;
    private AudioSource _audio;
    private int _currentIndex = -1;
    private bool _running;

    private bool _spotifyReady;
    private float _lineStep;                 // cuánto sube el container en cada transición
    private Coroutine _transitionCoroutine;

    public void Init(MusicQuizSongData song, AudioSource audioSource)
    {
        _song = song;
        _audio = audioSource;
        _currentIndex = -1;

        ApplySongTitleUI();
        SetupSlideshowInitialState();

        _spotifyReady =
            preferSpotifyStyle &&
            linesContainer != null &&
            currentLineText != null &&
            nextLineText != null;

        if (_spotifyReady)
        {
            PrepareLineStep();
            SetLine(currentLineText, "");
            SetLine(nextLineText, "");
            SetAlpha(currentLineText, 1f);
            SetAlpha(nextLineText, nextLineAlpha);
            linesContainer.anchoredPosition = Vector2.zero;
        }

        if (lyricsText) lyricsText.text = "";
        if (lyricsCanvasGroup) lyricsCanvasGroup.alpha = 1f;
    }

    private void ApplySongTitleUI()
    {
        if (songTitleText == null) return;

        string title = (_song != null) ? _song.songTitle : "";

        if (string.IsNullOrWhiteSpace(title) && fallbackToClipNameIfNoTitle)
        {
            if (_song != null && _song.karaokeClip != null) title = _song.karaokeClip.name;
            else if (_audio != null && _audio.clip != null) title = _audio.clip.name;
        }

        songTitleText.text = title ?? "";
        songTitleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(songTitleText.text));
    }

    public void StartKaraoke()
    {
        if (_song == null || _audio == null || _song.lines == null || _song.lines.Count == 0) return;

        _running = true;

        StopAllCoroutines();
        StartCoroutine(KaraokeRoutine());

        StartSlideshowIfPossible();
    }

    public void StopKaraoke()
    {
        _running = false;
        StopAllCoroutines();

        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }

        StopSlideshow();
    }

    private IEnumerator KaraokeRoutine()
    {
        while (_running && _audio != null && _audio.clip != null)
        {
            // Si está en pausa (audio pausado), no avanzamos letras.
            if (!_audio.isPlaying)
            {
                yield return null;
                continue;
            }

            float t = _audio.time;
            int nextIndex = GetActiveLineIndex(t);

            if (nextIndex != _currentIndex)
            {
                bool canAnimateOneStep = (nextIndex == _currentIndex + 1);
                _currentIndex = nextIndex;

                if (_spotifyReady)
                {
                    if (canAnimateOneStep && _currentIndex >= 0)
                        yield return TransitionToIndex(_currentIndex);
                    else
                        SnapToIndex(_currentIndex);
                }
                else
                {
                    string newText = (_currentIndex >= 0) ? _song.lines[_currentIndex].text : "";
                    yield return SetTextWithFade(newText);
                }
            }

            if (t >= _audio.clip.length - 0.02f)
                break;

            yield return null;
        }
    }

    // Devuelve el índice de la frase activa: la última cuya startTime <= t
    private int GetActiveLineIndex(float t)
    {
        int idx = -1;
        for (int i = 0; i < _song.lines.Count; i++)
        {
            if (_song.lines[i].startTime <= t) idx = i;
            else break; // asumimos que lines está ordenado por startTime
        }
        return idx;
    }

    // =========================================================
    //  SPOTIFY STYLE LOGIC
    // =========================================================

    private void PrepareLineStep()
    {
        float y0 = currentLineText.rectTransform.anchoredPosition.y;
        float y1 = nextLineText.rectTransform.anchoredPosition.y;
        _lineStep = Mathf.Abs(y0 - y1);

        if (_lineStep < 1f)
            _lineStep = Mathf.Max(40f, currentLineText.preferredHeight);
    }

    private void SnapToIndex(int idx)
    {
        if (!_spotifyReady) return;

        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }

        linesContainer.anchoredPosition = Vector2.zero;

        string current = (idx >= 0 && idx < _song.lines.Count) ? _song.lines[idx].text : "";
        string next = (idx + 1 >= 0 && idx + 1 < _song.lines.Count) ? _song.lines[idx + 1].text : "";

        SetLine(currentLineText, current);
        SetLine(nextLineText, next);

        SetAlpha(currentLineText, 1f);
        SetAlpha(nextLineText, nextLineAlpha);
    }

    private IEnumerator TransitionToIndex(int idx)
    {
        if (idx <= 0)
        {
            SnapToIndex(idx);
            yield break;
        }

        string expectedNext = _song.lines[idx].text;
        if (nextLineText.text != expectedNext)
        {
            SnapToIndex(idx);
            yield break;
        }

        float elapsed = 0f;
        Vector2 start = Vector2.zero;
        Vector2 end = new Vector2(0f, _lineStep);

        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }

        while (elapsed < scrollDuration)
        {
            float dt = useUnscaledTimeForScroll ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;

            float k = (scrollDuration <= 0f) ? 1f : Mathf.Clamp01(elapsed / scrollDuration);
            linesContainer.anchoredPosition = Vector2.Lerp(start, end, k);

            yield return null;
        }

        linesContainer.anchoredPosition = Vector2.zero;

        SetLine(currentLineText, expectedNext);
        SetAlpha(currentLineText, 1f);

        string newNext = (idx + 1 < _song.lines.Count) ? _song.lines[idx + 1].text : "";
        SetLine(nextLineText, newNext);
        SetAlpha(nextLineText, nextLineAlpha);
    }

    private void SetLine(TextMeshProUGUI tmp, string text)
    {
        if (tmp) tmp.text = text ?? "";
    }

    private void SetAlpha(TextMeshProUGUI tmp, float a)
    {
        if (!tmp) return;
        Color c = tmp.color;
        c.a = Mathf.Clamp01(a);
        tmp.color = c;
    }

    // =========================================================
    //  FALLBACK (old fade)
    // =========================================================

    private IEnumerator SetTextWithFade(string newText)
    {
        if (lyricsText == null) yield break;

        if (lyricsCanvasGroup == null || fadeDuration <= 0f)
        {
            lyricsText.text = newText;
            yield break;
        }

        yield return FadeTo(0f);
        lyricsText.text = newText;
        yield return FadeTo(1f);
    }

    private IEnumerator FadeTo(float target)
    {
        float start = lyricsCanvasGroup.alpha;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / fadeDuration);
            lyricsCanvasGroup.alpha = Mathf.Lerp(start, target, k);
            yield return null;
        }

        lyricsCanvasGroup.alpha = target;
    }

    // =========================================================
    //  SLIDESHOW LOGIC (Crossfade respetando alpha del inspector)
    // =========================================================

    private void SetupSlideshowInitialState()
    {
        if (!enableSlideshow) return;

        bool ok =
            slideshowImageA != null &&
            slideshowImageB != null &&
            slideshowSprites != null &&
            slideshowSprites.Length > 0;

        if (!ok) return;

        // Capturamos los colores base (incluye alpha del inspector) una sola vez
        if (!_slideshowBaseColorsCaptured)
        {
            _baseColorA = slideshowImageA.color;
            _baseColorB = slideshowImageB.color;
            _slideshowBaseColorsCaptured = true;
        }

        _slideshowIndex = 0;
        _slideshowUsingA = true;

        slideshowImageA.sprite = slideshowSprites[0];

        // Visible pero respetando alpha base
        SetImageFade(slideshowImageA, 1f);
        SetImageFade(slideshowImageB, 0f);

        // Preload para evitar “flash” en editor
        slideshowImageB.sprite = slideshowSprites[Mathf.Min(1, slideshowSprites.Length - 1)];
    }

    private void StartSlideshowIfPossible()
    {
        if (!enableSlideshow) return;

        bool ok =
            slideshowImageA != null &&
            slideshowImageB != null &&
            slideshowSprites != null &&
            slideshowSprites.Length > 1 &&
            slideshowInterval > 0.05f;

        if (!ok) return;

        StopSlideshow();
        _slideshowCoroutine = StartCoroutine(SlideshowRoutine());
    }

    private void StopSlideshow()
    {
        if (_slideshowCoroutine != null)
        {
            StopCoroutine(_slideshowCoroutine);
            _slideshowCoroutine = null;
        }
    }

    private IEnumerator SlideshowRoutine()
    {
        // Espera inicial
        if (slideshowUseUnscaledTime) yield return new WaitForSecondsRealtime(slideshowInterval);
        else yield return new WaitForSeconds(slideshowInterval);

        while (_running)
        {
            int next = GetNextSlideshowIndex();
            yield return CrossfadeToSprite(slideshowSprites[next]);
            _slideshowIndex = next;

            if (slideshowUseUnscaledTime) yield return new WaitForSecondsRealtime(slideshowInterval);
            else yield return new WaitForSeconds(slideshowInterval);
        }
    }

    private int GetNextSlideshowIndex()
    {
        if (slideshowSprites == null || slideshowSprites.Length == 0) return 0;

        if (slideshowRandom)
        {
            if (slideshowSprites.Length == 1) return 0;

            int tries = 6;
            int r = _slideshowIndex;
            while (tries-- > 0 && r == _slideshowIndex)
                r = Random.Range(0, slideshowSprites.Length);

            return r;
        }

        return (_slideshowIndex + 1) % slideshowSprites.Length;
    }

    private IEnumerator CrossfadeToSprite(Sprite nextSprite)
    {
        if (slideshowImageA == null || slideshowImageB == null) yield break;

        Image from = _slideshowUsingA ? slideshowImageA : slideshowImageB;
        Image to = _slideshowUsingA ? slideshowImageB : slideshowImageA;

        to.sprite = nextSprite;

        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, slideshowFadeDuration);

        // Asegura inicial
        SetImageFade(from, 1f);
        SetImageFade(to, 0f);

        while (elapsed < dur)
        {
            float dt = slideshowUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;

            float k = Mathf.Clamp01(elapsed / dur);
            SetImageFade(from, 1f - k);
            SetImageFade(to, k);

            yield return null;
        }

        SetImageFade(from, 0f);
        SetImageFade(to, 1f);

        _slideshowUsingA = !_slideshowUsingA;
    }

    /// <summary>
    /// fade01: 0..1. Respeta la alpha base que hayas puesto en el Inspector.
    /// Es decir: alphaFinal = alphaInspector * fade01
    /// </summary>
    private void SetImageFade(Image img, float fade01)
    {
        if (!img) return;
        fade01 = Mathf.Clamp01(fade01);

        if (img == slideshowImageA)
        {
            Color c = _baseColorA;      // mantiene RGB del inspector
            c.a = _baseColorA.a * fade01;
            img.color = c;
        }
        else if (img == slideshowImageB)
        {
            Color c = _baseColorB;
            c.a = _baseColorB.a * fade01;
            img.color = c;
        }
        else
        {
            // Fallback por si alguien asigna otra Image distinta:
            Color c = img.color;
            float baseA = c.a;
            c.a = baseA * fade01;
            img.color = c;
        }
    }
}


