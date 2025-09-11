using UnityEngine;
using UnityEngine.InputSystem; // �� Input System�� ���ٸ�

public sealed class InteractionScanner2D : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] LayerMask interactableMask;
    [SerializeField] float radius = 1.8f;
    [SerializeField] KeyCode legacyKey = KeyCode.E; // ���Ž� �Է¿�

    static readonly Collider2D[] s_Hits = new Collider2D[8]; 
    ContactFilter2D _filter;

    void Awake()
    {
        _filter = new ContactFilter2D();
        _filter.SetLayerMask(interactableMask); // ���̾� ���� ���� + useLayerMask=true
        _filter.useTriggers = true;             // Ʈ���ŵ� �����Ϸ��� �� ���ּ���
    }

    void Update()
    {
        if (!player) return;

        // --- �Է� ---
/*#if ENABLE_INPUT_SYSTEM
        if (!Keyboard.current?.eKey.wasPressedThisFrame ?? true) return;  // �� Input System ����
#else*/
        if (!Input.GetKeyDown(legacyKey)) return;                          // ���Ž� �Է�(����� ������ �����)
//#endif

        // --- Ž�� (���Ҵ�) ---
        var pos = (Vector2)player.position;
        int count = Physics2D.OverlapCircle(pos, radius, _filter, s_Hits); // �迭�� "ä�� ����"
        if (count == 0) return;

        // ���� ����� ��� ����
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