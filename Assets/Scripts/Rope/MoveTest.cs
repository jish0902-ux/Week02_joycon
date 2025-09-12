using UnityEngine;
using UnityEngine.InputSystem;

public class MoveTest : MonoBehaviour
{
    public float speed=0f;
    public Vector2 log = Vector2.zero;
    private MoveTest moveTest;
    public Ray2D ray2D;
    public Vector2 rayStart;
    public Vector2 rayDir;
    public LayerMask Obstacle;
    public float rayDistance=0.5f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        moveTest = GetComponent<MoveTest>();
        rayStart = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, rayDistance,  Obstacle);
        if (hit != null)
        {
            Debug.Log("∂•ø° ¥Í¿Ω");
        }
    }
    private void FixedUpdate()
    {
        
    }
    public void s(InputAction.CallbackContext context)
    {
        log=context.ReadValue<Vector2>();
        Debug.Log(log);
        Debug.Log(context);
    }
    private void OnDrawGizmosSelected()
    {
        Vector2 orgin = transform.position;
        Vector2 dir = Vector2.left;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(rayStart, rayDistance);
    }
}
