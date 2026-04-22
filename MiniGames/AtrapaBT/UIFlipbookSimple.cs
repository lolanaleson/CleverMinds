using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UIFlipbookSimple : MonoBehaviour
{
    [Header("Frames")]
    public Sprite[] frames;

    [Header("Playback")]
    [Min(1f)] public float fps = 10f;
    public bool loop = true;
    public bool playOnEnable = true;

    private Image _img;
    private int _index;
    private float _t;
    private bool _playing;

    private void Awake()
    {
        _img = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (playOnEnable) Play(true);
    }

    public void Play(bool restart = true)
    {
        if (frames == null || frames.Length == 0)
        {
            _playing = false;
            return;
        }

        if (restart)
        {
            _index = 0;
            _t = 0f;
            _img.sprite = frames[0];
        }

        _playing = true;
    }

    public void Stop(bool keepCurrentFrame = true)
    {
        _playing = false;
        if (!keepCurrentFrame && frames != null && frames.Length > 0)
        {
            _index = 0;
            _img.sprite = frames[0];
        }
    }

    private void Update()
    {
        if (!_playing) return;
        if (frames == null || frames.Length == 0) return;

        float frameTime = 1f / fps;
        _t += Time.unscaledDeltaTime; // UI: no depende del timescale

        while (_t >= frameTime)
        {
            _t -= frameTime;
            _index++;

            if (_index >= frames.Length)
            {
                if (loop) _index = 0;
                else { _index = frames.Length - 1; _playing = false; }
            }

            _img.sprite = frames[_index];
        }
    }
}

