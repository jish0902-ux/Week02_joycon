using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AreaZone : MonoBehaviour
{
    public string displayName;

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true; // ������ Ʈ����
    }
}
