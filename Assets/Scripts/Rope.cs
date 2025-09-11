using UnityEngine;
using UnityEngine.InputSystem;

public class Rope : MonoBehaviour
{
    public float climbSpeed = 3f;
    private Rigidbody2D rb;
    private bool isOnRope = false;
    private Vector2 ropeInput; 
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
    public void OnRope(InputAction.CallbackContext ctx)
    {
        ropeInput = ctx.ReadValue<Vector2>();
    }
    // Update is called once per frame
    void Update()
    {
        if (isOnRope)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, ropeInput.y * climbSpeed);
            rb.gravityScale = 0;
        }
        else
        {
            rb.gravityScale = 1;
        }
    }
    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Rope")) isOnRope = true;
    }
    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag("Rope")) isOnRope = false;
    }
}
