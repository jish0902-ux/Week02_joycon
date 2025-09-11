using UnityEngine;
using System;

public static class QuestEvents
{
    public struct InteractMsg
    {
        public string id;      // 예: "Box_A"
        public int idHash;     // Animator.StringToHash(id) 캐시값(비교 최적화)
        public Vector3 worldPos;
    }

    public static event Action<InteractMsg> OnInteract;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void RaiseInteract(string id, Vector3 pos)
    {
        var msg = new InteractMsg
        {
            id = id,
            idHash = Animator.StringToHash(id),
            worldPos = pos
        };
        var h = OnInteract;
        if (h != null) h(msg);
    }
}
