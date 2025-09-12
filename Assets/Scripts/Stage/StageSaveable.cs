using UnityEngine;

[DisallowMultipleComponent]
public sealed class StageSaveable : MonoBehaviour
{
    [Header("Stage / Key")]
    [SerializeField, Range(0, StageManager.MaxStages - 1)]
    private int stageIndex = 0;

    [SerializeField] private string objectKey = "Root#001";

    [Header("Options")]
    [SerializeField] private bool useLocal = false;     // 로컬 좌표로 저장/복구
    [SerializeField] private bool includeRoot = true;   // 루트 자신 포함 여부

    public int StageIndex => stageIndex;
    public string ObjectKey => objectKey;
    public bool UseLocal => useLocal;
    public bool IncludeRoot => includeRoot;

    void OnEnable() { if (StageManager.Instance) StageManager.Instance.Register(this); }
    void OnDisable() { if (StageManager.Instance) StageManager.Instance.Unregister(this); }

    // ------ Save ------
    public void SaveHierarchyWithCurrentStage()
    {
        var mgr = StageManager.Instance;
        if (!mgr) return;
        mgr.SaveHierarchy(mgr.CurrentStage, this, useLocal, includeRoot);
    }

    public void SaveHierarchyMyStage()
    {
        var mgr = StageManager.Instance;
        if (!mgr) return;
        mgr.SaveHierarchy(stageIndex, this, useLocal, includeRoot);
    }

    // ------ Restore (재구성) ------
    // 기본: 기존 자식 전부 삭제 후, 저장된 트리로 다시 생성
    public void RestoreHierarchyWithCurrentStage()
    {
        var mgr = StageManager.Instance;
        if (!mgr) return;
        mgr.RebuildHierarchy(mgr.CurrentStage, this, useLocal, includeRoot, factory: null);
    }

    public void RestoreHierarchyMyStage()
    {
        var mgr = StageManager.Instance;
        if (!mgr) return;
        mgr.RebuildHierarchy(stageIndex, this, useLocal, includeRoot, factory: null);
    }

    // 필요 시, 프리팹 팩토리 제공 버전(예: 경로별 스폰)
    public void RestoreWithFactory(System.Func<string, Transform, GameObject> factory)
    {
        var mgr = StageManager.Instance;
        if (!mgr) return;
        mgr.RebuildHierarchy(mgr.CurrentStage, this, useLocal, includeRoot, factory);
    }
}
