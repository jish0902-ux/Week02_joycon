using System;
using UnityEngine;

[RequireComponent(typeof(Controller2D))]
public class Player : MonoBehaviour
{
    [Header("Jump / Move")]
    public float maxJumpHeight = 4;
    public float minJumpHeight = 1;
    public float timeToJumpApex = .4f;
    float accelerationTimeAirborne = .2f;
    float accelerationTimeGrounded = .1f;
    float moveSpeed = 6;

    public Vector2 wallJumpClimb;
    public Vector2 wallJumpOff;
    public Vector2 wallLeap;

    public float wallSlideSpeedMax = 3;
    public float wallStickTime = .25f;
    float timeToWallUnstick;

    float gravity;
    float maxJumpVelocity;
    float minJumpVelocity;
    Vector3 velocity;
    float velocityXSmoothing;

    Controller2D controller;

    Vector2 directionalInput;
    bool wallSliding;
    int wallDirX;

    // ---------- Ladder ----------
    [Header("Ladder")]
    [SerializeField] LayerMask ladderMask = 0;           // 사다리 레이어 마스크
    [SerializeField, Range(1f, 12f)] float climbSpeed = 5.0f;
    [SerializeField, Range(0.05f, 1.0f)] float attachProbeHalfWidth = 0.25f; // 허리 폭
    [SerializeField, Range(0.6f, 2.0f)] float attachProbeHeight = 1.4f;      // 허리 높이
    [SerializeField, Range(0.1f, 20f)] float snapSpeed = 12f; // 사다리 중앙 X로 정렬 속도
    [SerializeField] bool snapToCenterX = true;           // X 중앙 정렬 여부
    [SerializeField, Range(0f, 10f)] float detachPush = 3.0f; // 좌/우로 이탈 시 수평 임펄스

    bool onLadder;
    Collider2D _ladderCol;        // 현재 붙은 사다리
    float _ladderCenterX;         // 중앙 X 캐시

    static readonly Collider2D[] sHits = new Collider2D[4]; // NonAlloc 버퍼

    // ----------------------------

    void Start()
    {
        controller = GetComponent<Controller2D>();

        gravity = -(2 * maxJumpHeight) / Mathf.Pow(timeToJumpApex, 2);
        maxJumpVelocity = Mathf.Abs(gravity) * timeToJumpApex;
        minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(gravity) * minJumpHeight);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 기본 속도 계산(중력/수평 가감속)
        CalculateVelocityBase(dt);

        // 벽타기(사다리 중에는 무시)
        if (!onLadder)
            HandleWallSliding();

        // --- 이동량 예측 ---
        Vector2 move = velocity * dt;

        // --- 사다리 입력/상태 처리 ---
        if (onLadder)
        {
            ApplyLadderMotion(ref move, dt);
            // Controller2D 이동
            controller.Move(move, directionalInput);

            // 사다리 도중엔 충돌로 y=0 보정하지 않는다(계단 같은 충돌 무시)
            // 상/하로 사다리 범위를 벗어나면 자동 해제
            if (!StillOnSameLadder())
                DetachFromLadder();
        }
        else
        {
            // 사다리에 붙을지 시도(↑ 입력 + 사다리 감지)
            TryAttachLadder();

            // 일반 이동
            controller.Move(move, directionalInput);

            // 일반 충돌 보정
            if (controller.collisions.above || controller.collisions.below)
            {
                if (controller.collisions.slidingDownMaxSlope)
                    velocity.y += controller.collisions.slopeNormal.y * -gravity * dt;
                else
                    velocity.y = 0;
            }
        }
    }

    // ==== Public Inputs ====

    public void SetDirectionalInput(Vector2 input)
    {
        directionalInput = input;
    }

    public void OnJumpInputDown()
    {
        if (onLadder)
        {
            // 사다리에서 점프 → 위로 살짝 팝 + 해제
            DetachFromLadder();
            velocity.y = maxJumpVelocity * 0.8f;
            return;
        }

        if (wallSliding)
        {
            if (wallDirX == directionalInput.x)
            {
                velocity.x = -wallDirX * wallJumpClimb.x;
                velocity.y = wallJumpClimb.y;
            }
            else if (directionalInput.x == 0)
            {
                velocity.x = -wallDirX * wallJumpOff.x;
                velocity.y = wallJumpOff.y;
            }
            else
            {
                velocity.x = -wallDirX * wallLeap.x;
                velocity.y = wallLeap.y;
            }
        }

        if (controller.collisions.below)
        {
            if (controller.collisions.slidingDownMaxSlope)
            {
                if (directionalInput.x != -Mathf.Sign(controller.collisions.slopeNormal.x))
                {
                    velocity.y = maxJumpVelocity * controller.collisions.slopeNormal.y;
                    velocity.x = maxJumpVelocity * controller.collisions.slopeNormal.x;
                }
            }
            else
            {
                velocity.y = maxJumpVelocity;
            }
        }
    }

    public void OnJumpInputUp()
    {
        if (onLadder) return; // 사다리 중엔 최소점프 로직 생략

        if (velocity.y > minJumpVelocity)
            velocity.y = minJumpVelocity;
    }

    // ==== Core Movement ====

    void CalculateVelocityBase(float dt)
    {
        // 수평 가감속은 항상 적용(사다리 중엔 거의 0에 수렴)
        float targetVelocityX = directionalInput.x * moveSpeed;
        velocity.x = Mathf.SmoothDamp(
            velocity.x, targetVelocityX, ref velocityXSmoothing,
            (controller.collisions.below) ? accelerationTimeGrounded : accelerationTimeAirborne);

        // 중력: 사다리 중엔 적용 X
        if (!onLadder)
            velocity.y += gravity * dt;
    }

    void HandleWallSliding()
    {
        wallDirX = (controller.collisions.left) ? -1 : 1;
        wallSliding = false;
        if ((controller.collisions.left || controller.collisions.right) &&
            !controller.collisions.below && velocity.y < 0)
        {
            wallSliding = true;

            if (velocity.y < -wallSlideSpeedMax)
                velocity.y = -wallSlideSpeedMax;

            if (timeToWallUnstick > 0)
            {
                velocityXSmoothing = 0;
                velocity.x = 0;

                if (directionalInput.x != wallDirX && directionalInput.x != 0)
                    timeToWallUnstick -= Time.deltaTime;
                else
                    timeToWallUnstick = wallStickTime;
            }
            else
            {
                timeToWallUnstick = wallStickTime;
            }
        }
    }

    // ==== Ladder Logic ====

    void TryAttachLadder()
    {
        // Up 입력일 때만 붙기 시도(원하면 조건 제거 가능)
        if (directionalInput.y <= 0.1f) return;

        // 플레이어 허리 중심에서 박스 오버랩
        Vector2 center = transform.position;
        Vector2 half = new Vector2(attachProbeHalfWidth, attachProbeHeight * 0.5f);

        int hitCount = Physics2D.OverlapBoxNonAlloc(center, half * 2f, 0f, sHits, ladderMask);
        if (hitCount == 0) return;

        // 가장 가까운 X 중심을 가진 사다리 선택
        Collider2D best = null;
        float bestDx = float.MaxValue;
        for (int i = 0; i < hitCount; ++i)
        {
            var c = sHits[i];
            if (!c) continue;
            float dx = Mathf.Abs(c.bounds.center.x - center.x);
            if (dx < bestDx) { bestDx = dx; best = c; }
        }
        if (!best) return;

        AttachToLadder(best);
    }

    void AttachToLadder(Collider2D ladder)
    {
        onLadder = true;
        _ladderCol = ladder;
        _ladderCenterX = ladder.bounds.center.x;

        // 사다리 진입 시 관성 제거(수직/수평)
        velocity = Vector3.zero;
    }

    void DetachFromLadder()
    {
        onLadder = false;
        _ladderCol = null;
    }

    bool StillOnSameLadder()
    {
        if (!_ladderCol) return false;

        // 현재 위치가 여전히 이 사다리 콜라이더와 겹치는가?
        Vector2 center = transform.position;
        Bounds b = _ladderCol.bounds;
        return b.min.x <= center.x && center.x <= b.max.x &&
               b.min.y <= center.y && center.y <= b.max.y;
    }

    void ApplyLadderMotion(ref Vector2 move, float dt)
    {
        // 1) 수직 이동: 위/아래 입력으로 등/하강
        float vy = directionalInput.y * climbSpeed;
        move.y = vy * dt;
        velocity.y = vy; // 관성 동기화

        // 2) 수평 스냅: 중앙 X로 정렬(사다리 느낌 강화)
        if (snapToCenterX)
        {
            float x = transform.position.x;
            float newX = Mathf.MoveTowards(x, _ladderCenterX, snapSpeed * dt);
            move.x = (newX - x);
            velocity.x = (dt > 1e-6f) ? move.x / dt : 0f;
        }
        else
        {
            // 스냅을 끄면 수평 속도는 0에 가깝게 유지
            move.x = 0f;
            velocity.x = 0f;
        }

        // 3) 좌/우 입력으로 탈출(사다리에서 옆으로 뛰어내리기)
        float ax = directionalInput.x;
        if (Mathf.Abs(ax) > 0.25f && Mathf.Abs(directionalInput.y) < 0.3f)
        {
            DetachFromLadder();
            velocity.x = Mathf.Sign(ax) * detachPush;
            // 수직 속도는 현재값 유지(원하면 살짝 하강/상승을 줄 수도 있음)
        }

        // 4) 하단 탈출: 아래로 강하게 누르면 빠져나오기
        if (directionalInput.y < -0.85f)
        {
            // 사다리 바깥으로 벗어났다면 자동 해제(StillOnSameLadder가 다음 프레임에 false)
            // 즉시 떨어지고 싶다면 아래처럼 바로 해제:
            // DetachFromLadder();
        }
    }

    // ---- Debug Gizmos: 사다리 탐색 박스 확인용 ----
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = onLadder ? Color.green : Color.yellow;
        Vector2 center = transform.position;
        Vector3 size = new Vector3(attachProbeHalfWidth * 2f, attachProbeHeight, 0.1f);
        Gizmos.DrawWireCube(center, size);
    }
#endif
}