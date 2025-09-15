using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PlayerRespawn2D_RuntimeSO : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Respawn")]
    [SerializeField] private bool snapToGroundOnRespawn = true;
    [SerializeField, Range(0.01f, 0.5f)] private float groundProbe = 0.2f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Auto Death (Optional)")]
    [SerializeField] private bool enableKillY = false;
    [SerializeField] private float killY = -20f;

    Vector2 _initialPos;
    float _initialRotZ;

    void Awake()
    {
        if (!rb) TryGetComponent(out rb);
        _initialPos = transform.position;
        _initialRotZ = transform.eulerAngles.z;
    }

    void Start()
    {
        var so = SaveRuntime.Current;
        if (so != null && so.TryGet(SceneManager.GetActiveScene().buildIndex, out var pos, out var rz))
            Teleport(pos, rz, snapToGroundOnRespawn);
    }

    void Update()
    {
        if (enableKillY && transform.position.y < killY) Die();
    }

    public void Die()
    {
        var so = SaveRuntime.Current;
        if (so != null && so.TryGet(SceneManager.GetActiveScene().buildIndex, out var pos, out var rz))
            Teleport(pos, rz, snapToGroundOnRespawn);
        else
            Teleport(_initialPos, _initialRotZ, snapToGroundOnRespawn);
    }

    void Teleport(Vector2 pos, float rotZ, bool snap)
    {

        transform.SetPositionAndRotation(
               new Vector3(pos.x, pos.y, transform.position.z),
               Quaternion.Euler(0f, 0f, rotZ)
           );

       /* if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = pos;
            rb.rotation = rotZ;
        }
        else
        {
            transform.SetPositionAndRotation(
                new Vector3(pos.x, pos.y, transform.position.z),
                Quaternion.Euler(0f, 0f, rotZ)
            );
        }*/

        if (snap)
        {
            var start = (Vector2)transform.position + Vector2.up * groundProbe;
            var hit = Physics2D.Raycast(start, Vector2.down, groundProbe * 2f, groundMask);
            if (hit.collider)
            {
                var p = transform.position; p.y = hit.point.y;
                if (rb) rb.position = p; else transform.position = p;
            }
        }
    }
}
