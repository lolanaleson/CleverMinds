using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class BushPeekActor : MonoBehaviour, IPointerClickHandler
{
    public enum CharacterId { Benito, Teodora }
    public enum ActorMode { BushPeek, WalkPath }
    public enum Side { Left, Right }
    public enum BaseSide { Left, Right }
    public enum BushFlipMethod { ScaleX, RotateY180 }

    [Header("Identidad")]
    public CharacterId characterId = CharacterId.Teodora;

    [Header("Modo")]
    public ActorMode mode = ActorMode.BushPeek;

    [Header("Actor (RectTransform que se mueve)")]
    public RectTransform actor;

    [Header("Mirror")]
    public float mirrorCenterX = 0f;

    [Header("BushPeek - Base side (dónde están tus puntos BASE)")]
    public BaseSide baseSide = BaseSide.Left;

    [Header("Catch (tap)")]
    public bool enableCatch = true;
    public bool catchOnlyWhenVisible = true;
    public bool disableOnCatch = true;

    [Header("Pause")]
    [Tooltip("Si true, este actor se congela cuando el manager pausa.")]
    public bool freezeWhenPaused = true;

    // =========================
    // BUSH
    // =========================
    [Header("BushPeek - Puntos BASE")]
    public RectTransform hideBase;
    public RectTransform peekBase;

    [Header("BushPeek - Side")]
    public bool randomSideEachCycle = true;
    public Side fixedSide = Side.Left;

    [Header("BushPeek - Timing (NATURAL)")]
    public float moveSeconds = 0.28f;
    public Vector2 hideSecondsRange = new Vector2(0.6f, 1.1f);
    public Vector2 peekSecondsRange = new Vector2(1.2f, 1.8f);

    [Header("BushPeek - Mirar al centro")]
    public bool flipWhenOnLeftSide = true;
    public bool flipWhenOnRightSide = false;

    [Header("BushPeek - Flip Method (solo arbustos)")]
    public BushFlipMethod bushFlipMethod = BushFlipMethod.RotateY180;

    // =========================
    // WALK
    // =========================
    [Header("WalkPath - Puntos")]
    public RectTransform walkPosA;
    public RectTransform walkPosB;

    [Header("WalkPath - Movimiento")]
    public float walkSpeed = 260f;
    public bool randomDirectionEachLoop = true;

    // =========================
    // Eventos
    // =========================
    public event Action<BushPeekActor> Caught;
    public event Action<BushPeekActor> WrongTapped;

    // =========================
    // Estado interno
    // =========================
    private Coroutine _routine;
    private bool _isVisibleNow;
    private bool _caught;

    private CharacterId _currentTarget;
    private bool _sessionConfigured;
    private Action _onFinishedOneShot;

    private bool _paused;
    private Side _currentBushSide;

    private void Reset()
    {
        actor = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (actor == null) actor = GetComponent<RectTransform>();
        ResetVisual();
    }

    private void OnValidate()
    {
        if (actor == null) actor = GetComponent<RectTransform>();
        if (moveSeconds < 0.01f) moveSeconds = 0.01f;
        if (walkSpeed < 0f) walkSpeed = 0f;
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
    }

    public void ConfigureTarget(CharacterId target, bool enableCatch)
    {
        _currentTarget = target;
        this.enableCatch = enableCatch;

        _caught = false;
        _isVisibleNow = false;

        _sessionConfigured = true;
        ResetVisual();
    }

    // Alias compatibilidad
    public void ConfigureForSession(CharacterId target, bool enableCatch)
    {
        ConfigureTarget(target, enableCatch);
    }

    public void SetMirrorCenterX(float x) => mirrorCenterX = x;

    public void Stop()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        _onFinishedOneShot = null;
        _isVisibleNow = false;
    }

    public void PlayBushOnce(Action onFinished = null, Side? forceSide = null)
    {
        Stop();
        mode = ActorMode.BushPeek;
        _onFinishedOneShot = onFinished;

        _caught = false;
        _isVisibleNow = false;

        if (!_sessionConfigured)
        {
            // El manager debe llamar ConfigureTarget/ConfigureForSession
        }

        if (hideBase == null || peekBase == null)
        {
            Debug.LogWarning($"[BushPeekActor] Falta hideBase/peekBase en {name}");
            FinishOneShot();
            return;
        }

        _currentBushSide = forceSide ?? GetNextSide();
        _routine = StartCoroutine(BushOnceRoutine(_currentBushSide));
    }

    public void PlayWalkOnce(Action onFinished = null, bool? forceAToB = null)
    {
        Stop();
        mode = ActorMode.WalkPath;
        _onFinishedOneShot = onFinished;

        _caught = false;
        _isVisibleNow = false;

        if (walkPosA == null || walkPosB == null)
        {
            Debug.LogWarning($"[BushPeekActor] Falta walkPosA/walkPosB en {name}");
            FinishOneShot();
            return;
        }

        bool aToB = forceAToB ?? (randomDirectionEachLoop ? (UnityEngine.Random.value < 0.5f) : true);
        _routine = StartCoroutine(WalkOnceRoutine(aToB));
    }

    // =========================
    // BUSH: hide → peek → hide
    // =========================
    private IEnumerator BushOnceRoutine(Side side)
    {
        SnapTo(hideBase, side);
        _isVisibleNow = false;

        yield return WaitRealtimePausable(RandomRangeSafe(hideSecondsRange));

        _isVisibleNow = true;
        yield return MovePausable(GetPos(hideBase, side), GetPos(peekBase, side), moveSeconds);

        yield return WaitRealtimePausable(RandomRangeSafe(peekSecondsRange));

        yield return MovePausable(GetPos(peekBase, side), GetPos(hideBase, side), moveSeconds);

        _isVisibleNow = false;
        FinishOneShot();
    }

    // =========================
    // WALK: A → B (llega siempre)
    // =========================
    private IEnumerator WalkOnceRoutine(bool aToB)
    {
        Vector2 A = walkPosA.anchoredPosition;
        Vector2 B = walkPosB.anchoredPosition;

        Vector2 from = aToB ? A : B;
        Vector2 to = aToB ? B : A;

        actor.anchoredPosition = from;
        _isVisibleNow = true;

        ApplyFlipWalk(from, to);

        while (!_caught && !Mathf.Approximately(actor.anchoredPosition.x, to.x))
        {
            if (freezeWhenPaused && _paused)
            {
                yield return null;
                continue;
            }

            float step = Mathf.Max(0.01f, walkSpeed) * Time.unscaledDeltaTime;
            float newX = Mathf.MoveTowards(actor.anchoredPosition.x, to.x, step);
            actor.anchoredPosition = new Vector2(newX, actor.anchoredPosition.y);
            yield return null;
        }

        _isVisibleNow = false;
        FinishOneShot();
    }

    private void FinishOneShot()
    {
        var cb = _onFinishedOneShot;
        _onFinishedOneShot = null;
        _routine = null;
        cb?.Invoke();
    }

    // =========================
    // TAP
    // =========================
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!enableCatch) return;
        if (_caught) return;
        if (catchOnlyWhenVisible && !_isVisibleNow) return;

        bool isCorrect = (characterId == _currentTarget);
        if (!isCorrect)
        {
            WrongTapped?.Invoke(this);
            return;
        }

        _caught = true;

        var cb = _onFinishedOneShot;

        Stop();
        Caught?.Invoke(this);

        if (disableOnCatch)
            gameObject.SetActive(false);

        cb?.Invoke();
    }

    // =========================
    // Mirror helpers
    // =========================
    private Side GetNextSide()
    {
        if (!randomSideEachCycle) return fixedSide;
        return (UnityEngine.Random.value < 0.5f) ? Side.Left : Side.Right;
    }

    private Vector2 GetPos(RectTransform basePoint, Side side)
    {
        Vector2 p = basePoint.anchoredPosition;

        bool askingForMirrored =
            (baseSide == BaseSide.Left && side == Side.Right) ||
            (baseSide == BaseSide.Right && side == Side.Left);

        if (askingForMirrored)
            p.x = (2f * mirrorCenterX) - p.x;

        return p;
    }

    // ✅ Flip se decide EN HIDE por SIDE y ya no cambia (sin saltito)
    private void SnapTo(RectTransform basePoint, Side side)
    {
        actor.anchoredPosition = GetPos(basePoint, side);
        ApplyBushFlipForSide(side);
    }

    private IEnumerator MovePausable(Vector2 from, Vector2 to, float seconds)
    {
        if (seconds <= 0f)
        {
            actor.anchoredPosition = to;
            yield break;
        }

        float t = 0f;
        actor.anchoredPosition = from;

        while (!_caught && t < seconds)
        {
            if (freezeWhenPaused && _paused)
            {
                yield return null;
                continue;
            }

            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            actor.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            yield return null;
        }

        actor.anchoredPosition = to;
    }

    private void ApplyBushFlipForSide(Side side)
    {
        ResetVisual();

        bool shouldFlip = (side == Side.Left) ? flipWhenOnLeftSide : flipWhenOnRightSide;
        if (!shouldFlip) return;

        if (bushFlipMethod == BushFlipMethod.RotateY180)
        {
            Vector3 eul = actor.localEulerAngles;
            eul.y = 180f;
            actor.localEulerAngles = eul;
        }
        else
        {
            Vector3 s = actor.localScale;
            s.x = -Mathf.Abs(s.x);
            actor.localScale = s;
        }
    }

    // WALK flip (sprites base miran hacia la izquierda)
    private void ApplyFlipWalk(Vector2 from, Vector2 to)
    {
        Vector3 eul = actor.localEulerAngles;
        eul.y = 0f;
        actor.localEulerAngles = eul;

        Vector3 s = actor.localScale;
        s.x = Mathf.Abs(s.x);

        bool movingRight = (to.x - from.x) > 0f;
        if (movingRight) s.x = -Mathf.Abs(s.x);

        actor.localScale = s;
    }

    private void ResetVisual()
    {
        if (actor == null) return;

        Vector3 s = actor.localScale;
        s.x = Mathf.Abs(s.x);
        actor.localScale = s;

        Vector3 e = actor.localEulerAngles;
        e.y = 0f;
        actor.localEulerAngles = e;
    }

    private static float RandomRangeSafe(Vector2 range)
    {
        float a = Mathf.Max(0f, range.x);
        float b = Mathf.Max(a, range.y);
        return UnityEngine.Random.Range(a, b);
    }

    private IEnumerator WaitRealtimePausable(float seconds)
    {
        if (seconds <= 0f) yield break;

        float t = 0f;
        while (t < seconds)
        {
            if (freezeWhenPaused && _paused)
            {
                yield return null;
                continue;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}


