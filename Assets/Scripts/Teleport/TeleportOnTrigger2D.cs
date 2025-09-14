using UnityEngine;

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

    [Header("Gizmo (Red Line)")]
    [SerializeField] private bool showGizmo = true;         // 표시 토글
    [SerializeField] private bool onlyWhenSelected = true;  // 선택 시에만
    [SerializeField] private Color lineColor = new Color(1f, 0f, 0f, 0.9f); // 빨간색

    private Collider2D _col;

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

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!target) return;
        if (((1 << other.gameObject.layer) & includeLayers.value) == 0) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        if (IsOnCooldown(other.gameObject)) return;

        Teleport(other);
        StampCooldown(other.gameObject);
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

    void Teleport(Collider2D other)
    {
        var dstPos = target.position;

        if (other.attachedRigidbody && other.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.position = dstPos;
            if (!preserveVelocity) rb.linearVelocity = Vector2.zero;
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
