using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AreaZoneDetector : MonoBehaviour
{
    public AreaNameUI areaUI;
    public string defaultName = "거리";

    [Header("Filter")]
    [Tooltip("이 레이어의 콜라이더에만 반응 (예: AreaZone)")]
    [SerializeField] private LayerMask zoneMask;

    void Awake()
    {
        if (areaUI == null)
            Debug.LogWarning("[AreaZoneDetector] areaUI가 비었습니다. 인스펙터에서 연결하세요.", this);
    }

    void Start()
    {
        if (areaUI != null)
            areaUI.SetAreaName(defaultName);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 레이어 필터: 원치 않는 트리거엔 반응 안 함
        if (zoneMask.value != 0 && (zoneMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        var zone = other.GetComponentInParent<AreaZone>();
        if (zone == null)
        {
            // 다른 용도의 트리거에 진입했을 때 여기로 옴
            // Debug.Log($"Enter non AreaZone: {other.name}");
            return;
        }

        // 여기 오면 zone은 null 아님
        // Debug.Log($"Enter: {zone.displayName}");
        areaUI?.SetAreaName(zone.displayName);
    }

    // 나갈 때 기본값으로 돌리고 싶다면 주석 해제
    /*
    void OnTriggerExit2D(Collider2D other)
    {
        if (zoneMask.value != 0 && (zoneMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        if (other.GetComponentInParent<AreaZone>() != null)
            areaUI?.SetAreaName(defaultName);
    }
    */
}
