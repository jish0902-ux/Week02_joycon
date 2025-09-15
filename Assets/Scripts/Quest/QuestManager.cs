using Game.Quests;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static QuestManager;

/// <summary>
/// 최적화 포인트:
/// - 해시(Animator.StringToHash) 기반 비교
/// - List/배열 재사용, foreach 미사용
/// - 이벤트 단위 처리(StayInArea/Delivery/Flag)
/// </summary>
[DisallowMultipleComponent]
public sealed class QuestManager : MonoBehaviour
{
    [SerializeField] private QuestSO[] questDB; // 등록된 퀘스트 목록(필요한 것만)

    // --- 런타임 상태 ---
    [Serializable]
    public struct SubTaskState
    {
        public int targetHash;       // 최적화용
        public string targetId;      // 디버그/UI
        public bool done;            // 서브 완료 여부
        public float staySeconds;    // StayInArea 누적 시간(해당 타깃)
    }

    [Serializable]
    public class ObjectiveState
    {
        public ObjectiveDef def;
        public SubTaskState[] subs;  // 타깃 기반 목표(Interact/Sequence/Stay 등)
        public bool completed;

        // 추가 진행도
        public int progressCount;    // Delivery/반복형에서 사용
        public int seqIndex;         // InteractSequence 진행 커서
    }

    [Serializable]
    public class QuestState
    {
        public QuestSO so;
        public bool started;
        public bool completed;
        public bool completionEventRaised;
        public ObjectiveState[] objectives;
    }

    // 퀘스트ID -> 상태
    readonly Dictionary<uint, QuestState> _states = new(16);

    // UI 갱신 등 알림
    public event Action<uint> OnQuestUpdated; // 파라미터: questId

    void OnEnable()
    {
        QuestEvents.OnInteract += OnInteract;
        QuestEvents.OnInteractCanceled += OnInteractCanceled;
        QuestEvents.OnAreaStayTick += OnAreaStayTick;
        QuestEvents.OnDelivery += OnDelivery;
        QuestEvents.OnFlagRaised += OnFlagRaised;
    }


    void OnDisable()
    {
        QuestEvents.OnInteract -= OnInteract;
        QuestEvents.OnInteractCanceled -= OnInteractCanceled;
        QuestEvents.OnAreaStayTick -= OnAreaStayTick;
        QuestEvents.OnDelivery -= OnDelivery;
        QuestEvents.OnFlagRaised -= OnFlagRaised;
    }

    void Awake()
    {
        // 데모: 원하는 퀘스트 시작
        /*     StartQuest(1001);
             StartQuest(1002);
             StartQuest(1003);
             StartQuest(1004);
             StartQuest(1005);
        StartQuest(1006);*/
        StartQuest(1000);
        StartQuest(2000);
        StartQuest(3000);
        StartQuest(4000);
        StartQuest(9000); //테스트

    }

    // --- Public API ---
    public bool StartQuest(uint questId)
    {
        if (TryGetState(questId, out var qs))
        {
            if (qs.started) return false; // 이미 시작
            qs.started = true;
            EvaluateImmediateObjectives(qs); // 플래그형 등 즉시 판정
            OnQuestUpdated?.Invoke(questId);
            return true;
        }

        var so = FindQuestSO(questId);
        if (!so) return false;

        var newState = BuildState(so);
        newState.started = true;
        EvaluateImmediateObjectives(newState);
        _states[questId] = newState;
        OnQuestUpdated?.Invoke(questId);


        return true;
    }

    public bool TryGetSnapshot(uint questId, out QuestState qs)
        => _states.TryGetValue(questId, out qs);

    // ----------------- 내부 구현 -----------------
    QuestSO FindQuestSO(uint id)
    {
        for (int i = 0; i < questDB.Length; ++i)
            if (questDB[i] && questDB[i].id == id) return questDB[i];
        return null;
    }

    QuestState BuildState(QuestSO so)
    {
        var qs = new QuestState { so = so, started = false, completed = false };
        var objs = so.objectives;
        qs.objectives = new ObjectiveState[objs.Length];

        for (int i = 0; i < objs.Length; ++i)
        {
            ref var def = ref objs[i];
            var os = new ObjectiveState { def = def, completed = false, progressCount = 0, seqIndex = 0 };

            if (def.targetIds != null && def.targetIds.Length > 0)
            {
                // 모든 타깃형 목표에서 공통으로 사용(Interact/Sequence/Stay/Delivery 수신자 등)
                int n = def.targetIds.Length;
                os.subs = new SubTaskState[n];
                for (int s = 0; s < n; ++s)
                {
                    var id = def.targetIds[s] ?? string.Empty;
                    os.subs[s] = new SubTaskState
                    {
                        targetId = id,
                        targetHash = Animator.StringToHash(id),
                        done = false,
                        staySeconds = 0f
                    };
                }
            }
            else os.subs = Array.Empty<SubTaskState>();

            qs.objectives[i] = os;
        }
        return qs;
    }

    bool TryGetState(uint questId, out QuestState qs)
        => _states.TryGetValue(questId, out qs);

    // --- 이벤트 핸들러 ---
    void OnInteract(QuestEvents.InteractMsg msg)
    {

        Debug.Log("Final Events Raise" +  msg.id );
        // 활성 퀘스트 목록 스냅샷
        _keysScratch.Clear();
        foreach (var id in _states.Keys) _keysScratch.Add(id);

        _changedIds.Clear();

        for (int k = 0; k < _keysScratch.Count; ++k)
        {
            var questId = _keysScratch[k];
            if (!_states.TryGetValue(questId, out var qs)) continue;
            if (!qs.started || qs.completed) continue;

            bool changed = false;

            if (qs.so.sequentialObjectives)
            {
                int idx = GetFirstIncompleteObjectiveIndex(qs);
                if (idx >= 0) changed |= TryProgressObjective_OnInteract(qs.objectives[idx], msg);
            }
            else
            {
                for (int i = 0; i < qs.objectives.Length; ++i)
                {
                    var o = qs.objectives[i];
                    if (!o.completed) changed |= TryProgressObjective_OnInteract(o, msg);
                }
            }

            if (changed)
            {
                qs.completed = AreMandatoryObjectivesCompleted(qs);
                _changedIds.Add(questId);
            }
        }

        for (int i = 0; i < _changedIds.Count; ++i)
            OnQuestUpdated?.Invoke(_changedIds[i]);

        TryRaiseCompletedForChanged();

    }

    // 매니저 내부
    // #define CANCEL_ALL_MATCHES // 켜면 매칭되는 서브태스크를 모두 되돌림(기본: 한 개만)

    private void OnInteractCanceled(QuestEvents.InteractMsg msg)
    {
        Debug.Log("Final Events Canceled " + msg.id);

        // 활성 퀘스트 키 스냅샷
        _keysScratch.Clear();
        foreach (var id in _states.Keys) _keysScratch.Add(id);

        _changedIds.Clear();

        int idHash = msg.idHash;
        string idStr = msg.id;

        for (int k = 0; k < _keysScratch.Count; ++k)
        {
            var questId = _keysScratch[k];
            if (!_states.TryGetValue(questId, out var qs)) continue;
            if (!qs.started) continue;

            bool questChanged = false;

            // 모든 오브젝트 훑되, 서브태스크 단위로 되감기
            for (int i = qs.objectives.Length - 1; i >= 0; --i)
            {
                var os = qs.objectives[i];
                if (os.subs == null || os.subs.Length == 0) continue;

                bool objChanged = false;

                // 뒤→앞: 최근 것에 가까운 항목을 먼저 되감기
                for (int s = os.subs.Length - 1; s >= 0; --s)
                {
                    ref var sub = ref os.subs[s];
                    if (!SubMatches(in sub, idHash, idStr)) continue;
                    if (!sub.done) continue; // 이미 미완료면 건드릴 것 없음

                    // --- 되감기: 해당 서브태스크만 롤백 ---
                    sub.done = false;
                    objChanged = true;

#if !CANCEL_ALL_MATCHES
                    break; // 한 개만 되돌릴 때
#endif
                }

                if (!objChanged) continue;

                // --- 오브젝트 완료 상태 재계산 ---
                RecomputeObjectiveAfterCancel(os);
                questChanged = true;
            }

            if (questChanged)
            {
                qs.completed = AreMandatoryObjectivesCompleted(qs);
                _changedIds.Add(questId);
            }
        }


        for (int i = 0; i < _changedIds.Count; ++i)
            OnQuestUpdated?.Invoke(_changedIds[i]);


    }

    // sub와 취소 id 매칭(해시 우선, 문자열 Ordinal 폴백)
    static bool SubMatches(in SubTaskState sub, int idHash, string idStr)
    {
        if (idHash != 0)
            return sub.targetHash == idHash;

        return !string.IsNullOrEmpty(idStr) &&
               !string.IsNullOrEmpty(sub.targetId) &&
               string.Equals(sub.targetId, idStr, StringComparison.Ordinal);
    }

    // 서브태스크 변경 후 오브젝트 완료 상태/커서 재계산
    static void RecomputeObjectiveAfterCancel(ObjectiveState os)
    {
        var def = os.def;
        var subs = os.subs;
        int n = subs?.Length ?? 0;

        switch (def.type)
        {
            case ObjectiveType.InteractSet:
                {
                    // 모든 서브가 done이면 완료
                    bool all = true;
                    for (int i = 0; i < n; ++i)
                        if (!subs[i].done) { all = false; break; }
                    os.completed = all;
                    // 순서 커서는 사용하지 않음
                    break;
                }
            case ObjectiveType.InteractSequence:
                {
                    if (def.mustFollowOrder)
                    {
                        // 앞에서부터 연속된 done 개수 = seqIndex
                        int seq = 0;
                        for (int i = 0; i < n; ++i)
                        {
                            if (subs[i].done) seq++;
                            else break;
                        }
                        os.seqIndex = seq;
                        os.completed = (seq >= n);
                    }
                    else
                    {
                        // 순서 무시 → Set과 동일
                        bool all = true;
                        for (int i = 0; i < n; ++i)
                            if (!subs[i].done) { all = false; break; }
                        os.completed = all;
                    }
                    break;
                }
            case ObjectiveType.HoldOnTargets:
                {
                    // requiredCount(기본: 모든 서브) 충족 여부
                    int doneCnt = 0;
                    for (int i = 0; i < n; ++i) if (subs[i].done) doneCnt++;
                    int req = def.requiredCount <= 0 ? n : Mathf.Min(def.requiredCount, n);
                    os.completed = (doneCnt >= Mathf.Max(1, req));
                    break;
                }
            // 아래 타입은 InteractCanceled에서 다루지 않음(별도 이벤트에서 처리)
            case ObjectiveType.StayInArea:
            case ObjectiveType.Delivery:
            case ObjectiveType.TriggerFlags:
            default:
                break;
        }
    }



    void OnAreaStayTick(string areaId, float deltaSec, Vector3 _pos)
    {
        int areaHash = Animator.StringToHash(areaId);

        _keysScratch.Clear();
        foreach (var id in _states.Keys) _keysScratch.Add(id);
        _changedIds.Clear();

        for (int k = 0; k < _keysScratch.Count; ++k)
        {
            var questId = _keysScratch[k];
            if (!_states.TryGetValue(questId, out var qs)) continue;
            if (!qs.started || qs.completed) continue;

            bool changed = false;

            if (qs.so.sequentialObjectives)
            {
                int idx = GetFirstIncompleteObjectiveIndex(qs);
                if (idx >= 0) changed |= TryProgressObjective_OnArea(qs.objectives[idx], areaHash, deltaSec);
            }
            else
            {
                for (int i = 0; i < qs.objectives.Length; ++i)
                {
                    var o = qs.objectives[i];
                    if (!o.completed) changed |= TryProgressObjective_OnArea(o, areaHash, deltaSec);
                }
            }

            if (changed)
            {
                qs.completed = AreMandatoryObjectivesCompleted(qs);
                _changedIds.Add(questId);
            }
        }

        for (int i = 0; i < _changedIds.Count; ++i)
            OnQuestUpdated?.Invoke(_changedIds[i]);

        TryRaiseCompletedForChanged();

    }

    void OnDelivery(string itemId, string receiverId, Vector3 _pos)
    {
        int receiverHash = Animator.StringToHash(receiverId);

        _keysScratch.Clear();
        foreach (var id in _states.Keys) _keysScratch.Add(id);
        _changedIds.Clear();

        for (int k = 0; k < _keysScratch.Count; ++k)
        {
            var questId = _keysScratch[k];
            if (!_states.TryGetValue(questId, out var qs)) continue;
            if (!qs.started || qs.completed) continue;

            bool changed = false;

            if (qs.so.sequentialObjectives)
            {
                int idx = GetFirstIncompleteObjectiveIndex(qs);
                if (idx >= 0) changed |= TryProgressObjective_OnDelivery(qs.objectives[idx], itemId, receiverHash);
            }
            else
            {
                for (int i = 0; i < qs.objectives.Length; ++i)
                {
                    var o = qs.objectives[i];
                    if (!o.completed) changed |= TryProgressObjective_OnDelivery(o, itemId, receiverHash);
                }
            }

            if (changed)
            {
                qs.completed = AreMandatoryObjectivesCompleted(qs);
                _changedIds.Add(questId);
            }
        }

        for (int i = 0; i < _changedIds.Count; ++i)
            OnQuestUpdated?.Invoke(_changedIds[i]);

        TryRaiseCompletedForChanged();

    }

    void OnFlagRaised(string _flagId)
    {
        // 플래그형 목표는 즉시 재평가
        _keysScratch.Clear();
        foreach (var id in _states.Keys) _keysScratch.Add(id);
        _changedIds.Clear();

        for (int k = 0; k < _keysScratch.Count; ++k)
        {
            var questId = _keysScratch[k];
            if (!_states.TryGetValue(questId, out var qs)) continue;
            if (!qs.started || qs.completed) continue;

            bool changed = false;

            if (qs.so.sequentialObjectives)
            {
                int idx = GetFirstIncompleteObjectiveIndex(qs);
                if (idx >= 0) changed |= TryProgressObjective_RecheckFlags(qs.objectives[idx]);
            }
            else
            {
                for (int i = 0; i < qs.objectives.Length; ++i)
                {
                    var o = qs.objectives[i];
                    if (!o.completed) changed |= TryProgressObjective_RecheckFlags(o);
                }
            }

            if (changed)
            {
                qs.completed = AreMandatoryObjectivesCompleted(qs);
                _changedIds.Add(questId);
            }
        }

        for (int i = 0; i < _changedIds.Count; ++i)
            OnQuestUpdated?.Invoke(_changedIds[i]);

        TryRaiseCompletedForChanged();
    }

    // --- 진행 로직 ---
    static int GetFirstIncompleteObjectiveIndex(QuestState qs)
    {
        for (int i = 0; i < qs.objectives.Length; ++i)
            if (!qs.objectives[i].completed) return i;
        return -1;
    }

    static bool AreMandatoryObjectivesCompleted(QuestState qs)
    {
        for (int i = 0; i < qs.objectives.Length; ++i)
        {
            var o = qs.objectives[i];
            if (o.def.optional) continue;
            if (!o.completed) return false;
        }
        return true;
    }


    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void TryRaiseCompletedForChanged()
    {
        for (int i = 0; i < _changedIds.Count; ++i)
        {
            var qid = _changedIds[i];
            if (!_states.TryGetValue(qid, out var s) || s == null) continue;
            if (s.completed && !s.completionEventRaised)
            {
                s.so?.RaiseCompleted();        
                s.completionEventRaised = true; // 중복 방지
            }
        }
    }


    // 상호작용 이벤트 기반 진행
    static bool TryProgressObjective_OnInteract(ObjectiveState os, QuestEvents.InteractMsg msg)
    {
        if (os.completed) return false;
        var def = os.def;

        switch (def.type)
        {
            case ObjectiveType.InteractSet:
                {
                    if (os.subs.Length == 0) return false;
                    for (int s = 0; s < os.subs.Length; ++s)
                    {
                        ref var sub = ref os.subs[s];
                        if (!sub.done && sub.targetHash == msg.idHash)
                        {
                            sub.done = true;
                            // 전체 완료 판정
                            bool all = true;
                            for (int k = 0; k < os.subs.Length; ++k)
                                if (!os.subs[k].done) { all = false; break; }
                            os.completed = all;
                            return true;
                        }
                    }
                    return false;
                }

            case ObjectiveType.InteractSequence:
                {
                    if (os.subs.Length == 0) return false;

                    if (def.mustFollowOrder)
                    {
                        int idx = os.seqIndex;
                        if (idx >= 0 && idx < os.subs.Length)
                        {
                            if (msg.idHash == os.subs[idx].targetHash)
                            {
                                os.subs[idx].done = true;
                                os.seqIndex++;
                                if (os.seqIndex >= os.subs.Length) os.completed = true;
                                return true;
                            }
                            else if (def.resetOnWrongOrder)
                            {
                                // 리셋
                                for (int k = 0; k < os.subs.Length; ++k)
                                {
                                    var sub = os.subs[k];
                                    sub.done = false; sub.staySeconds = 0f;
                                    os.subs[k] = sub;
                                }
                                os.seqIndex = 0;
                                return true; // 상태 변동
                            }
                        }
                        return false;
                    }
                    else
                    {
                        // 순서 무시 => InteractSet과 동일
                        for (int s = 0; s < os.subs.Length; ++s)
                        {
                            ref var sub = ref os.subs[s];
                            if (!sub.done && sub.targetHash == msg.idHash)
                            {
                                sub.done = true;
                                bool all = true;
                                for (int k = 0; k < os.subs.Length; ++k)
                                    if (!os.subs[k].done) { all = false; break; }
                                os.completed = all;
                                return true;
                            }
                        }
                        return false;
                    }
                }

            case ObjectiveType.HoldOnTargets:
                {
                    if (msg.kind != InteractionKind.Hold) return false;
                    if (os.subs.Length == 0) return false;

                    for (int s = 0; s < os.subs.Length; ++s)
                    {
                        ref var sub = ref os.subs[s];
                        if (!sub.done && sub.targetHash == msg.idHash)
                        {
                            sub.done = true;

                            int req = def.requiredCount <= 0 ? os.subs.Length : Mathf.Min(def.requiredCount, os.subs.Length);
                            int doneCnt = 0;
                            for (int k = 0; k < os.subs.Length; ++k) if (os.subs[k].done) doneCnt++;
                            if (doneCnt >= req) os.completed = true;
                            return true;
                        }
                    }
                    return false;
                }

            case ObjectiveType.TriggerFlags:
                // 상호작용과 무관. 플래그 이벤트에서 처리(아래 함수 참조)
                return TryProgressObjective_RecheckFlags(os);

            case ObjectiveType.StayInArea:
            case ObjectiveType.Delivery:
            default:
                return false;
        }
    }

    // StayInArea: 영역 틱 기반 진행
    static bool TryProgressObjective_OnArea(ObjectiveState os, int areaHash, float deltaSec)
    {
        if (os.completed) return false;
        if (os.def.type != ObjectiveType.StayInArea) return false;
        if (os.subs.Length == 0) return false;

        bool touched = false;

        for (int s = 0; s < os.subs.Length; ++s)
        {
            ref var sub = ref os.subs[s];
            if (sub.targetHash != areaHash) continue;

            sub.staySeconds += deltaSec;
            if (!sub.done && sub.staySeconds >= Mathf.Max(0.01f, os.def.requiredStaySeconds))
            {
                sub.done = true;
                touched = true;
            }
        }

        if (!touched) return false;

        // 완료 조건: requiredCount(기본 1개 달성 시 완료)
        int need = os.def.requiredCount <= 0 ? 1 : os.def.requiredCount;
        int doneCnt = 0;
        for (int k = 0; k < os.subs.Length; ++k) if (os.subs[k].done) doneCnt++;
        if (doneCnt >= need) os.completed = true;

        return true;
    }

    // Delivery: 전달 이벤트 기반 진행
    static bool TryProgressObjective_OnDelivery(ObjectiveState os, string itemId, int receiverHash)
    {
        if (os.completed) return false;
        if (os.def.type != ObjectiveType.Delivery) return false;

        // 아이템 매칭
        var needItem = !string.IsNullOrEmpty(os.def.deliveryItemId) ? os.def.deliveryItemId :
                       (!string.IsNullOrEmpty(os.def.requiredItemId) ? os.def.requiredItemId : null);

        if (!string.IsNullOrEmpty(needItem) && needItem != itemId) return false;

        // 수령자 매칭(타깃이 지정되어 있다면)
        if (os.subs.Length > 0)
        {
            bool match = false;
            for (int i = 0; i < os.subs.Length; ++i)
            {
                if (os.subs[i].targetHash == receiverHash) { match = true; break; }
            }
            if (!match) return false;
        }
        // 카운팅 완료
        os.progressCount++;
        int need = os.def.requiredCount <= 0 ? 1 : os.def.requiredCount;
        if (os.progressCount >= need) os.completed = true;
        return true;
    }

    // TriggerFlags: 모든 플래그가 서 있으면 완료
    static bool TryProgressObjective_RecheckFlags(ObjectiveState os)
    {
        if (os.completed) return false;
        if (os.def.type != ObjectiveType.TriggerFlags) return false;

        var flags = os.def.requiredFlags;
        if (flags == null || flags.Length == 0) return false;

        for (int i = 0; i < flags.Length; ++i)
            if (!QuestFlags.Has(flags[i])) return false; 

        os.completed = true;
        return true;
    }

    // 퀘스트 시작/로드 직후 즉시 판정이 필요한 목표 평가
    static void EvaluateImmediateObjectives(QuestState qs)
    {
        for (int i = 0; i < qs.objectives.Length; ++i)
            TryProgressObjective_RecheckFlags(qs.objectives[i]); // TriggerFlags만 즉시 확인
        qs.completed = AreMandatoryObjectivesCompleted(qs);
    }

    readonly List<uint> _keysScratch = new(32);
    readonly List<uint> _changedIds = new(8);

   

}
