using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AreaZoneDetector : MonoBehaviour
{
    public AreaNameUI areaUI;
    public string defaultName = "�Ÿ�";

    [Header("Filter")]
    [Tooltip("�� ���̾��� �ݶ��̴����� ���� (��: AreaZone)")]
    [SerializeField] private LayerMask zoneMask;

    void Awake()
    {
        if (areaUI == null)
            Debug.LogWarning("[AreaZoneDetector] areaUI�� ������ϴ�. �ν����Ϳ��� �����ϼ���.", this);
    }

    void Start()
    {
        if (areaUI != null)
            areaUI.SetAreaName(defaultName);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // ���̾� ����: ��ġ �ʴ� Ʈ���ſ� ���� �� ��
        if (zoneMask.value != 0 && (zoneMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        var zone = other.GetComponentInParent<AreaZone>();
        if (zone == null)
        {
            // �ٸ� �뵵�� Ʈ���ſ� �������� �� ����� ��
            // Debug.Log($"Enter non AreaZone: {other.name}");
            return;
        }

        // ���� ���� zone�� null �ƴ�
        // Debug.Log($"Enter: {zone.displayName}");
        areaUI?.SetAreaName(zone.displayName);
    }

    // ���� �� �⺻������ ������ �ʹٸ� �ּ� ����
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
