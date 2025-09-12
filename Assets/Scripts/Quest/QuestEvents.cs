// Assets/Scripts/Quests/QuestEvents.cs
using System;
using UnityEngine;

public static class QuestEvents
{
    // ---- 상호작용 본 이벤트 ----
    public struct InteractMsg
    {
        public string id;          // Interactable Id
        public int idHash;         // Animator.StringToHash(id)
        public Vector3 pos;        // 상호작용 위치
        public InteractionKind kind; // Press / Hold / UseItem
    }

    public static event Action<InteractMsg> OnInteract;

    public static void RaiseInteract(string id, Vector3 pos, InteractionKind kind)
        => OnInteract?.Invoke(new InteractMsg
        {
            id = id,
            idHash = Animator.StringToHash(id ?? string.Empty),
            pos = pos,
            kind = kind
        });

    // ---- Hold UI/상태 이벤트 ----
    public static event Action<string, float> OnHoldStarted;    // (id, requiredSeconds)
    public static event Action<string, float> OnHoldProgress;   // (id, elapsedSeconds)
    public static event Action<string> OnHoldCanceled;    // (id)

    public static void RaiseHoldStarted(string id, float requiredSeconds)
        => OnHoldStarted?.Invoke(id, requiredSeconds);

    public static void RaiseHoldProgress(string id, float elapsedSeconds)
        => OnHoldProgress?.Invoke(id, elapsedSeconds);

    public static void RaiseHoldCanceled(string id)
        => OnHoldCanceled?.Invoke(id);

    // ---- 플래그(TriggerFlags) ----
    public static event Action<string> OnFlagRaised;            // (flagId)
    public static void RaiseFlag(string flagId)
        => OnFlagRaised?.Invoke(flagId);

    // ---- StayInArea ----
    public static event Action<string, float, Vector3> OnAreaStayTick; // (areaId, deltaSeconds, pos)
    public static void RaiseAreaStayTick(string areaId, float deltaSeconds, Vector3 pos)
        => OnAreaStayTick?.Invoke(areaId, deltaSeconds, pos);

    // ---- Delivery ----
    public static event Action<string, string, Vector3> OnDelivery;    // (itemId, receiverId, pos)
    public static void RaiseDelivery(string itemId, string receiverId, Vector3 pos)
        => OnDelivery?.Invoke(itemId, receiverId, pos);
}
