using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;

[DisallowMultipleComponent]
public sealed class QuestManager : MonoBehaviour
{
    [SerializeField] private QuestSO[] questDB; // ��ϵ� ����Ʈ��(�ʿ��� �͸� �ֱ�)

    // --- ��Ÿ�� ���� ---
    [Serializable]
    public struct SubTaskState
    {
        public int targetHash;     // �� ����ȭ��
        public string targetId;    // UI��
        public bool done;
    }

    [Serializable]
    public class ObjectiveState
    {
        public ObjectiveDef def;
        public SubTaskState[] subs; // InteractSet�� �� �����½�ũ �迭
        public bool completed;
    }

    [Serializable]
    public class QuestState
    {
        public QuestSO so;
        public bool started;
        public bool completed;
        public ObjectiveState[] objectives;
    }

    // ����ƮID -> ����
    readonly Dictionary<uint, QuestState> _states = new(16);

    // UI ���� �� �˸�
    public event Action<uint> OnQuestUpdated; // �Ķ����: questId

    void OnEnable() => QuestEvents.OnInteract += OnInteract;
    void OnDisable() => QuestEvents.OnInteract -= OnInteract;


    private void Awake()
    {
        StartQuest(1001);
    }

    public bool StartQuest(uint questId)
    {
        if (TryGetState(questId, out var qs))
        {
            if (qs.started) return false; // �̹� ����
            qs.started = true;
            OnQuestUpdated?.Invoke(questId);
            return true;
        }

        var so = FindQuestSO(questId);
        if (!so) return false;

        var newState = BuildState(so);
        newState.started = true;
        _states[questId] = newState;
        OnQuestUpdated?.Invoke(questId);
        return true;
    }

    // �ܺο��� ��ȸ: ������ ����(UI���� ���)
    public bool TryGetSnapshot(uint questId, out QuestState qs)
        => _states.TryGetValue(questId, out qs);

    // ----------------- ���� ���� -----------------
    QuestSO FindQuestSO(uint id)
    {
        for (int i = 0; i < questDB.Length; ++i)
            if (questDB[i] && questDB[i].id == id) return questDB[i];
        return null;
    }

    QuestState BuildState(QuestSO so)
    {
        var qs = new QuestState { so = so, started = false, completed = false };
        qs.objectives = new ObjectiveState[so.objectives.Length];

        for (int i = 0; i < so.objectives.Length; ++i)
        {
            ref var def = ref so.objectives[i];
            var os = new ObjectiveState { def = def, completed = false };

            if (def.type == ObjectiveType.InteractSet && def.targetIds != null && def.targetIds.Length > 0)
            {
                os.subs = new SubTaskState[def.targetIds.Length];
                for (int s = 0; s < def.targetIds.Length; ++s)
                {
                    var id = def.targetIds[s];
                    os.subs[s] = new SubTaskState
                    {
                        targetId = id,
                        targetHash = Animator.StringToHash(id),
                        done = false
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

    void OnInteract(QuestEvents.InteractMsg msg)
    {
        // ���� �� ��� ����Ʈ �˻�(�Ϲ������� ���� ���� ���� ����)
        foreach (var kv in _states)
        {
            var questId = kv.Key;
            var qs = kv.Value;
            if (!qs.started || qs.completed) continue;

            bool changed = false;

            // ���� ������ ù �̿Ϸ� ��ǥ�� �˻�
            if (qs.so.sequentialObjectives)
            {
                int idx = GetFirstIncompleteObjectiveIndex(qs);
                if (idx >= 0)
                    changed |= TryProgressObjective(qs.objectives[idx], msg);
            }
            else
            {
                for (int i = 0; i < qs.objectives.Length; ++i)
                    if (!qs.objectives[i].completed)
                        changed |= TryProgressObjective(qs.objectives[i], msg);
            }

            // ����Ʈ �Ϸ� ����
            if (changed)
            {
                bool allDone = true;
                for (int i = 0; i < qs.objectives.Length; ++i)
                {
                    var o = qs.objectives[i];
                    if (o.def.optional) continue;
                    if (!o.completed) { allDone = false; break; }
                }
                qs.completed = allDone;
                _states[questId] = qs; // ���� Ÿ�������� �����ϰ� �ٽ� �־���
                OnQuestUpdated?.Invoke(questId);
            }
        }
    }

    static int GetFirstIncompleteObjectiveIndex(QuestState qs)
    {
        for (int i = 0; i < qs.objectives.Length; ++i)
        {
            if (!qs.objectives[i].completed) return i;
        }
        return -1;
    }

    static bool TryProgressObjective(ObjectiveState os, QuestEvents.InteractMsg msg)
    {
        if (os.completed) return false;

        switch (os.def.type)
        {
            case ObjectiveType.InteractSet:
                {
                    // �ش� ID�� ���� �����½�ũ üũ
                    for (int s = 0; s < os.subs.Length; ++s)
                    {
                        ref var sub = ref os.subs[s];
                        if (!sub.done && sub.targetHash == msg.idHash)
                        {
                            sub.done = true;
                            // ��� ���� �Ϸ� �� ��ǥ �Ϸ�
                            bool all = true;
                            for (int k = 0; k < os.subs.Length; ++k)
                                if (!os.subs[k].done) { all = false; break; }

                            os.completed = all; // ������ �ϳ��� ������ ���⼭ true
                            return true;
                        }
                    }
                    return false;
                }
            default: return false;
        }
    }

    // --- (����) ���� ���̺�/�ε� ���� ---
    [Serializable] struct SaveSub { public string id; public bool done; }
    [Serializable] struct SaveObj { public SaveSub[] subs; public bool completed; public bool optional; }
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
                var soj = new SaveObj { completed = o.completed, optional = o.def.optional };
                soj.subs = new SaveSub[o.subs.Length];
                for (int k = 0; k < o.subs.Length; ++k)
                    soj.subs[k] = new SaveSub { id = o.subs[k].targetId, done = o.subs[k].done };
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

                int nSub = Mathf.Min(dst.subs.Length, src.subs.Length);
                for (int s = 0; s < nSub; ++s)
                {
                    var sub = dst.subs[s];
                    // id ��Ī ����(���� ���� ���)
                    int srcHash = Animator.StringToHash(src.subs[s].id);
                    if (sub.targetHash == srcHash) sub.done = src.subs[s].done;
                    dst.subs[s] = sub;
                }
                qs.objectives[o] = dst;
            }
            _states[sq.id] = qs;
        }
        // �ѹ��� UI ���� ���ϸ� ���⼭ ��ü ��ε�ĳ��Ʈ ����
    }
}
