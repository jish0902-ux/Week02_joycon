using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[DisallowMultipleComponent]
public sealed class UISlideToggleOnFire : MonoBehaviour
{
    [Header("Input (New Input System)")]
    [SerializeField] private InputActionReference fireAction;

    [Header("Target UI")]
    [SerializeField] private RectTransform panel;       // 슬라이드할 패널
    [SerializeField] private CanvasGroup cg;            // Raycast/Interactable 제어용(선택)

    [Header("Slide Settings")]
    [SerializeField] private float duration = 0.35f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("보이는 상태의 X(anchoredPosition.x). 보통 0")]
    [SerializeField] private float shownX = 0f;

    [Tooltip("숨김 상태의 X(왼쪽 오프스크린). 비워두면 런타임에 자동 계산")]
    [SerializeField] private float hiddenX = float.NaN;

    [Header("Options")]
    [SerializeField] private bool startHidden = true;   // 시작 시 숨김
    [SerializeField] private bool replayIfRunning = true; // 재입력 시 재생 재시작

    Vector2 _shownPos, _hiddenPos;
    bool _isShown;
    Coroutine _slideCo;

    void Awake()
    {
        if (!panel) TryGetComponent(out panel);
        if (!cg) TryGetComponent(out cg);

        float y = panel != null ? panel.anchoredPosition.y : 0f;

        // hiddenX 자동 계산: 패널+부모 크기로 왼쪽 바깥 값 산출
        if (float.IsNaN(hiddenX) && panel != null)
        {
            var parent = panel.parent as RectTransform;
            float parentWidth = parent ? parent.rect.width : Screen.width;
            float panelWidth = panel.rect.width;
            hiddenX = -(parentWidth * 0.5f + panelWidth); // 넉넉히 왼쪽 바깥
        }

        _shownPos = new Vector2(shownX, y);
        _hiddenPos = new Vector2(hiddenX, y);
    }

    void OnEnable()
    {
        if (fireAction && fireAction.action != null)
        {
            fireAction.action.Enable();
            fireAction.action.performed += OnFire;
        }

        SetInstant(startHidden ? _hiddenPos : _shownPos, !startHidden);
    }

    void OnDisable()
    {
        if (fireAction && fireAction.action != null)
        {
            fireAction.action.performed -= OnFire;
            fireAction.action.Disable();
        }
        if (_slideCo != null) { StopCoroutine(_slideCo); _slideCo = null; }
    }

    void OnFire(InputAction.CallbackContext _)
    {
        Toggle();
    }

    public void Toggle()
    {
        if (!panel) return;

        Vector2 target = _isShown ? _hiddenPos : _shownPos;

        if (_slideCo != null)
        {
            if (!replayIfRunning) return;
            StopCoroutine(_slideCo);
        }
        _slideCo = StartCoroutine(SlideTo(target));
    }

    IEnumerator SlideTo(Vector2 target)
    {
        Vector2 from = panel.anchoredPosition;
        float t = 0f;
        float dur = Mathf.Max(0.0001f, duration);

        // 목표 상태에 따라 상호작용/레이캐스트 선반영도 가능(원하면 주석 해제)
        // if (cg) { cg.blocksRaycasts = true; cg.interactable = true; }

        while (t < 1f)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt / dur;
            float e = ease.Evaluate(t < 1f ? t : 1f);

            float x = Mathf.Lerp(from.x, target.x, e);
            panel.anchoredPosition = new Vector2(x, target.y);
            yield return null;
        }

        // 스냅 + 상태 반영
        panel.anchoredPosition = target;
        _isShown = Approximately(target, _shownPos);

        if (cg)
        {
            // 보일 때만 상호작용 허용
            cg.alpha = 1f; // 필요 시 페이드와 함께 쓰면 좋음
            cg.blocksRaycasts = _isShown;
            cg.interactable = _isShown;
        }

        _slideCo = null;
    }

    void SetInstant(Vector2 pos, bool shown)
    {
        if (!panel) return;
        if (_slideCo != null) { StopCoroutine(_slideCo); _slideCo = null; }
        panel.anchoredPosition = pos;
        _isShown = shown;

        if (cg)
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = shown;
            cg.interactable = shown;
        }
    }

    static bool Approximately(Vector2 a, Vector2 b)
        => Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
}
