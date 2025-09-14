using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AreaZoneDetector : MonoBehaviour
{
    public AreaNameUI areaUI;
    public string defaultName = "�Ÿ�";

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

    // ������ �������� �����Ƿ� Exit ����
}
