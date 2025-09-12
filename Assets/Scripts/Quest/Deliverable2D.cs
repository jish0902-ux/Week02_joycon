using UnityEngine;

[DisallowMultipleComponent]
public sealed class Deliverable2D : MonoBehaviour
{
    [SerializeField] string receiverId = "NPC_A"; // Interactable Id와 동일하게
    [SerializeField] string itemId = "Key_A";
    [SerializeField] bool consumeOnDelivery = true;

    public string ReceiverId => receiverId;
    public string ItemId => itemId;
    public bool Consume => consumeOnDelivery;
}


/*if (_focus.TryGetComponent<Deliverable2D>(out var del))
{
    if (Inventory.HasItem(del.ItemId))
    {
        QuestEvents.RaiseDelivery(del.ItemId, del.ReceiverId, _focus.transform.position);
        if (del.Consume) Inventory.Consume(del.ItemId);
    }
}*/