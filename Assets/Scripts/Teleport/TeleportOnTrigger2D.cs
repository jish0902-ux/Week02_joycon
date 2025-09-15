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
    [SerializeField] private string requiredTag = "Player"; // 비우면 전체 허용
    [SerializeField] private LayerMask includeLayers = ~0;  // 포함 레이어

    [Header("Behavior")]
    [SerializeField] private bool alignRotation = false;    // 회전도 이동
    [SerializeField] private bool preserveVelocity = true;  // 속도 유지
    [SerializeField, Min(0f)] private float perObjectCooldown = 0.1f; // 왕복 방지

    [Header("Input (New Input System)")]
    [Tooltip("여기에 액션(예: Interact/Teleport)을 참조로 연결하면, performed 시 텔레포트합니다.")]
    [SerializeField] private InputActionReference teleportAction;

    [Tooltip("true면 트리거 안에 들어온 대상에게만 입력이 유효합니다.")]
    [SerializeField] private bool requireOverlapForInput = true;

    [Header("Gizmo (Red Line)")]
    [SerializeField] private bool showGizmo = true;         // 표시 토글
    [SerializeField] private bool onlyWhenSelected = true;  // 선택 시에만
    [SerializeField] private Color lineColor = new Color(1f, 0f, 0f, 0.9f); // 빨간색

    private Collider2D _col;

    // 현재 트리거 내부 후보들
    private readonly HashSet<Collider2D> _inside = new HashSet<Collider2D>();
    private Collider2D _lastEntered; // 우선 대상

    void Reset()
    {
        _col = GetComponent<Collider2D>();
        if (_col) _col.isTrigger = true; // 트리거 권장
    }

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col && !_col.isTrigger) _col.isTrigger = true;
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

    // ───────────────────────────────── 이벤트/액션 진입/이탈 ─────────────────────────────────
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsEligible(other)) return;
        _inside.Add(other);
        _lastEntered = other;
        // 즉시 텔레포트하지 않고, 입력을 대기합니다.
    }

    void OnTriggerExit2D(Collider2D other)
    {
        _inside.Remove(other);
        if (_lastEntered == other) _lastEntered = null;
    }

    // PlayerInput(Unity Events)에서 직접 연결 가능
    public void OnTeleportAction(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        TryTeleportByInput();
    }

    // InputActionReference(performed) 구독
    private void OnTeleportPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        TryTeleportByInput();
    }

    // ───────────────────────────────── 핵심 로직 ─────────────────────────────────
    private void TryTeleportByInput()
    {
        if (!target) return;

        // 입력이 트리거 내부 대상에게만 유효해야 한다면 후보 풀에서 선택
        if (requireOverlapForInput)
        {
            var candidate = PickCandidate();
            if (candidate == null) return;
            if (IsOnCooldown(candidate.gameObject)) return;

            Teleport(candidate);
            StampCooldown(candidate.gameObject);
            return;
        }

        // 트리거 요구 없으면: 우선 트리거 내부 우선 대상 → 없으면 requiredTag/layer를 만족하는 씬 내 첫 Rigidbody2D
        Collider2D chosen = _lastEntered ?? PickAnyFromScene();
        if (chosen == null) return;
        if (IsOnCooldown(chosen.gameObject)) return;

        Teleport(chosen);
        StampCooldown(chosen.gameObject);
    }

    private Collider2D PickCandidate()
    {
        // 가장 최근 진입 우선, 없으면 집합에서 첫 항목
        if (_lastEntered && _inside.Contains(_lastEntered))
            return _lastEntered;

        foreach (var c in _inside)
            if (c) return c;

        return null;
    }

    private Collider2D PickAnyFromScene()
    {
        // requiredTag / includeLayers를 만족하는 첫 번째 Rigidbody2D 보유 콜라이더 탐색
        var bodies = Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
        foreach (var rb in bodies)
        {
            var go = rb.gameObject;
            if (((1 << go.layer) & includeLayers.value) == 0) continue;
            if (!string.IsNullOrEmpty(requiredTag) && !go.CompareTag(requiredTag)) continue;
            if (!rb.TryGetComponent<Collider2D>(out var col)) continue;
            return col;
        }
        return null;
    }

    // --- 텔레포트 쿨다운 스탬프 ---
    sealed class TeleportStamp : MonoBehaviour
    {
        public float ignoreUntil;
    }

    bool IsOnCooldown(GameObject go)
    {
        if (perObjectCooldown <= 0f) return false;
        if (go.TryGetComponent<TeleportStamp>(out var s))
            return s.ignoreUntil > Time.unscaledTime;
        return false;
    }

    void StampCooldown(GameObject go)
    {
        if (perObjectCooldown <= 0f) return;
        if (!go.TryGetComponent<TeleportStamp>(out var s))
            s = go.AddComponent<TeleportStamp>();
        s.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        s.ignoreUntil = Time.unscaledTime + perObjectCooldown;
    }

    bool IsEligible(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & includeLayers.value) == 0) return false;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return false;
        return true;
    }

    void Teleport(Collider2D other)
    {
        var dstPos = target.position;

        if (other.attachedRigidbody && other.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.position = dstPos;
            if (!preserveVelocity) rb.linearVelocity = Vector2.zero; // Unity 6 물리 이름 사용
            if (alignRotation) rb.rotation = target.eulerAngles.z;
        }
        else
        {
            other.transform.position = dstPos;
            if (alignRotation) other.transform.rotation = target.rotation;
        }
    }

    // 게임뷰에서 오브젝트 클릭 시 표시 토글(게임뷰의 Gizmos 버튼이 켜져 있어야 보임)
    void OnMouseDown() => showGizmo = !showGizmo;

    void OnDrawGizmos() => DrawGizmoInternal(selected: false);
    void OnDrawGizmosSelected() => DrawGizmoInternal(selected: true);

    void DrawGizmoInternal(bool selected)
    {
        if (!target || !showGizmo) return;
        if (onlyWhenSelected && !selected) return;

        Gizmos.color = lineColor;
        Gizmos.DrawLine(transform.position, target.position); // 목적지까지 빨간 선
    }
}
