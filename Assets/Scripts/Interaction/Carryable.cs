using UnityEngine;

public class Carryable : MonoBehaviour
{
    public bool carrying = false;//드는중
    public float large = -1;
    public float weight=1;
    private float lxw;

    [Header("Event - Quest")]
    public string Id;
    public int ScannerID;

    private PlayerCarrying player;
    private LayerMask maskObstacle;

    void Start()
    {if (large < 0)//크기 설정 따로 안했으면
        {
            large = transform.localScale.x* transform.localScale.y;//크기 x*y로 저장
        }
        lxw = large*weight;//크기*무게=실제 무게
        GetComponent<Rigidbody2D>().mass = lxw;//무게 적용
        player = GameObject.Find("Player").GetComponent<PlayerCarrying>();//플레이어 찾아넣기
        maskObstacle = LayerMask.GetMask("Obstacle");
    }
    private void OnCollisionStay2D(Collision2D collision)
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

}
