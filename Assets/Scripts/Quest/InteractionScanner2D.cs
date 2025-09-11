using UnityEngine;

public sealed class InteractionScanner2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private float radius = 1.8f;
    [SerializeField] private KeyCode key = KeyCode.E;

    static readonly Collider2D[] _hits = new Collider2D[8];

    void Update()
    {
        if (!Input.GetKeyDown(key) || !player) return;

        int n = Physics2D.OverlapCircleNonAlloc(player.position, radius, _hits, interactableMask);
        if (n <= 0) return;

        // 가장 가까운 Interactable 선택
        float best = float.MaxValue;
        Interactable2D pick = null;
        var p = (Vector2)player.position;

        for (int i = 0; i < n; ++i)
        {
            var col = _hits[i];
            if (!col) continue;
            if (!col.TryGetComponent(out Interactable2D it)) continue;

            float d2 = (p - (Vector2)col.transform.position).sqrMagnitude;
            if (d2 < best) { best = d2; pick = it; }
        }

        if (pick != null)
            QuestEvents.RaiseInteract(pick.Id, pick.transform.position);
    }

    // 디버그 시각화(선택)
    void OnDrawGizmosSelected()
    {
        if (player)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.position, radius);
        }
    }
}
