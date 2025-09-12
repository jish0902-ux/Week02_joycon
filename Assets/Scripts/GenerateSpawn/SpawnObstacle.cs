// Assets/Scripts/Spawn/SpawnObstacle.cs
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SpawnObstacle : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference fireAction;

    [Header("Probe(BoxCast)")]
    [SerializeField, Min(0f)] private float rightOffset = 1.0f;
    [SerializeField, Range(0.05f, 1.0f)] private float probeRadius = 0.25f;
    [SerializeField, Min(0f)] private float castDistance = 0f;      // 0이면 Overlap처럼
    [SerializeField] private bool drawGizmos = true;

    [Header("List to Spawn From")]
    [SerializeField] private List<SpawnableSO> spawnables;
    [SerializeField] private Transform spawnParent;  // 개별 부모(없으면 OM의 defaultParent)

    static readonly List<RaycastHit2D> s_Hits = new(8);
    ContactFilter2D _filter;

    void Awake()
    {
        // 필터: 트리거 포함, 모든 레이어(필요하면 레이어마스크 적용)
        _filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };

        // 미리 풀 보장(런타임 첫 스폰 스파이크 줄임)
        if (ObjectManager.Instance && spawnables != null)
            foreach (var so in spawnables) ObjectManager.Instance.EnsurePool(so);
    }

    void OnEnable()
    {
        if (fireAction && fireAction.action != null)
        {
            fireAction.action.performed += OnFire;
            if (!fireAction.action.enabled) fireAction.action.Enable();
        }
    }
    void OnDisable()
    {
        if (fireAction && fireAction.action != null)
            fireAction.action.performed -= OnFire;
    }

    void OnFire(InputAction.CallbackContext _)
    {
        if (!ObjectManager.Instance || spawnables == null || spawnables.Count == 0) return;

        // 캐스트 파라미터
        Vector2 origin = (Vector2)transform.position + (Vector2)transform.right * rightOffset;
        Vector2 size = Vector2.one * (probeRadius * 2f);
        float angle = transform.eulerAngles.z;
        Vector2 dir = (Vector2)transform.right;

        s_Hits.Clear();
        int count = Physics2D.BoxCast(origin, size, angle, dir, _filter, s_Hits, castDistance);

        // 스폰 위치/회전 계산
        Vector2 pos; Vector2 normal;
        if (count > 0 && s_Hits[0].collider)
        {
            var hit = s_Hits[0];
            pos = hit.point + hit.normal * 0.001f;
            normal = hit.normal;
        }
        else
        {
            pos = origin;
            normal = dir; // 맞은 게 없으면 전방
        }

        // SO 선택(가중치)
        int idx = PickWeightedIndex(spawnables);
        var so = spawnables[idx];

        // 회전: 법선 정렬 or 유지
        Quaternion rot;
        if (so.alignToHitNormal && normal.sqrMagnitude > 1e-6f)
            rot = Quaternion.FromToRotation(Vector3.up, new Vector3(normal.x, normal.y, 0f));
        else
            rot = transform.rotation;

        // 오프셋 적용
        Vector3 offset = rot * (Vector3)so.localOffset + (Vector3)normal * so.surfaceOffset;
        var go = ObjectManager.Instance.Spawn(so, pos + (Vector2)offset, rot, spawnParent);

        // (선택) 자동 반납 예시
        // ObjectManager.Instance.DespawnAfter(go, 5f);
    }

    static int PickWeightedIndex(IList<SpawnableSO> arr)
    {
        float sum = 0f;
        for (int i = 0; i < arr.Count; ++i) sum += Mathf.Max(0f, arr[i] ? arr[i].weight : 0f);
        if (sum <= 0f) return 0;
        float r = Random.value * sum, run = 0f;
        for (int i = 0; i < arr.Count; ++i)
        {
            float w = Mathf.Max(0f, arr[i] ? arr[i].weight : 0f);
            run += w;
            if (r <= run) return i;
        }
        return arr.Count - 1;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Vector3 p = transform.position + transform.right * rightOffset;
        Vector2 size = Vector2.one * (probeRadius * 2f);
        var old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(p, Quaternion.Euler(0, 0, transform.eulerAngles.z), Vector3.one);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0.01f));
        Gizmos.matrix = old;
    }
}
