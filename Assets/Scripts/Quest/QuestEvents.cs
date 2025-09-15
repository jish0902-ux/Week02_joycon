// Assets/Scripts/Quests/QuestEvents.cs
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class QuestEvents
{
    // =========================
    // 메시지/이벤트 정의 (기존 유지)
    // =========================

    // ---- 상호작용 본 이벤트 ----
    public struct InteractMsg
    {
        public string id;             // Interactable Id
        public int idHash;            // Animator.StringToHash(id)
        public Vector3 pos;           // 상호작용 위치
        public InteractionKind kind;  // Press / Hold / UseItem
    }

    public static event Action<InteractMsg> OnInteract;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RaiseInteract(string id, Vector3 pos, InteractionKind kind)
    {
        var msg = new InteractMsg
        {
            id = id,
            idHash = Animator.StringToHash(id ?? string.Empty),
            pos = pos,
            kind = kind
        };

        OnInteract?.Invoke(msg);

        // 캐시: 마지막 상호작용 저장(취소용)
        _lastInteractByHash[msg.idHash] = msg;
    }

    // ---- Hold UI/상태 이벤트 ----
    public static event Action<string, float> OnHoldStarted;    // (id, requiredSeconds)
    public static event Action<string, float> OnHoldProgress;   // (id, elapsedSeconds)
    public static event Action<string> OnHoldCanceled;          // (id)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RaiseHoldStarted(string id, float requiredSeconds)
        => OnHoldStarted?.Invoke(id, requiredSeconds);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RaiseHoldProgress(string id, float elapsedSeconds)
        => OnHoldProgress?.Invoke(id, elapsedSeconds);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RaiseHoldCanceled(string id)
        => OnHoldCanceled?.Invoke(id);

    // ---- 플래그(TriggerFlags) ----
    public static event Action<string> OnFlagRaised;   // (flagId)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RaiseFlag(string flagId)
    {
        OnFlagRaised?.Invoke(flagId);
        _raisedFlags.Add(flagId); // 캐시(취소용)
    }

    // ---- StayInArea ----
    public static event Action<string, float, Vector3> OnAreaStayTick; // (areaId, deltaSeconds, pos)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RaiseAreaStayTick(string areaId, float deltaSeconds, Vector3 pos)
    {
        OnAreaStayTick?.Invoke(areaId, deltaSeconds, pos);
        _activeAreas[areaId] = pos; // 마지막 위치만 저장(취소/종료용)
    }

    // ---- Delivery ----
    public static event Action<string, string, Vector3> OnDelivery;    // (itemId, receiverId, pos)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RaiseDelivery(string itemId, string receiverId, Vector3 pos)
    {
        OnDelivery?.Invoke(itemId, receiverId, pos);
        _lastDeliveryByReceiver[receiverId] = (itemId, pos); // 캐시(취소용)
    }

    // ======================================================
    // "취소"를 위한 추가 이벤트 & API (최소 침습, 선택적 구독)
    // ======================================================

    public static event Action<InteractMsg> OnInteractCanceled;

    /// <summary>
    /// 주어진 ID로 상호작용 취소 신호를 브로드캐스트합니다.
    /// 매니저는 이 신호를 받아 "현재 상태를 스캔"하여 매칭되는 완료 목표를 롤백합니다.
    /// </summary>
    public static bool CancelLastInteract(string id)
    {
        if (string.IsNullOrEmpty(id)) return false; // 빈 ID 방지
        var msg = new InteractMsg
        {
            id = id,
            idHash = UnityEngine.Animator.StringToHash(id),
            pos = default,
            kind = InteractionKind.None
        };
        OnInteractCanceled?.Invoke(msg); // 캐시 검사 없이 즉시 브로드캐스트
        return true;
    }



    // Hold 취소(이미 OnHoldCanceled가 있어 래퍼만 제공)
    /// <summary>진행 중인 Hold를 취소(구독자는 이 신호로 UI/상태 롤백).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CancelHold(string id)
    {
        OnHoldCanceled?.Invoke(id);
        return true; // 별도 상태 트래킹 없이 신호용
    }

    // 플래그 취소(해제)
    public static event Action<string> OnFlagCleared; // (flagId)

    /// <summary>이전에 Raise된 Flag를 해제.</summary>
    public static bool CancelFlag(string flagId)
    {
        if (_raisedFlags.Remove(flagId))
        {
            OnFlagCleared?.Invoke(flagId);
            return true;
        }
        return false;
    }

    // AreaStay 취소/종료
    public static event Action<string> OnAreaStayCanceled; // (areaId)

    /// <summary>마지막으로 관측된 AreaStay를 종료 신호로 취소.</summary>
    public static bool CancelAreaStay(string areaId)
    {
        if (_activeAreas.Remove(areaId))
        {
            OnAreaStayCanceled?.Invoke(areaId);
            return true;
        }
        return false;
    }

    // 배송 취소
    public static event Action<string, string, Vector3> OnDeliveryCanceled; // (itemId, receiverId, pos)

    /// <summary>receiver 기준 마지막 Delivery를 취소.</summary>
    public static bool CancelLastDeliveryTo(string receiverId)
    {
        if (_lastDeliveryByReceiver.TryGetValue(receiverId, out var data))
        {
            OnDeliveryCanceled?.Invoke(data.itemId, receiverId, data.pos);
            _lastDeliveryByReceiver.Remove(receiverId);
            return true;
        }
        return false;
    }

    // 유틸: 캐시 초기화
    public static void ClearAllCaches()
    {
        _lastInteractByHash.Clear();
        _raisedFlags.Clear();
        _activeAreas.Clear();
        _lastDeliveryByReceiver.Clear();
    }

    // =========================
    // 내부 캐시(메인스레드 전제)
    // =========================
    static readonly Dictionary<int, InteractMsg> _lastInteractByHash = new(128);
    static readonly HashSet<string> _raisedFlags = new();
    static readonly Dictionary<string, Vector3> _activeAreas = new();
    static readonly Dictionary<string, (string itemId, Vector3 pos)> _lastDeliveryByReceiver = new();
}
