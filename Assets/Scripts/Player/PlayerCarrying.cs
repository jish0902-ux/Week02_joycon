using Game.Quests;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerCarrying : MonoBehaviour
{
    [Header("Carry Settings")]
    public Transform holdPoint;
    public float carryingTop;//제일 위에 들고있는 위치
    public Vector2 dropOffset; // 플레이어 기준 드롭 위치
    public LayerMask carryableMask;
    public LayerMask maskObstacle;

    private Vector2 lastObjSize;//들고있는 것중 제일 마지막 오브젝 간격
    public float pickUpRange = 1.5f;
    public int maxCarryCount = 3;
    public float stackOffsetY = 0.5f; // 오브젝트 간격
    Controller2D controller2D;
    private bool showDropGizmo = false;
    Vector2 lastDropPos;
    Vector2 dropPos;
    float lastObjRadius = 0.25f;
    public int collideCarrying=0;//충돌한 짐 넘버 (현재 들고있는 것보다 높게 유지해야 안떨어짐)닿은거 이상 다 떨어질거야

    public List<GameObject> carriedObjects = new List<GameObject>();

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
    private void Update()
    {
        if (collideCarrying < carriedObjects.Count)
        {   //짐이 충돌하여 그 넘버를 받으면
            CarryingDrop();
        }
        if (collideCarrying < 0)
        {
            //Debug.Log(999);
        }
    }
    private void LateUpdate()
    {


        carryingTop = 0f; // 누적 높이 초기화
        for (int i = 0; i < carriedObjects.Count; i++)
        {
            if (carriedObjects[i] != null)
            {
                float objHeight = carriedObjects[i].GetComponent<Collider2D>().bounds.size.y;
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

                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.freezeRotation = true;
            }




            carriedObjects.Add(closestObj);

            collideCarrying++;//충돌 할 수 있는 물체+ (최대치+1유지해야 안떨어짐)
            Carryable carryable = closestObj.GetComponent<Carryable>();
            if (carryable != null)
                carryable.carrying = true;


            #region Events
            Interactable2D _focus;
            _focus = closestObj.GetComponent<Interactable2D>();

            if (_focus != null)
            {
                // 선행 조건 재검증
                if (!_focus.HasRequiredFlags(QuestFlags.Has)) return;
                if (!_focus.CheckItem(Inventory.HasItem)) return;

                QuestEvents.RaiseInteract(_focus.Id, _focus.transform.position, InteractionKind.Press);

            }
            else
            {
                Debug.Log("_focus is null");
            }

            #endregion

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
                BoxCollider2D box = obj.GetComponent<BoxCollider2D>();
                Vector2 checkSize;
                float carru = obj.transform.localScale.y;
                if (box != null)
                    checkSize = box.size; // 실제 콜라이더 크기 사용
                else
                    checkSize = obj.transform.localScale;

                // 플레이어가 바라보는 방향에 드롭 위치 계산
                dropOffset = new Vector2((obj.transform.localScale.x+transform.localScale.x)/2,0);//들고있는 것 /2+플레이어 크기
                Vector2 dropPos = (Vector2)transform.position + dropOffset * controller2D.collisions.faceDir;

                //  기즈모용 위치 저장
                lastDropPos = dropPos;
                lastObjSize = obj.GetComponent<Collider2D>().bounds.size*0.9f;//사이즈
                showDropGizmo = true;

                //  레이캐스트로 드롭할 공간 확인
                Collider2D hit = Physics2D.OverlapBox(dropPos, lastObjSize,0, LayerMask.GetMask("Obstacle"));
                if (hit != null)
                {
                    Debug.Log("막혔어");
                    return; // 벽에 막혀 있으면 드롭 취소
                }

                //  안전한 위치라면 드롭 진행
                if (rb != null)
                {
                    rb.transform.position = dropPos;
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.freezeRotation = false;
                }


                Carryable carryable = obj.GetComponent<Carryable>();
                if (carryable != null)
                    carryable.carrying = false;

                carriedObjects.RemoveAt(carriedObjects.Count - 1);
            }
        }
    }
    public void CarryingDrop()
    {
        int count = carriedObjects.Count;
        if (count == 0) { collideCarrying = 0; return; }

        // collideCarrying가 음수/과대일 수 있으니 방어
        int startIndex = Mathf.Clamp(collideCarrying, 0, count);

        // 위에서부터(리스트 끝) collideCarrying 인덱스까지 제거
        for (int i = count - 1; i >= startIndex; --i)
        {
            var go = carriedObjects[i];
            if (!go) { carriedObjects.RemoveAt(i); continue; }

            if (go.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.freezeRotation = false;
            }
                

            if (go.TryGetComponent<Carryable>(out var car))
                car.carrying = false;

            carriedObjects.RemoveAt(i);
        }

        // 남은 개수에 맞춰 정리
        collideCarrying = carriedObjects.Count;
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
