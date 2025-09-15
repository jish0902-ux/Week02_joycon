using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TeleportOnTrigger2D : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private Transform target;

    [Header("Filter")]
    [SerializeField] private string requiredTag = "Player";     // 비우면 전체 허용
    [SerializeField] private LayerMask includeLayers = ~0;      // 포함 레이어

    [Header("Behavior")]
    [SerializeField] private bool alignRotation = false;        // 회전 동기화
    [SerializeField] private bool preserveVelocity = true;      // 속도 유지
    [SerializeField, Min(0f)] private float perObjectCooldown = 0.15f; // 왕복 방지

    [Header("Input (New Input System)")]
    [Tooltip("여기에 액션(예: Interact/Teleport)을 참조로 연결하면, performed 시 텔레포트합니다.")]
    [SerializeField] private InputActionReference teleportAction;

    [Tooltip("true면 트리거 안에 들어온 대상에게만 입력이 유효합니다.")]
    [SerializeField] private bool requireOverlapForInput = true;

    [Header("Gizmo (Red Line)")]
    [SerializeField] private bool showGizmo = true;             // 표시 토글
    [SerializeField] private bool onlyWhenSelected = true;      // 선택 시에만
    [SerializeField] private Color lineColor = new Color(1f, 0f, 0f, 0.9f); // 빨간색

    private Collider2D _col;
    private readonly HashSet<Collider2D> _inside = new HashSet<Collider2D>();
    private Collider2D _lastEntered;
    private int _includeMask;

    // 🔒 전역 프레임 가드(모든 텔레포터 공통): 한 프레임 1회만 처리
    private static int s_lastInputFrame = -1;

    // 대상 오브젝트에 부착되는 쿨다운 스탬프(모든 텔레포터가 공유)
    sealed class TeleportStamp : MonoBehaviour { public float ignoreUntil; }

    void Reset()
    {
        _col = GetComponent<Collider2D>();
        if (_col) _col.isTrigger = true;
    }

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col && !_col.isTrigger) _col.isTrigger = true;
        _includeMask = includeLayers.value;
    }

    void OnEnable()
    {
        if (teleportAction != null)
        {
            teleportAction.action.performed += OnTeleportPerformed;
            if (!teleportAction.action.enabled) teleportAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (teleportAction != null)
            teleportAction.action.performed -= OnTeleportPerformed;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsEligible(other)) return;
        _inside.Add(other);
        _lastEntered = other;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!IsEligible(other)) return;
        _inside.Add(other);
        _lastEntered = other;
    }


    void OnTriggerExit2D(Collider2D other)
    {
        _inside.Remove(other);
        if (_lastEntered == other) _lastEntered = null;
    }

    // PlayerInput(Unity Events) 연결용(Press Only 권장)
    public void OnTeleportAction(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        TryTeleportByInput();
    }

    // InputActionReference 구독 콜백
    private void OnTeleportPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        TryTeleportByInput();
    }

    // ───────────────────────────────── 핵심 로직 ─────────────────────────────────
    private void TryTeleportByInput()
    {
        // 전역 프레임 가드
        if (Time.frameCount == s_lastInputFrame) return;
        s_lastInputFrame = Time.frameCount;

        if (!target) return;

        Collider2D chosen = null;

        if (requireOverlapForInput)
        {
            chosen = PickCandidate();
            if (!chosen) return;
        }
        else
        {
            chosen = _lastEntered ?? PickAnyFromScene();
            if (!chosen) return;
        }

        var key = CooldownKey(chosen);
        if (IsOnCooldown(key)) return;

        Teleport(chosen);            
        StampCooldown(key);
    }

    private Collider2D PickCandidate()
    {
        if (_lastEntered && _inside.Contains(_lastEntered))
            return _lastEntered;

        foreach (var c in _inside)
            if (c) return c;
        return null;
    }

    private Collider2D PickAnyFromScene()
    {
        var cols = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        for (int i = 0; i < cols.Length; ++i)
        {
            var c = cols[i];
            if (!c) continue;

            var go = c.attachedRigidbody ? c.attachedRigidbody.gameObject : c.gameObject;
            if (((1 << go.layer) & _includeMask) == 0) continue;
            if (!string.IsNullOrEmpty(requiredTag) && !go.CompareTag(requiredTag)) continue;

            return c;
        }
        return null;
    }

    private static GameObject CooldownKey(Collider2D c)
    {
        var rb = c.attachedRigidbody;
        return rb ? rb.gameObject : c.gameObject;
    }

    private bool IsOnCooldown(GameObject go)
    {
        if (perObjectCooldown <= 0f) return false;
        return go.TryGetComponent<TeleportStamp>(out var s) && s.ignoreUntil > Time.unscaledTime;
    }

    private void StampCooldown(GameObject go)
    {
        if (perObjectCooldown <= 0f) return;
        if (!go.TryGetComponent<TeleportStamp>(out var s))
            s = go.gameObject.AddComponent<TeleportStamp>();
        s.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        s.ignoreUntil = Time.unscaledTime + perObjectCooldown;
    }

    private bool IsEligible(Collider2D other)
    {
        var go = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
        if (((1 << go.layer) & _includeMask) == 0) return false;
        if (!string.IsNullOrEmpty(requiredTag) && !go.CompareTag(requiredTag)) return false;
        return true;
    }

    // ⬇️⬇️ Transform 강제 텔레포트(요청사항 반영)
    private void Teleport(Collider2D other)
    {
        // 이동시킬 루트 트랜스폼(리지드바디가 있으면 그 쪽으로)
        Transform root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;

        // 속도 유지 옵션이 꺼져 있으면, 텔레포트 직후 튐 방지를 위해 속도 0
        var rb = other.attachedRigidbody;
        if (rb && !preserveVelocity)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // 위치/회전 강제 스냅(Transform)
        if (alignRotation)
            root.SetPositionAndRotation(target.position, target.rotation);
        else
            root.position = target.position;

        // 물리/트리거 갱신 즉시 반영
        if (rb) rb.WakeUp();
        Physics2D.SyncTransforms();
    }

    void OnMouseDown() => showGizmo = !showGizmo;
    void OnDrawGizmos() => DrawGizmoInternal(false);
    void OnDrawGizmosSelected() => DrawGizmoInternal(true);

    private void DrawGizmoInternal(bool selected)
    {
        if (!target || !showGizmo) return;
        if (onlyWhenSelected && !selected) return;

        Gizmos.color = lineColor;
        Gizmos.DrawLine(transform.position, target.position);
    }
}
