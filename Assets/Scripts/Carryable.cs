using UnityEngine;
using UnityEngine.InputSystem.XR;

public class Carryable : MonoBehaviour
{
    public bool carrying = false;//드는중
    public float large = -1;
    public float weight;
    private float lxw;
    public PlayerCarrying player;
    public LayerMask maskObstacle;

    void Start()
    {if (large < 0)
        {
            large = transform.localScale.x* transform.localScale.y;
        }
        lxw = large*weight;
        player = GameObject.Find("Player").GetComponent<PlayerCarrying>();//플레이어 찾아넣기
        maskObstacle = LayerMask.GetMask("Obstacle");
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (carrying)
        {
            if (collision.gameObject.layer== LayerMask.NameToLayer("Obstacle"))
            {
                // 플레이어가 들고 있는 오브젝트 리스트에서 상대 오브젝트의 인덱스 확인
                int myIndex = player.carriedObjects.IndexOf(gameObject); // 자기 자신이 몇번째 짐인지
                if (myIndex != -1)
                {
                    if (player.collideCarrying > myIndex)
                    {
                        Debug.Log("indexChamge");
                        player.collideCarrying = myIndex;
                    }
                    Debug.Log($"짐 {myIndex + 1}충돌");
                }
                else
                {
                    Debug.Log("조졌"+myIndex);
                }
            }
        }
    }
    /*
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
