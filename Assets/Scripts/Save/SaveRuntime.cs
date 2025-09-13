using UnityEngine;

public sealed class SaveRuntime : MonoBehaviour
{
    [SerializeField] private CheckpointSaveSO sourceAsset;
    public static CheckpointSaveSO Current { get; private set; }

    void Awake()
    {
        if (Current != null) { Destroy(gameObject); return; }

        // 에셋을 런타임 복제(메모리 전용). 에셋 파일은 절대 수정 안 함.
        Current = ScriptableObject.Instantiate(sourceAsset);
        Current.name = sourceAsset ? sourceAsset.name + " (Runtime)" : "Checkpoint(Runtime)";
        Current.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        DontDestroyOnLoad(gameObject);
    }
}
