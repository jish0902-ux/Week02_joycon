using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class AreaStayTrigger2D : MonoBehaviour
{
    [SerializeField] string areaId = "Area_A";
    [SerializeField] string playerTag = "Player";

    void Reset()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        QuestEvents.RaiseAreaStayTick(areaId, Time.deltaTime, transform.position);
    }
}
