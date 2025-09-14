using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AreaZoneDetector : MonoBehaviour
{
    public AreaNameUI areaUI;
    public string defaultName = "거리";

    void Start()
    {
        areaUI?.SetAreaName(defaultName);
    }

    void OnTriggerEnter2D(Collider2D other)
    {            
        var zone = other.GetComponentInParent<AreaZone>();
        Debug.Log($"Enter: {zone.displayName}");
        if (zone != null)
        {
            areaUI?.SetAreaName(zone.displayName);
        }
    }

    // 나가도 변경하지 않으므로 Exit 없음
}
