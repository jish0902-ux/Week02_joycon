using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[RequireComponent(typeof(Controller2D))]
[DisallowMultipleComponent]
public class Player : MonoBehaviour
{
    // ===== Move / Jump =====
    [Header("Jump / Move")]
    public float maxJumpHeight = 4f;
    public float minJumpHeight = 1f;
    public float timeToJumpApex = .4f;
    [SerializeField] float accelerationTimeAirborne = .2f;
    [SerializeField] float accelerationTimeGrounded = .1f;
    [SerializeField] float moveSpeed = 6f;

    public Vector2 wallJumpClimb = new Vector2(7.5f, 16f);
    public Vector2 wallJumpOff = new Vector2(8f, 7f);
    public Vector2 wallLeap = new Vector2(18f, 17f);

    [SerializeField] float wallSlideSpeedMax = 3f;
    [SerializeField] float wallStickTime = .25f;

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

    // ===== Ladder =====
    [Header("Ladder")]
    [SerializeField] LayerMask ladderMask = 0;
    [SerializeField, Range(1f, 12f)] float climbSpeed = 5.0f;
    [SerializeField, Range(0.05f, 1.0f)] float attachProbeHalfWidth = 0.25f;
    [SerializeField, Range(0.6f, 2.0f)] float attachProbeHeight = 1.4f;
    [SerializeField, Range(0.1f, 20f)] float snapSpeed = 12f;
    [SerializeField] bool snapToCenterX = true;
    [SerializeField, Range(0f, 10f)] float detachPush = 3.0f;

    bool onLadder;
    Collider2D _ladderCol;
    float _ladderCenterX;

    // ===== Ladder Jump (NEW) =====
    [Header("Ladder Jump")]
    [SerializeField] bool allowLadderJump = true;
    [SerializeField, Range(0f, 24f)] float ladderJumpUp = 14f;         // ↑ (x 입력 거의 없을 때)
    [SerializeField] Vector2 ladderJumpSide = new Vector2(10f, 12f);   // ↗/↖ (x 입력 있을 때)
    [SerializeField, Range(0f, 0.25f)] float ladderCoyoteTime = 0.08f; // 사다리에서 방금 떨어졌을 때 여유 점프 시간

    [SerializeField, Range(0f, 0.2f)] float jumpBuffer = 0.12f;         // 점프 입력 버퍼
    [SerializeField, Range(0f, 0.25f)] float ladderReattachBlock = 0.12f; // 사다리 점프 직후 재부착 차단
    
    
    float _ladderCoyoteTimer;
    float _jumpBufferTimer;            // 점프 버퍼 타이머
    bool _didJumpThisFrame;           // 이번 프레임에 점프 소비 여부
    float _ladderAttachBlockTimer;     // 재부착 차단 타이머

    // ===== Solid / Contact Resolve =====
    [Header("Solids / Resolve")]
    [SerializeField] LayerMask solidMask = 0; // 벽/바닥/타일맵 레이어만 포함
    [SerializeField, Range(0.001f, 0.01f)] float cornerEpsilon = 0.004f;
    [SerializeField, Range(0f, 0.1f)] float wallLockAfterLand = 0.03f;


    [Header("Visuals")]
    [SerializeField] private SpriteRenderer playerSprite;

    float wallLockTimer;
    bool wasGrounded;




    // ===== Buffers & Filters (NoAlloc) =====
    static readonly Collider2D[] sHits = new Collider2D[8];      // ladder probe
    static readonly Collider2D[] _overlapHits = new Collider2D[8]; // self overlap resolve
    static readonly Collider2D[] _probeHits = new Collider2D[8];   // corner probe

    private ContactFilter2D _solidFilter;
    private ContactFilter2D _ladderFilter;   // (NEW) ladder 전용 필터
    private Collider2D _selfCol;

#if UNITY_EDITOR
    // Debug gizmo
    bool _drawAttachGizmo = true;
#endif

    /// <summary>
    /// 컴포넌트/필드 캐시 및 충돌 필터 초기화.
    /// </summary>
    void Awake()
    {
        controller = GetComponent<Controller2D>();
        _selfCol = GetComponent<Collider2D>();

        // Solid 필터
        _solidFilter.useTriggers = false;
        _solidFilter.SetLayerMask(solidMask);
        _solidFilter.useDepth = false;

        // Ladder 필터 (사다리가 보통 Trigger인 경우가 많음)
        _ladderFilter.useTriggers = true;          // 사다리가 Trigger가 아니면 false로 변경
        _ladderFilter.SetLayerMask(ladderMask);
        _ladderFilter.useDepth = false;
    }

    /// <summary>
    /// 점프/중력 관련 파생 값(중력, 최대/최소 점프 속도) 선계산.
    /// </summary>
    void Start()
    {
        gravity = -(2f * maxJumpHeight) / Mathf.Pow(timeToJumpApex, 2f);
        maxJumpVelocity = Mathf.Abs(gravity) * timeToJumpApex;
        minJumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * minJumpHeight);
    }

    /// <summary>
    /// 매 프레임 이동/충돌/사다리 처리. 점프 입력은 최우선으로 선처리하여
    /// '위 + 오른쪽 + 점프' 동시 입력 시에도 점프가 이기도록 한다.
    /// </summary>
    void Update()
    {
        float dt = Time.deltaTime;

        // 타이머 감소
        if (_ladderAttachBlockTimer > 0f) _ladderAttachBlockTimer = Mathf.Max(0f, _ladderAttachBlockTimer - dt);
        if (_ladderCoyoteTimer > 0f) _ladderCoyoteTimer = Mathf.Max(0f, _ladderCoyoteTimer - dt);
        if (_jumpBufferTimer > 0f) _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - dt);

        if(playerSprite != null && directionalInput.x != 0)
        {
            playerSprite.flipX = directionalInput.x < 0;
        }


        // 기본 속도(중력/수평 스무딩)
        CalculateVelocityBase(dt);

        // 점프 선처리(최우선) → 이번 프레임에 점프 소비 여부 기록
        _didJumpThisFrame = TryConsumeJump();

        if (!onLadder)
            HandleWallSliding();

        // 이동 벡터 계산
        Vector2 move = velocity * dt;

        if (onLadder)
        {
            // 점프를 이미 소비했다면(=사다리에서 뛰어내림) 사다리 모션 스킵
            if (!_didJumpThisFrame)
            {
                ApplyLadderMotion(ref move, dt);
            }

            controller.Move(move, directionalInput);

            if (controller.collisions.above && velocity.y > 0f) velocity.y = 0f;
            if (controller.collisions.below && velocity.y < 0f) velocity.y = 0f;

            CornerLockOnLanding(dt);

            // 재부착 차단 중이거나 사다리 밖이면 분리
            if (!StillOnSameLadder())
                DetachFromLadder();
        }
        else
        {
            // 점프 직후 같은 프레임에 사다리 재부착되는 문제 방지
            if (!_didJumpThisFrame)
                TryAttachLadder();

            controller.Move(move, directionalInput);
            CornerLockOnLanding(dt);

            if (controller.collisions.above || controller.collisions.below)
            {
                if (controller.collisions.slidingDownMaxSlope)
                    velocity.y += controller.collisions.slopeNormal.y * -gravity * dt;
                else
                    velocity.y = 0;
            }
        }
    }

    /// <summary>
    /// 외부 입력 시스템으로부터 이동 입력을 설정.
    /// </summary>
    /// <param name="input">수평/수직(-1~1) 입력 벡터</param>
    public void SetDirectionalInput(Vector2 input) => directionalInput = input;

    /// <summary>
    /// 점프 버튼 Down. 즉시 점프하지 않고 버퍼만 채워
    /// Update()에서 최우선 소비되도록 한다.
    /// </summary>
    public void OnJumpInputDown()
    {
        _jumpBufferTimer = jumpBuffer; // 버퍼 리필(중복 입력에도 최신화)
    }

    /// <summary>
    /// 점프 버퍼를 확인하고 가능한 경우 즉시 점프를 수행(소비)한다.
    /// 우선순위: 사다리 점프 > 사다리 코요테 > 벽 점프 > 지상 점프.
    /// </summary>
    /// <returns>이번 프레임에 점프를 수행했으면 true</returns>
    bool TryConsumeJump()
    {
        if (_jumpBufferTimer <= 0f) return false;

        // 1) 사다리 점프 우선
        if (onLadder && allowLadderJump)
        {
            Debug.Log("사다리 점프");

            _jumpBufferTimer = 0f;
            LadderJump();                // Detach + 속도 적용 + 재부착 차단
            return true;
        }

        // 2) 사다리 코요테 점프(방금 떨어졌을 때)
        if (!onLadder && _ladderCoyoteTimer > 0f && allowLadderJump)
        {
            Debug.Log("사다리 코요테 점프");

            _jumpBufferTimer = 0f;
            _ladderCoyoteTimer = 0f;
            velocity.y = Mathf.Max(velocity.y, ladderJumpUp);
            return true;
        }

        // 3) 벽 점프
        if (wallSliding)
        {
            Debug.Log("벽 점프");
            _jumpBufferTimer = 0f;

            if (wallDirX == Mathf.RoundToInt(directionalInput.x))
            {
                velocity.x = -wallDirX * wallJumpClimb.x;
                velocity.y = wallJumpClimb.y;
            }
            else if (Mathf.Abs(directionalInput.x) < 0.001f)
            {
                velocity.x = -wallDirX * wallJumpOff.x;
                velocity.y = wallJumpOff.y;
            }
            else
            {
                velocity.x = -wallDirX * wallLeap.x;
                velocity.y = wallLeap.y;
            }
            return true;
        }

        // 4) 지상 점프
        if (controller.collisions.below)
        {
            //Debug.Log("지상 점프");

            _jumpBufferTimer = 0f;

            if (controller.collisions.slidingDownMaxSlope)
            {
                if (Mathf.RoundToInt(directionalInput.x) != -Mathf.Sign(controller.collisions.slopeNormal.x))
                {
                    velocity.y = maxJumpVelocity * controller.collisions.slopeNormal.y;
                    velocity.x = maxJumpVelocity * controller.collisions.slopeNormal.x;
                }
            }
            else
            {
                velocity.y = maxJumpVelocity;
            }
            return true;
        }

        // 공중이지만 지금은 점프할 수 없음 → 버퍼 유지(코요테/착지 직후를 위해)
        return false;
    }

    /// <summary>
    /// 사다리에 붙은 상태에서의 점프.
    /// 좌/우 입력 시 측면 점프, 아니면 위 점프.
    /// 점프 직후 일정 시간 사다리 재부착을 차단한다.
    /// </summary>
    void LadderJump()
    {
        DetachFromLadder();

        float ax = directionalInput.x;
        bool side = Mathf.Abs(ax) > 0.25f;

        if (side)
        {
            velocity.x = Mathf.Sign(ax) * ladderJumpSide.x;
            velocity.y = ladderJumpSide.y;
        }
        else
        {
            velocity.x = 0f;
            velocity.y = ladderJumpUp;
        }

        wallLockTimer = 0f;
        _ladderAttachBlockTimer = ladderReattachBlock; // 재부착 차단
    }

    /// <summary>
    /// 점프 키 업 처리. 최소 점프 높이를 위해 상승 속도를 클램프.
    /// 사다리에 붙어있는 동안은 적용하지 않음.
    /// </summary>
    public void OnJumpInputUp()
    {
        if (onLadder) return;                // 사다리 점프 전(부착 상태)는 무시
        if (velocity.y > minJumpVelocity)    // 공중/지상 점프에서만 적용
            velocity.y = minJumpVelocity;
    }

    // ===== Core Movement =====

    /// <summary>
    /// 기본 이동 속도 계산(수평 스무딩, 중력 적용).
    /// </summary>
    /// <param name="dt">델타타임</param>
    void CalculateVelocityBase(float dt)
    {
        float targetVelocityX = directionalInput.x * moveSpeed;
        velocity.x = Mathf.SmoothDamp(
            velocity.x, targetVelocityX, ref velocityXSmoothing,
            (controller.collisions.below) ? accelerationTimeGrounded : accelerationTimeAirborne);

        if (!onLadder)
            velocity.y += gravity * dt;
    }

    /// <summary>
    /// 벽 슬라이딩 상태 검사 및 속도/스틱 타이머 처리.
    /// </summary>
    void HandleWallSliding()
    {
        wallDirX = (controller.collisions.left) ? -1 : 1;
        wallSliding = false;

        if ((controller.collisions.left || controller.collisions.right) &&
            !controller.collisions.below && velocity.y < 0f)
        {
            wallSliding = true;

            if (velocity.y < -wallSlideSpeedMax)
                velocity.y = -wallSlideSpeedMax;

            if (timeToWallUnstick > 0f)
            {
                velocityXSmoothing = 0f;
                velocity.x = 0f;

                if (Mathf.Abs(directionalInput.x) > 0.001f &&
                    Mathf.RoundToInt(directionalInput.x) != wallDirX)
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

    // ===== Ladder Logic =====

    /// <summary>
    /// 상/하 입력 시 주변 사다리를 탐지해 부착 시도.
    /// 점프 직후에는 재부착 차단 타이머가 켜져 있으면 즉시 리턴.
    /// </summary>
    void TryAttachLadder()
    {
        if (_ladderAttachBlockTimer > 0f) return;     // 점프 우선
        if (directionalInput.y <= 0.1f) return;

        Vector2 center = transform.position;
        Vector2 size = new Vector2(attachProbeHalfWidth * 2f, attachProbeHeight);

        int hitCount = Physics2D.OverlapBox(center, size, 0f, _ladderFilter, sHits);
        if (hitCount <= 0) return;

        int bestIdx = -1;
        float bestDx = float.MaxValue;
        for (int i = 0; i < hitCount; ++i)
        {
            var c = sHits[i]; if (!c) continue;
            float ladderX = c.bounds.center.x;
            float dx = Mathf.Abs(ladderX - center.x);
            if (dx < bestDx) { bestDx = dx; bestIdx = i; }
        }
        if (bestIdx < 0) return;

        AttachToLadder(sHits[bestIdx]);
    }

    /// <summary>
    /// 특정 사다리에 부착하고 수평/수직 관성을 제거.
    /// </summary>
    /// <param name="ladder">부착할 사다리 콜라이더</param>
    void AttachToLadder(Collider2D ladder)
    {
        onLadder = true;
        _ladderCol = ladder;
        _ladderCenterX = ladder.bounds.center.x;
        velocity = Vector3.zero; // 관성 제거
        _ladderCoyoteTimer = 0f; // 부착중에는 코요테 비활성
    }

    /// <summary>
    /// 사다리에서 분리(상태/참조 초기화). 코요테 타이머 시작.
    /// </summary>
    void DetachFromLadder()
    {
        onLadder = false;
        _ladderCol = null;
        _ladderCoyoteTimer = ladderCoyoteTime; // 분리 직후 잠깐 점프 허용
    }

    /// <summary>
    /// 현재 부착한 사다리의 AABB 내부에 여전히 존재하는지 확인.
    /// </summary>
    /// <returns>같은 사다리 위면 true</returns>
    bool StillOnSameLadder()
    {
        if (!_ladderCol) return false;
        Vector2 center = transform.position;
        Bounds b = _ladderCol.bounds;
        return b.min.x <= center.x && center.x <= b.max.x &&
               b.min.y <= center.y && center.y <= b.max.y;
    }

    /// <summary>
    /// 사다리 상태에서의 수직 이동/수평 스냅/탈출 로직을 적용.
    /// </summary>
    /// <param name="move">이번 프레임 이동 벡터(참조 수정)</param>
    /// <param name="dt">델타타임</param>
    void ApplyLadderMotion(ref Vector2 move, float dt)
    {
        // 1) 수직 이동
        float vy = directionalInput.y * climbSpeed;
        move.y = vy * dt;
        velocity.y = vy;

        // 2) 수평 스냅
        if (snapToCenterX)
        {
            float x = transform.position.x;
            float newX = Mathf.MoveTowards(x, _ladderCenterX, snapSpeed * dt);
            float dx = Mathf.Clamp(newX - x, -0.2f, 0.2f); // 과도한 밀기 방지
            move.x = dx;
            velocity.x = (dt > 1e-6f) ? dx / dt : 0f;
        }
        else
        {
            move.x = 0f;
            velocity.x = 0f;
        }

        // 3) 좌/우 입력으로 탈출
        float ax = directionalInput.x;
        if (Mathf.Abs(ax) > 0.25f && Mathf.Abs(directionalInput.y) < 0.3f)
        {
            DetachFromLadder();
            velocity.x = Mathf.Sign(ax) * detachPush;
        }

        // 4) 하단 강탈출(원하면 즉시 Detach)
        if (directionalInput.y < -0.85f)
        {
            // 필요 시: DetachFromLadder();
        }
    }

   

    // ===== Resolve / Corner Guards =====

    /// <summary>
    /// 벽/바닥과의 겹침 해소 및 속도 정규화(관통/진동 방지).
    /// </summary>
    void ResolveContactsAndClampVelocity()
    {
        var col = controller.collisions;

        // 수평 충돌 시 즉시 차단
        if (col.left && velocity.x < 0f) { velocity.x = 0f; velocityXSmoothing = 0f; }
        if (col.right && velocity.x > 0f) { velocity.x = 0f; velocityXSmoothing = 0f; }

        if (!_selfCol) return;

        // Unity 6: OverlapCollider → Overlap(contactFilter, results)
        int n = _selfCol.Overlap(_solidFilter, _overlapHits);
        for (int i = 0; i < n; ++i)
        {
            var other = _overlapHits[i];
            if (!other) continue;

            var d = _selfCol.Distance(other); // isOverlapped, distance, normal
            if (!d.isOverlapped) continue;

            Vector2 pushOut = d.normal * (-d.distance + 0.001f);
            transform.Translate(pushOut, Space.World);

            float vn = Vector2.Dot((Vector2)velocity, d.normal);
            if (vn > 0f) velocity -= (Vector3)(d.normal * vn);
        }
    }

    /// <summary>
    /// 착지-코너 상황에서 잠시 수평 이동을 락하고 벽으로부터 미세 분리.
    /// </summary>
    /// <param name="dt">델타타임</param>
    void CornerLockOnLanding(float dt)
    {
        var col = controller.collisions;
        bool grounded = col.below;
        bool walling = col.left || col.right;

        // 공중 -> 착지 프레임 + 벽 동시 접촉이면 잠깐 수평락 + 미세분리
        if (grounded && !wasGrounded && walling)
        {
            wallLockTimer = wallLockAfterLand;
            ZeroX();
            MicroSeparateFromWall();
        }
        else if (grounded && wallLockTimer > 0f && walling)
        {
            wallLockTimer -= dt;
            ZeroX();
            MicroSeparateFromWall();
        }

        wasGrounded = grounded;
    }

    /// <summary>
    /// 수평 속도 및 스무딩 계수를 0으로 초기화.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ZeroX()
    {
        velocity.x = 0f;
        velocityXSmoothing = 0f;
    }

    /// <summary>
    /// 코너 근접 시 경미한 겹침/흡착을 방지하기 위해 소량 밀어내기.
    /// </summary>
    void MicroSeparateFromWall()
    {
        if (!_selfCol) return;

        var b = _selfCol.bounds;
        Vector2 center = b.center;
        Vector2 size = new Vector2(b.size.x + cornerEpsilon * 2f, b.size.y + cornerEpsilon * 2f);

        int n = Physics2D.OverlapBox(center, size, 0f, _solidFilter, _probeHits);
        for (int i = 0; i < n; ++i)
        {
            var other = _probeHits[i];
            if (!other) continue;

            var d = _selfCol.Distance(other);
            if (d.distance <= cornerEpsilon)
            {
                Vector2 push = d.normal * (cornerEpsilon - d.distance + 0.0005f);
                transform.Translate(push, Space.World);

                float vn = Vector2.Dot((Vector2)velocity, d.normal);
                if (vn > 0f) velocity -= (Vector3)(d.normal * vn);
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 선택 시 사다리 부착 탐색 박스를 기즈모로 시각화.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (!_drawAttachGizmo) return;
        Gizmos.color = onLadder ? Color.green : Color.yellow;
        Vector2 center = transform.position;
        Vector3 size = new Vector3(attachProbeHalfWidth * 2f, attachProbeHeight, 0.1f);
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
