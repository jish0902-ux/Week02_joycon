using Game.Quests;
using System;
using System.Collections.Generic;
using UnityEngine;

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
        public ObjectiveState[] objectives;
    }

    // 퀘스트ID -> 상태
    readonly Dictionary<uint, QuestState> _states = new(16);

    // UI 갱신 등 알림
    public event Action<uint> OnQuestUpdated; // 파라미터: questId

    void OnEnable()
    {
        QuestEvents.OnInteract += OnInteract;
        QuestEvents.OnAreaStayTick += OnAreaStayTick;
        QuestEvents.OnDelivery += OnDelivery;
        QuestEvents.OnFlagRaised += OnFlagRaised;
    }
    void OnDisable()
    {
        QuestEvents.OnInteract -= OnInteract;
        QuestEvents.OnAreaStayTick -= OnAreaStayTick;
        QuestEvents.OnDelivery -= OnDelivery;
        QuestEvents.OnFlagRaised -= OnFlagRaised;
    }

    void Awake()
    {
        // 데모: 원하는 퀘스트 시작
        StartQuest(1001);
        StartQuest(1002);
        StartQuest(1003);
        StartQuest(1004);
        StartQuest(1005);
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

    // --- (세이브/로드) 확장 저장 포맷 ---
    [Serializable] struct SaveSub { public string id; public bool done; public float stay; }
    [Serializable] struct SaveObj { public SaveSub[] subs; public bool completed; public bool optional; public int seqIndex; public int progressCount; public int type; }
    [Serializable] struct SaveQuest { public uint id; public bool started; public bool completed; public SaveObj[] objectives; }
    [Serializable] struct SaveBlob { public SaveQuest[] quests; }

    public string ToJson()
    {
        var list = new List<SaveQuest>(_states.Count);
        foreach (var kv in _states)
        {
            var qs = kv.Value;
            var so = qs.so;
            var s = new SaveQuest { id = so.id, started = qs.started, completed = qs.completed };
            s.objectives = new SaveObj[qs.objectives.Length];

            for (int i = 0; i < qs.objectives.Length; ++i)
            {
                var o = qs.objectives[i];
                var soj = new SaveObj
                {
                    completed = o.completed,
                    optional = o.def.optional,
                    seqIndex = o.seqIndex,
                    progressCount = o.progressCount,
                    type = (int)o.def.type
                };
                soj.subs = new SaveSub[o.subs.Length];
                for (int k = 0; k < o.subs.Length; ++k)
                {
                    var sub = o.subs[k];
                    soj.subs[k] = new SaveSub { id = sub.targetId, done = sub.done, stay = sub.staySeconds };
                }
                s.objectives[i] = soj;
            }
            list.Add(s);
        }
        return JsonUtility.ToJson(new SaveBlob { quests = list.ToArray() }, prettyPrint: false);
    }

    public void FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var blob = JsonUtility.FromJson<SaveBlob>(json);
        if (blob.quests == null) return;

        _states.Clear();

        for (int i = 0; i < blob.quests.Length; ++i)
        {
            var sq = blob.quests[i];
            var so = FindQuestSO(sq.id);
            if (!so) continue;

            var qs = BuildState(so);
            qs.started = sq.started;
            qs.completed = sq.completed;

            int nObj = Mathf.Min(qs.objectives.Length, sq.objectives.Length);
            for (int o = 0; o < nObj; ++o)
            {
                var src = sq.objectives[o];
                var dst = qs.objectives[o];

                dst.completed = src.completed;
                dst.seqIndex = src.seqIndex;
                dst.progressCount = src.progressCount;

                int nSub = Mathf.Min(dst.subs.Length, src.subs.Length);
                for (int s = 0; s < nSub; ++s)
                {
                    // id 해시 매칭으로 안정 복구
                    int srcHash = Animator.StringToHash(src.subs[s].id ?? string.Empty);
                    var sub = dst.subs[s];
                    if (sub.targetHash == srcHash)
                    {
                        sub.done = src.subs[s].done;
                        sub.staySeconds = src.subs[s].stay;
                        dst.subs[s] = sub;
                    }
                }
                qs.objectives[o] = dst;
            }

            // 플래그형은 즉시 재확인
            EvaluateImmediateObjectives(qs);
            _states[sq.id] = qs;
        }
        // 필요한 경우 여기서 전체 브로드캐스트 가능
    }


}
