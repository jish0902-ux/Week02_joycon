using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class InteractionOutline : MonoBehaviour
{
    [Header("자식 오브젝트 (퍼블릭으로 할당)")]
    public GameObject Outline; // 예: 아웃라인
    public GameObject Effect; // 예: 아이콘

    [Tooltip("플레이어 태그 이름")]
    public string playerTag = "Player";

    void Reset()
    {
        // 트리거로 자동 설정
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void Start()
    {
        // 시작 시 꺼두기
        SetChildrenActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            SetChildrenActive(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            SetChildrenActive(false);
        }
    }

    private void SetChildrenActive(bool active)
    {
        if (Outline != null) Outline.SetActive(active);
        if (Effect != null) Effect.SetActive(active);
    }
}
