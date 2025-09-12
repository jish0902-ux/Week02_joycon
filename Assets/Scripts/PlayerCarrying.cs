using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCarrying : MonoBehaviour
{
    [Header("Carry Settings")]
    public Transform holdPoint;
    public float carryingTop;//제일 위에 들고있는 위치
    public Vector2 dropOffset = new Vector2(1.0f, 0f); // 플레이어 기준 드롭 위치
    public LayerMask carryableMask;

    Vector2 lastObjSize;
    public float pickUpRange = 1.5f;
    public int maxCarryCount = 3;
    public float stackOffsetY = 0.5f; // 오브젝트 간 Y 간격
    Controller2D controller2D;
    private bool showDropGizmo = false;
    Vector2 lastDropPos;
    float lastObjRadius = 0.25f;

    private List<GameObject> carriedObjects = new List<GameObject>();

    [Header("Interaction Cooldown")]
    public float interactCooldown = 0.5f; // 쿨타임
    private float lastInteractTime = 0;

    private void Start()
    {
        // HoldPoint 생성
        GameObject hp = new GameObject("HoldPoint");
        hp.transform.parent = transform;
        hp.transform.localPosition = new Vector2(0, 0.5f);
        holdPoint = hp.transform;
        carryingTop = 0f; // 높이 초기화
        controller2D=GetComponent<Controller2D>();
    }

    private void LateUpdate()
    {
        // 들고 있는 오브젝트를 holdPoint 위로 순차적으로 쌓기
        carryingTop = 0f; // 누적 높이 초기화

        for (int i = 0; i < carriedObjects.Count; i++)
        {
            if (carriedObjects[i] != null)
            {
                float objHeight = carriedObjects[i].transform.localScale.y;
                // holdPoint 위에 누적된 높이만큼 올려서 배치
                carriedObjects[i].transform.position = holdPoint.position + new Vector3(0, carryingTop + objHeight / 2f, 0);
                carryingTop += objHeight; // 다음 오브젝트를 위해 누적 높이 업데이트
            }
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed && Time.time - lastInteractTime >= interactCooldown)
        {
            lastInteractTime = Time.time;
            TryPickUp();
        }
    }

    void TryPickUp()
    {
        if (carriedObjects.Count >= maxCarryCount)
        {
            Debug.Log("Cannot pick up: Max carry count reached");
            return;
        }

        // 주변 오브젝트 배열 가져오기
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, pickUpRange, carryableMask);
        GameObject closestObj = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            Carryable carryable = hit.GetComponent<Carryable>();
            if (carryable != null && carryable.carrying)
            {
                continue; // 이미 들고 있는 오브젝트는 무시
            }

            float distance = Vector2.Distance(transform.position, hit.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestObj = hit.gameObject;
            }
        }

        // PickUp 가능한 가장 가까운 오브젝트가 있으면 처리
        if (closestObj != null)
        {
            Rigidbody2D rb = closestObj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                // Z 회전 정리
                Vector3 rot = closestObj.transform.eulerAngles;
                if (rot.z < 90f || rot.z >= 270f)
                    rot.z = 0f;
                else
                    rot.z = 180f;
                closestObj.transform.eulerAngles = rot;

                rb.freezeRotation = true;
                rb.bodyType = RigidbodyType2D.Kinematic; // 물리 비활성화
            }

            carriedObjects.Add(closestObj);

            Carryable carryable = closestObj.GetComponent<Carryable>();
            if (carryable != null)
                carryable.carrying = true;

            Debug.Log($"Picked up: {closestObj.name} | Total: {carriedObjects.Count}");
        }
    }

    public void OnDrop(InputAction.CallbackContext context)
    {
        if (context.performed && Time.time - lastInteractTime >= interactCooldown)
        {
            lastInteractTime = Time.time; // 드랍 쿨타임

            if (carriedObjects.Count > 0)
            {
                GameObject obj = carriedObjects[carriedObjects.Count - 1];//젤 위에 들고있는 오브젝
                Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();

                // 플레이어가 바라보는 방향에 드롭 위치 계산
                Vector2 dropPos = (Vector2)transform.position + dropOffset * controller2D.collisions.faceDir;

                // ✅ 기즈모용 위치 저장
                lastDropPos = dropPos;
                lastObjSize = obj.transform.localScale;
                showDropGizmo = true;

                // ✅ 레이캐스트로 드롭할 공간 확인
                Collider2D hit = Physics2D.OverlapBox(dropPos, lastObjSize, LayerMask.GetMask("Obstacle"));
                if (hit != null)
                {
                    Debug.Log("Cannot drop: wall detected at drop position");
                    return; // 벽에 막혀 있으면 드롭 취소
                }

                // ✅ 안전한 위치라면 드롭 진행
                if (rb != null)
                {
                    rb.freezeRotation = false;
                    rb.bodyType = RigidbodyType2D.Dynamic;
                }

                obj.transform.position = dropPos;

                Carryable carryable = obj.GetComponent<Carryable>();
                if (carryable != null)
                    carryable.carrying = false;

                carriedObjects.RemoveAt(carriedObjects.Count - 1);
                Debug.Log($"Dropped: {obj.name}");
            }
        }
    }
    private void OnDrawGizmos()
    {
        if (showDropGizmo)
        {
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(lastDropPos, lastObjSize);
        }
    }
}
