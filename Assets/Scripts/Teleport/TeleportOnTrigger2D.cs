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
    [SerializeField, Min(0f)] private float perObjectCooldown = 0.15f; // 왕복 방지(인스턴스 기준)

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

    // ⬇️ 전역 대신, 인스턴스 단위 프레임 가드
    private int _lastInputFrame = -1;

    // ⬇️ 전역 컴포넌트(TeleportStamp) 대신, 인스턴스 딕셔너리로 쿨다운 관리
    // key: 텔레포트 대상 GameObject.GetInstanceID()
    private readonly Dictionary<int, float> _cooldownUntil = new Dictionary<int, float>(64);

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

        // 인스턴스 상태 정리(선택)
        _inside.Clear();
        _cooldownUntil.Clear();
        _lastEntered = null;
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
        // 인스턴스 프레임 가드
        if (Time.frameCount == _lastInputFrame) return;
        _lastInputFrame = Time.frameCount;

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

        var go = CooldownKey(chosen);
        if (IsOnCooldown(go)) return;

        Teleport(chosen);
        StampCooldown(go);
    }

    private Collider2D PickCandidate()
    {
        if (_lastEntered && _inside.Contains(_lastEntered))
            return _lastEntered;

        // 첫 번째 유효 콜라이더 하나 고름
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
        int id = go.GetInstanceID();
        return _cooldownUntil.TryGetValue(id, out var until) && until > Time.unscaledTime;
    }

    private void StampCooldown(GameObject go)
    {
        if (perObjectCooldown <= 0f) return;
        _cooldownUntil[go.GetInstanceID()] = Time.unscaledTime + perObjectCooldown;
    }

    private bool IsEligible(Collider2D other)
    {
        var go = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
        if (((1 << go.layer) & _includeMask) == 0) return false;
        if (!string.IsNullOrEmpty(requiredTag) && !go.CompareTag(requiredTag)) return false;
        return true;
    }

    private void Teleport(Collider2D other)
    {
        // 이동시킬 루트 트랜스폼(리지드바디가 있으면 그 쪽으로)
        Transform root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;

        // 속도 유지 옵션이 꺼져 있으면, 텔레포트 직후 튐 방지를 위해 속도 0
        var rb = other.attachedRigidbody;
        if (rb && !preserveVelocity)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector2.zero;
#else
            rb.velocity = Vector2.zero;
#endif
            rb.angularVelocity = 0f;
        }

        // 위치/회전 강제 스냅(Transform)
        if (alignRotation)
            root.SetPositionAndRotation(target.position, target.rotation);
        else
            root.position = new Vector3(target.position.x, target.position.y, root.position.z);

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
