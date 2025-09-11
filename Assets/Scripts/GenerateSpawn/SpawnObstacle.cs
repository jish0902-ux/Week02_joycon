using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SpawnObstacle : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference fireAction;

    [Header("Probe")]
    [SerializeField, Min(0f)] private float rightOffset = 1.0f;
    [SerializeField, Range(0.05f, 1.0f)] private float probeRadius = 0.25f;
    [SerializeField] private string obstacleLayerName = "obstacle";
    [SerializeField] private bool drawGizmos = true;

    static readonly Collider2D[] _hits = new Collider2D[8];
    int _obstacleLayer = -1;
    int _includeMask = ~0;

    void Awake() => CacheLayer();
    void OnValidate() => CacheLayer();

    void OnEnable()
    {
        if (fireAction != null && fireAction.action != null)
        {
            fireAction.action.performed += OnFire;
            if (!fireAction.action.enabled) fireAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (fireAction != null && fireAction.action != null)
            fireAction.action.performed -= OnFire;
    }

    void CacheLayer()
    {
        _obstacleLayer = LayerMask.NameToLayer(obstacleLayerName);
        _includeMask = (_obstacleLayer >= 0) ? ~(1 << _obstacleLayer) : ~0; // obstacle Á¦¿Ü
    }

    void OnFire(InputAction.CallbackContext ctx)
    {
        Vector2 probePos = (Vector2)transform.position + (Vector2)transform.right * rightOffset;

        int count = Physics2D.OverlapCircleNonAlloc(probePos, probeRadius, _hits, _includeMask);
        for (int i = 0; i < count; i++)
        {
            var col = _hits[i];
            if (!col) continue;
            if (col.gameObject.layer == _obstacleLayer) continue;

            Debug.Log($"[RightProbe2D] Hit: {col.name} / Layer: {LayerMask.LayerToName(col.gameObject.layer)}");
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.yellow;
        Vector3 p = transform.position + transform.right * rightOffset;
        Gizmos.DrawWireSphere(p, probeRadius);
    }
}
