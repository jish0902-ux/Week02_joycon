using UnityEngine;

[DisallowMultipleComponent]
public class SpikeObstacle : MonoBehaviour
{
    void OnCollisionEnter2D(Collision2D c)
    {
        var contact = c.GetContact(0);
        Debug.Log($"Hit {c.collider.name} via my child {contact.collider.name}");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"Trigger with {other.name}");
    }
}
