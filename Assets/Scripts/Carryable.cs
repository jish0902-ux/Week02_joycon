using UnityEngine;
using UnityEngine.InputSystem.XR;

public class Carryable : MonoBehaviour
{
    public bool carrying = false;//드는중
    public float large = -1;
    public float weight;
    private float lxw;
    void Start()
    {if (large < 0)
        {
            large = transform.localScale.x* transform.localScale.y;
        }
        lxw = large*weight;
    }
    /* public Player player;
     public LayerMask maskObstacle;
     Controller2D controller;//컨트롤러 커스텀 함수
     BoxCollider2D box;
     RaycastHit2D[] results =new RaycastHit2D[4];
     private void Awake()
     {
         box=GetComponent<BoxCollider2D>();
     }
     // Start is called once before the first execution of Update after the MonoBehaviour is created


     // Update is called once per frame
     void Update()
     {
         //레이 사용
         Vector2 size = Vector2.Scale(box.size, transform.lossyScale);
         RaycastHit2D hit = Physics2D.BoxCast(transform.position,size,0,Vector2.zero,0,maskObstacle );
         if (hit.collider != null)
         {

         }
         //집었으면

     }*/

    //
}
