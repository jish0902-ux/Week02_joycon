using UnityEngine;

[CreateAssetMenu(menuName = "Game/Save/Checkpoint Save SO", fileName = "CheckpointSaveSO")]
public sealed class CheckpointSaveSO : ScriptableObject
{
    [SerializeField] private bool has;
    [SerializeField] private int sceneIndex;
    [SerializeField] private Vector2 pos;
    [SerializeField] private float rotZ;

    public void Set(Vector2 p, float rz, int sIdx)
    {
        has = true; pos = p; rotZ = rz; sceneIndex = sIdx;
    }

    public void Clear()
    {
        has = false; sceneIndex = -1; pos = default; rotZ = 0f;
    }

    public bool TryGet(int currentSceneIndex, out Vector2 p, out float rz)
    {
        if (has && sceneIndex == currentSceneIndex) { p = pos; rz = rotZ; return true; }
        p = default; rz = default; return false;
    }
}
