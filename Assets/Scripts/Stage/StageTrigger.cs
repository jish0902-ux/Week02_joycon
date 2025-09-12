using UnityEngine;

public class StageTrigger : MonoBehaviour
{
    public bool isEnd;
    [SerializeField] private StageSaveable stageSaveable;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        if (isEnd)
        {
            stageSaveable.SaveHierarchyWithCurrentStage();
        }
        else
        {
            // 기존 자식 전부 삭제 + 저장된 스냅샷대로 재구성
            stageSaveable.RestoreHierarchyWithCurrentStage();
        }
    }
}
