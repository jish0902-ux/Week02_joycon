using UnityEngine;
using UnityEngine.InputSystem; // 새 Input System을 쓴다면

public sealed class InteractionScanner2D : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] LayerMask interactableMask;
    [SerializeField] float radius = 1.8f;
    [SerializeField] KeyCode legacyKey = KeyCode.E; // 레거시 입력용

    static readonly Collider2D[] s_Hits = new Collider2D[8]; 
    ContactFilter2D _filter;

    void Awake()
    {
        _filter = new ContactFilter2D();
        _filter.SetLayerMask(interactableMask); // 레이어 필터 적용 + useLayerMask=true
        _filter.useTriggers = true;             // 트리거도 감지하려면 꼭 켜주세요
    }

    void Update()
    {
        if (!player) return;

        // --- 입력 ---
/*#if ENABLE_INPUT_SYSTEM
        if (!Keyboard.current?.eKey.wasPressedThisFrame ?? true) return;  // 새 Input System 폴링
#else*/
        if (!Input.GetKeyDown(legacyKey)) return;                          // 레거시 입력(허용은 되지만 비권장)
//#endif

        // --- 탐색 (비할당) ---
        var pos = (Vector2)player.position;
        int count = Physics2D.OverlapCircle(pos, radius, _filter, s_Hits); // 배열에 "채워 넣음"
        if (count == 0) return;

        // 가장 가까운 대상 고르기
        float best = float.MaxValue;
        Collider2D bestCol = null;
        for (int i = 0; i < count; ++i)
        {
            var c = s_Hits[i];
            if (!c) continue;
            float d2 = ((Vector2)c.transform.position - pos).sqrMagnitude;
            if (d2 < best) { best = d2; bestCol = c; }
            s_Hits[i] = null; 
        }

        if (bestCol && bestCol.TryGetComponent(out Interactable2D it))
        {
            QuestEvents.RaiseInteract(it.Id, it.transform.position);
        }
    }
}