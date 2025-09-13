using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public sealed class CheckpointTrigger2D_RuntimeSO : MonoBehaviour
{
    [Header("Ground Snap (Optional)")]
    [SerializeField] private bool snapToGround = false;
    [SerializeField, Range(0.01f, 0.5f)] private float groundProbe = 0.2f;
    [SerializeField] private LayerMask groundMask = ~0;

    void Reset() { GetComponent<Collider2D>().isTrigger = true; }

    void OnTriggerEnter2D(Collider2D other)
    {
        
        if (!other.CompareTag("Player") || SaveRuntime.Current == null) return;


        Debug.Log("Player Trigger");

        var pos = (Vector2)transform.position;
        if (snapToGround)
        {
            var hit = Physics2D.Raycast(pos + Vector2.up * groundProbe, Vector2.down, groundProbe * 2f, groundMask);
            if (hit.collider) pos = hit.point;
        }

        SaveRuntime.Current.Set(
            pos,
            transform.eulerAngles.z,
            SceneManager.GetActiveScene().buildIndex
        );
    }
}
