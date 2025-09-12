// Assets/Scripts/Spawn/ObjectManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[DisallowMultipleComponent]
public sealed class ObjectManager : MonoBehaviour
{
    // --- Singleton(원하면 DI로 바꿔도 됨) ---
    public static ObjectManager Instance { get; private set; }

    [SerializeField] private Transform defaultParent; // 풀에서 꺼낼 기본 부모(없으면 자기 Transform)

    // SO -> 풀, 인스턴스 -> 풀
    readonly Dictionary<SpawnableSO, ObjectPool<GameObject>> _pools = new(64);
    readonly Dictionary<GameObject, ObjectPool<GameObject>> _owner = new(256);

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (!defaultParent) defaultParent = transform;
    }

    // ---------- 외부 API ----------
    public void EnsurePool(SpawnableSO so)
    {
        if (!so || !so.prefab || _pools.ContainsKey(so)) return;

        var pool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                var go = Instantiate(so.prefab, defaultParent);
                go.SetActive(false);
                return go;
            },
            actionOnGet: go => go.SetActive(true),
            actionOnRelease: go => go.SetActive(false),
            actionOnDestroy: go => Destroy(go),
            collectionCheck: false,
            defaultCapacity: Mathf.Max(1, so.prewarm),
            maxSize: 512
        );

        // 프리웜
        for (int i = 0; i < so.prewarm; ++i)
        {
            var g = pool.Get();
            pool.Release(g);
        }

        _pools.Add(so, pool);
    }

    public GameObject Spawn(SpawnableSO so, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        if (!so || !so.prefab) return null;
        if (!_pools.TryGetValue(so, out var pool))
        {
            EnsurePool(so);
            pool = _pools[so];
        }

        var go = pool.Get();
        _owner[go] = pool;

        go.transform.SetPositionAndRotation(pos, rot);
        if (parent) go.transform.SetParent(parent, worldPositionStays: true);
        else go.transform.SetParent(defaultParent, worldPositionStays: true);

        if (go.TryGetComponent<IPooledObject>(out var p)) p.OnSpawned();

        return go;
    }

    public void Despawn(GameObject go)
    {
        if (!go) return;
        if (_owner.TryGetValue(go, out var pool))
        {
            if (go.TryGetComponent<IPooledObject>(out var p)) p.OnDespawned();
            _owner.Remove(go);
            pool.Release(go);
        }
        else
        {
            Debug.LogWarning($"[ObjectManager] Unknown owner pool for {go.name}");
            go.SetActive(false);
        }
    }

    public void DespawnAfter(GameObject go, float seconds)
        => StartCoroutine(DespawnCo(go, seconds));

    System.Collections.IEnumerator DespawnCo(GameObject go, float t)
    {
        if (t > 0f) yield return new WaitForSeconds(t);
        Despawn(go);
    }
}

public interface IPooledObject
{
    void OnSpawned();
    void OnDespawned();
}
