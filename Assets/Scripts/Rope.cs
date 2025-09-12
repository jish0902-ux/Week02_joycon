using UnityEngine;
using UnityEngine.InputSystem;

public class Rope : MonoBehaviour
{
    public InputSystem_Actions action;
    public float moveSpeed = 5f;
    public float climbSpeed = 3f;
    public LayerMask ropeLayer;
    private Vector2 moveInput;
    private bool isOnRope = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        action = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        //action.Enable();
    }
    // Update is called once per frame
    void Update()
    {
        // 좌우 이동
        transform.position += new Vector3(moveInput.x, 0, 0) * moveSpeed * Time.deltaTime;

        // 로프 위/아래 이동
        if (isOnRope)
        {
            transform.position += new Vector3(0, moveInput.y, 0) * climbSpeed * Time.deltaTime;
        }

        // 레이캐스트로 로프 감지
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.up, 0.5f, ropeLayer);
        if (hit.collider != null)
        {
            isOnRope = true;
        }
        else
        {
            hit = Physics2D.Raycast(transform.position, Vector2.down, 0.5f, ropeLayer);
            isOnRope = hit.collider != null;
        }
    }
    public void OnRope(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
        Debug.Log("onRope");
    }

    private void OnDrawGizmos()
    {
        // 레이 시각화
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 0.5f);
    }
}
