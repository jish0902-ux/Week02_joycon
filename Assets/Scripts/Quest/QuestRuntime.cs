using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-500)]
public sealed class QuestRuntime : MonoBehaviour
{
    public static QuestRuntime Instance
    {
        get
        {
            if (_inst == null)
            {
#if UNITY_2023_1_OR_NEWER
                _inst = FindFirstObjectByType<QuestRuntime>(FindObjectsInactive.Include);
#else
                _inst = FindObjectOfType<QuestRuntime>();
#endif
                if (_inst == null)
                {
                    var go = new GameObject(nameof(QuestRuntime));
                    _inst = go.AddComponent<QuestRuntime>();
                    DontDestroyOnLoad(go);
                }
            }
            return _inst;
        }
    }
    static QuestRuntime _inst;

    [Header("Initial Flags (for debug)")]
    [SerializeField] private string[] initialFlags;

    [Header("Initial Items (for debug)")]
    [SerializeField] private string[] initialItemIds;
    [SerializeField] private int[] initialItemCounts;

    readonly HashSet<int> _flags = new(128);
    readonly Dictionary<int, int> _items = new(128);

    // Events
    public event Action<int/*flagHash*/> OnFlagRaised;
    public event Action<int/*flagHash*/> OnFlagCleared;
    public event Action<int/*itemHash*/, int/*newCount*/> OnItemChanged;

    void Awake()
    {
        if (_inst != null && _inst != this) { Destroy(gameObject); return; }
        _inst = this;
        DontDestroyOnLoad(gameObject);

        // Seed (디버그/테스트용)
        if (initialFlags != null)
            for (int i = 0; i < initialFlags.Length; ++i)
                _flags.Add(Animator.StringToHash(initialFlags[i] ?? string.Empty));

        int n = Mathf.Min(initialItemIds?.Length ?? 0, initialItemCounts?.Length ?? 0);
        for (int i = 0; i < n; ++i)
        {
            int h = Animator.StringToHash(initialItemIds[i] ?? string.Empty);
            int c = Mathf.Max(0, initialItemCounts[i]);
            if (c > 0) _items[h] = c;
        }
    }

    // -------- Flags --------
    public bool HasFlag(int flagHash) => _flags.Contains(flagHash);
    public bool HasFlag(string flagId) => _flags.Contains(Animator.StringToHash(flagId ?? string.Empty));

    public bool SetFlag(string flagId)
    {
        int h = Animator.StringToHash(flagId ?? string.Empty);
        if (_flags.Add(h))
        {
            OnFlagRaised?.Invoke(h);
            // 기존 QuestEvents와 연동 원하면 여기서도 Raise 가능:
            QuestEvents.RaiseFlag(flagId);
            return true;
        }
        return false;
    }

    public bool ClearFlag(string flagId)
    {
        int h = Animator.StringToHash(flagId ?? string.Empty);
        if (_flags.Remove(h))
        {
            OnFlagCleared?.Invoke(h);
            return true;
        }
        return false;
    }

    // -------- Items --------
    public bool HasItem(int itemHash)
        => _items.TryGetValue(itemHash, out var c) && c > 0;

    public bool HasItem(string itemId)
        => HasItem(Animator.StringToHash(itemId ?? string.Empty));

    public int GetCount(string itemId)
    {
        int h = Animator.StringToHash(itemId ?? string.Empty);
        return _items.TryGetValue(h, out var c) ? c : 0;
    }

    public void AddItem(string itemId, int count = 1)
    {
        if (count <= 0) return;
        int h = Animator.StringToHash(itemId ?? string.Empty);
        _items.TryGetValue(h, out var cur);
        cur += count;
        _items[h] = cur;
        OnItemChanged?.Invoke(h, cur);
    }

    public bool Consume(string itemId, int count = 1)
    {
        if (count <= 0) return true;
        int h = Animator.StringToHash(itemId ?? string.Empty);
        if (!_items.TryGetValue(h, out var cur) || cur < count) return false;
        cur -= count;
        if (cur <= 0) _items.Remove(h);
        else _items[h] = cur;
        OnItemChanged?.Invoke(h, cur);
        return true;
    }

    // --- (선택) 간단 세이브/로드: 플래그/인벤만 ---
    [Serializable] struct SaveBlob { public int[] flags; public int[] itemHashes; public int[] itemCounts; }

    public string ToJson()
    {
        var flagsArr = new int[_flags.Count];
        int i = 0; foreach (var h in _flags) flagsArr[i++] = h;

        var itemHashes = new int[_items.Count];
        var itemCounts = new int[_items.Count];
        i = 0; foreach (var kv in _items) { itemHashes[i] = kv.Key; itemCounts[i] = kv.Value; i++; }

        return JsonUtility.ToJson(new SaveBlob { flags = flagsArr, itemHashes = itemHashes, itemCounts = itemCounts }, false);
    }

    public void FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var b = JsonUtility.FromJson<SaveBlob>(json);
        _flags.Clear(); _items.Clear();

        if (b.flags != null) for (int i = 0; i < b.flags.Length; ++i) _flags.Add(b.flags[i]);
        int n = Mathf.Min(b.itemHashes?.Length ?? 0, b.itemCounts?.Length ?? 0);
        for (int i = 0; i < n; ++i)
        {
            var cnt = Mathf.Max(0, b.itemCounts[i]);
            if (cnt > 0) _items[b.itemHashes[i]] = cnt;
        }
    }
}
