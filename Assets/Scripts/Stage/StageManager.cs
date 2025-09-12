using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public const int MaxStages = 8;

    [Header("Stage")]
    [SerializeField, Range(0, MaxStages - 1)] private int currentStage = 0;
    public int CurrentStage
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => currentStage;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => currentStage = Mathf.Clamp(value, 0, MaxStages - 1);
    }

    [Header("Prewarm / Capacity")]
    [SerializeField, Min(0)] private int initialCapacityPerStage = 128;

    // [기존] 빠른 단일 적용용: (id -> 스냅샷)
    private Dictionary<int, TransformSnapshot>[] _stageData;

    // [신규] 재구성(Respawn)용: (rootKeyId -> 노드 리스트)
    private Dictionary<int, List<NodeSnapshot>>[] _stageTrees;

    // 레지스트리: 저장/복구 대상
    private List<StageSaveable>[] _stageRegs;

    // 임시 버퍼들(할당 최소화)
    static readonly List<Transform> kCollectBuf = new List<Transform>(256);
    static readonly List<Transform> kPathBuf = new List<Transform>(64);
    static readonly StringBuilder kSB = new StringBuilder(192);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _stageData = new Dictionary<int, TransformSnapshot>[MaxStages];
        _stageTrees = new Dictionary<int, List<NodeSnapshot>>[MaxStages];
        _stageRegs = new List<StageSaveable>[MaxStages];
        for (int i = 0; i < MaxStages; i++)
        {
            _stageData[i] = new Dictionary<int, TransformSnapshot>(initialCapacityPerStage);
            _stageTrees[i] = new Dictionary<int, List<NodeSnapshot>>(64);
            _stageRegs[i] = new List<StageSaveable>(Mathf.Max(8, initialCapacityPerStage / 4));
        }
    }

    // ------------------------------------------------------------------
    // Register
    // ------------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Register(StageSaveable s)
    {
        if (!s) return;
        int idx = s.StageIndex;
        if (!IsValidStage(idx)) return;
        var list = _stageRegs[idx];
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], s)) return;
        list.Add(s);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unregister(StageSaveable s)
    {
        if (!s) return;
        int idx = s.StageIndex;
        if (!IsValidStage(idx)) return;
        _stageRegs[idx].Remove(s);
    }

    // ------------------------------------------------------------------
    // Save / Apply (Hierarchy)  - 기존 API 유지
    // ------------------------------------------------------------------
    public void SaveHierarchy(int stageIndex, StageSaveable root, bool useLocal, bool includeRoot = true)
    {
        if (!root || !IsValidStage(stageIndex)) return;

        // 수집
        kCollectBuf.Clear();
        CollectHierarchy(root.transform, kCollectBuf, includeRoot);

        // 1) 단건 적용용 딕셔너리(id -> snapshot) 채우기
        var dict = _stageData[stageIndex];
        // 2) 재구성용 트리(rootKeyId -> list<NodeSnapshot>) 채우기 (기존 데이터 덮어씀)
        int rootKeyId = MakeId(root.ObjectKey);
        if (!_stageTrees[stageIndex].TryGetValue(rootKeyId, out var list))
        {
            list = new List<NodeSnapshot>(kCollectBuf.Count + 4);
            _stageTrees[stageIndex][rootKeyId] = list;
        }
        else list.Clear();

        string baseKey = root.ObjectKey;

        for (int i = 0; i < kCollectBuf.Count; i++)
        {
            var t = kCollectBuf[i];
            // 상대 경로 문자열
            string path = MakeRelativePath(baseKey, root.transform, t, includeRoot, out int depth);

            // id(이전 호환)
            int id = Animator.StringToHash(path); // baseKey 포함된 path 해시 (안정)

            // 스냅샷
            TransformSnapshot snap = useLocal
                ? new TransformSnapshot(t.localPosition, t.localRotation, t.localScale)
                : new TransformSnapshot(t.position, t.rotation, t.localScale);

            dict[id] = snap;

            // 트리용 리스트
            list.Add(new NodeSnapshot(path, depth, snap));
        }

        // 깊이순 정렬(부모 → 자식 순으로)
        list.Sort((a, b) => a.Depth.CompareTo(b.Depth));
    }

    public void ApplyHierarchy(int stageIndex, StageSaveable root, bool useLocal, bool includeRoot = true)
    {
        if (!root || !IsValidStage(stageIndex)) return;

        var dict = _stageData[stageIndex];

        kCollectBuf.Clear();
        CollectHierarchy(root.transform, kCollectBuf, includeRoot);

        string baseKey = root.ObjectKey;
        for (int i = 0; i < kCollectBuf.Count; i++)
        {
            var t = kCollectBuf[i];
            string path = MakeRelativePath(baseKey, root.transform, t, includeRoot, out _);
            int id = Animator.StringToHash(path);
            if (!dict.TryGetValue(id, out var s)) continue;

            if (useLocal)
            {
                t.localPosition = s.Position;
                t.localRotation = s.Rotation;
                t.localScale = s.LocalScale;
            }
            else
            {
                t.SetPositionAndRotation(s.Position, s.Rotation);
                t.localScale = s.LocalScale;
            }
        }
    }

    // ------------------------------------------------------------------
    // Respawn (재구성): 기존 자식 파괴 → 저장 데이터 기준으로 다시 생성
    // ------------------------------------------------------------------
    /// <summary>
    /// 기존 자식들을 모두 제거하고, 저장된 트리 데이터를 바탕으로 동일한 계층/트랜스폼으로 재구성.
    /// factory가 null이면 이름만 동일한 빈 GameObject로 생성.
    /// </summary>
    public void RebuildHierarchy(
        int stageIndex,
        StageSaveable root,
        bool useLocal,
        bool includeRoot = true,
        System.Func<string, Transform, GameObject> factory = null)
    {
        if (!root || !IsValidStage(stageIndex)) return;

        int rootKeyId = MakeId(root.ObjectKey);
        if (!_stageTrees[stageIndex].TryGetValue(rootKeyId, out var nodes) || nodes.Count == 0)
            return;

        var rootT = root.transform;

        // 1) 기존 자식 모두 제거 (루트는 보존)
        for (int i = rootT.childCount - 1; i >= 0; --i)
            Destroy(rootT.GetChild(i).gameObject);

        // 2) 경로 -> Transform 맵 (부모 찾기용)
        var path2Tf = new Dictionary<string, Transform>(nodes.Count + 4);
        path2Tf["."] = rootT; // 루트 앵커

        // 3) 루트 적용(옵션)
        if (includeRoot)
        {
            // 리스트는 depth 순이라 첫 원소가 루트(".")일 가능성이 큼
            // 없더라도 안전하게 루프에서 처리됨
        }

        // 4) 생성 루프
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            string path = n.Path;

            // 루트 노드 처리
            if (path == ".")
            {
                if (includeRoot)
                {
                    if (useLocal)
                    {
                        rootT.localPosition = n.Snap.Position;
                        rootT.localRotation = n.Snap.Rotation;
                        rootT.localScale = n.Snap.LocalScale;
                    }
                    else
                    {
                        rootT.SetPositionAndRotation(n.Snap.Position, n.Snap.Rotation);
                        rootT.localScale = n.Snap.LocalScale;
                    }
                }
                continue;
            }

            // 부모 경로/리프 이름
            string parentPath = GetParentPath(path);
            if (!path2Tf.TryGetValue(parentPath, out var parentTf))
                parentTf = rootT;

            string leafName = GetLeafName(path); // "Cube" from "Root[0]/Cube[1]"

            GameObject go = factory != null
                ? factory(path, parentTf)                   // 사용자 팩토리
                : new GameObject(leafName);                 // 기본: 빈 GO

            var tf = go.transform;
            tf.SetParent(parentTf, worldPositionStays: false);

            if (useLocal)
            {
                tf.localPosition = n.Snap.Position;
                tf.localRotation = n.Snap.Rotation;
                tf.localScale = n.Snap.LocalScale;
            }
            else
            {
                tf.SetPositionAndRotation(n.Snap.Position, n.Snap.Rotation);
                tf.localScale = n.Snap.LocalScale;
            }

            path2Tf[path] = tf;
        }
    }

    // 편의 함수(현재 스테이지 기반)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SaveAllForCurrentStage() => SaveAllForStage(CurrentStage);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RestoreAllForCurrentStage() => RestoreAllForStage(CurrentStage);

    public void SaveAllForStage(int stageIndex)
    {
        if (!IsValidStage(stageIndex)) return;
        var list = _stageRegs[stageIndex];
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i];
            if (!s) continue;
            SaveHierarchy(stageIndex, s, s.UseLocal, s.IncludeRoot);
        }
    }

    public void RestoreAllForStage(int stageIndex)
    {
        if (!IsValidStage(stageIndex)) return;
        var list = _stageRegs[stageIndex];
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i];
            if (!s) continue;
            ApplyHierarchy(stageIndex, s, s.UseLocal, s.IncludeRoot);
        }
    }

    // ------------------------------------------------------------------
    // Utils
    // ------------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MakeId(string key) => Animator.StringToHash(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidStage(int idx) => (uint)idx < MaxStages;

    private static void CollectHierarchy(Transform root, List<Transform> buf, bool includeRoot)
    {
        if (!root) return;
        if (includeRoot) buf.Add(root);

        var stack = new Stack<Transform>(64);
        for (int i = 0; i < root.childCount; i++)
            stack.Push(root.GetChild(i));

        while (stack.Count > 0)
        {
            var t = stack.Pop();
            buf.Add(t);
            for (int i = 0; i < t.childCount; i++)
                stack.Push(t.GetChild(i));
        }
    }

    // baseKey + 상대경로 문자열 빌드 (예: "Root#001|./Child[0]/Sub[2]")
    private static string MakeRelativePath(string baseKey, Transform root, Transform node, bool includeRoot, out int depth)
    {
        kPathBuf.Clear();
        for (var t = node; t != null; t = t.parent)
        {
            kPathBuf.Add(t);
            if (ReferenceEquals(t, root)) break;
        }
        kPathBuf.Reverse();

        kSB.Length = 0;
        kSB.Append(baseKey);
        kSB.Append('|');

        int segCount = 0;
        bool started = false;
        for (int i = 0; i < kPathBuf.Count; i++)
        {
            var t = kPathBuf[i];
            if (!includeRoot && ReferenceEquals(t, root)) continue;
            if (!started) { kSB.Append('.'); started = true; }
            else
            {
                kSB.Append('/');
                kSB.Append(t.name);
                kSB.Append('[').Append(t.GetSiblingIndex()).Append(']');
                segCount++;
            }
        }

        // 루트 제외 저장인 경우 최상위 자식에서 segCount==1부터 시작
        // 루트 포함 저장이면 루트는 '.' 로만 표현
        if (!started) kSB.Append('.'); // 안전

        depth = segCount; // 부모→자식 정렬용
        return kSB.ToString();
    }

    private static string GetParentPath(string path)
    {
        // path 형식: "{baseKey}|." 또는 "{baseKey}|./A[0]/B[1]"
        int bar = path.IndexOf('|');
        int lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0) return path.Substring(0, bar + 2); // "{base}|."
        return path.Substring(0, lastSlash);
    }

    private static string GetLeafName(string path)
    {
        int bar = path.IndexOf('|');
        int lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0) return "."; // 루트
        int start = lastSlash + 1;
        int bracket = path.IndexOf('[', start);
        if (bracket < 0) bracket = path.Length;
        return path.Substring(start, bracket - start);
    }

    // ------------------------------------------------------------------
    // Data structs
    // ------------------------------------------------------------------
    public struct TransformSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 LocalScale;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TransformSnapshot(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            Position = pos; Rotation = rot; LocalScale = scale;
        }
    }

    public struct NodeSnapshot
    {
        public string Path;   // "{baseKey}|." or "{baseKey}|./Child[0]/Sub[1]" (상대경로)
        public int Depth;     // 부모→자식 정렬용
        public TransformSnapshot Snap;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeSnapshot(string path, int depth, TransformSnapshot snap)
        {
            Path = path; Depth = depth; Snap = snap;
        }
    }
}
