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

    // ===== Solid / Contact Resolve =====
    [Header("Solids / Resolve")]
    [SerializeField] LayerMask solidMask = 0; // 벽/바닥/타일맵 레이어만 포함
    [SerializeField, Range(0.001f, 0.01f)] float cornerEpsilon = 0.004f;
    [SerializeField, Range(0f, 0.1f)] float wallLockAfterLand = 0.03f;

    float wallLockTimer;
    bool wasGrounded;

    // ===== Buffers & Filters (NoAlloc) =====
    static readonly Collider2D[] sHits = new Collider2D[8]; // ladder probe
    static readonly Collider2D[] _overlapHits = new Collider2D[8]; // self overlap resolve
    static readonly Collider2D[] _probeHits = new Collider2D[8]; // corner probe

    private ContactFilter2D _solidFilter;
    private Collider2D _selfCol;

#if UNITY_EDITOR
    // Debug gizmo
    bool _drawAttachGizmo = true;
#endif

    void Awake()
    {
        controller = GetComponent<Controller2D>();
        _selfCol = GetComponent<Collider2D>();

        _solidFilter.useTriggers = false;
        _solidFilter.SetLayerMask(solidMask); // useLayerMask = true 로 설정됨
        _solidFilter.useDepth = false;
    }

    void Start()
    {
        gravity = -(2f * maxJumpHeight) / Mathf.Pow(timeToJumpApex, 2f);
        maxJumpVelocity = Mathf.Abs(gravity) * timeToJumpApex;
        minJumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * minJumpHeight);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        CalculateVelocityBase(dt);

        if (!onLadder)
            HandleWallSliding();

        Vector2 move = velocity * dt;

        if (onLadder)
        {
            ApplyLadderMotion(ref move, dt);
            controller.Move(move, directionalInput);

            // 사다리 중 충돌시 수직속도 정리(관통 방지)
            if (controller.collisions.above && velocity.y > 0f) velocity.y = 0f;
            if (controller.collisions.below && velocity.y < 0f) velocity.y = 0f;

            //ResolveContactsAndClampVelocity();
            CornerLockOnLanding(dt);

            if (!StillOnSameLadder())
                DetachFromLadder();
        }
        else
        {
            TryAttachLadder();

            controller.Move(move, directionalInput);
            //ResolveContactsAndClampVelocity();
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

    // ===== Public Inputs =====
    public void SetDirectionalInput(Vector2 input) => directionalInput = input;

    public void OnJumpInputDown()
    {
        if (onLadder)
        {
            DetachFromLadder();
            velocity.y = maxJumpVelocity * 0.8f;
            return;
        }

        if (wallSliding)
        {
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
        }

        if (controller.collisions.below)
        {
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
        }
    }

    public void OnJumpInputUp()
    {
        if (onLadder) return;
        if (velocity.y > minJumpVelocity) velocity.y = minJumpVelocity;
    }

    // ===== Core Movement =====
    void CalculateVelocityBase(float dt)
    {
        float targetVelocityX = directionalInput.x * moveSpeed;
        velocity.x = Mathf.SmoothDamp(
            velocity.x, targetVelocityX, ref velocityXSmoothing,
            (controller.collisions.below) ? accelerationTimeGrounded : accelerationTimeAirborne);

        if (!onLadder)
            velocity.y += gravity * dt;
    }

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
    void TryAttachLadder()
    {
        if (directionalInput.y <= 0.1f) return;

        Vector2 center = transform.position;
        Vector2 size = new Vector2(attachProbeHalfWidth * 2f, attachProbeHeight);

        int hitCount = Physics2D.OverlapBoxNonAlloc(center, size, 0f, sHits, ladderMask);
        if (hitCount <= 0) return;

        int bestIdx = -1;
        float bestDx = float.MaxValue;
        for (int i = 0; i < hitCount; ++i)
        {
            var c = sHits[i]; if (!c) continue;
            float ladderX = c.bounds.center.x; // 정확도가 더 중요하면 bounds 사용
            float dx = Mathf.Abs(ladderX - center.x);
            if (dx < bestDx) { bestDx = dx; bestIdx = i; }
        }
        if (bestIdx < 0) return;

        AttachToLadder(sHits[bestIdx]);
    }

    void AttachToLadder(Collider2D ladder)
    {
        onLadder = true;
        _ladderCol = ladder;
        _ladderCenterX = ladder.bounds.center.x;
        velocity = Vector3.zero; // 관성 제거
    }

    void DetachFromLadder()
    {
        onLadder = false;
        _ladderCol = null;
    }

    bool StillOnSameLadder()
    {
        if (!_ladderCol) return false;
        Vector2 center = transform.position;
        Bounds b = _ladderCol.bounds;
        return b.min.x <= center.x && center.x <= b.max.x &&
               b.min.y <= center.y && center.y <= b.max.y;
    }

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
            // 선택: DetachFromLadder();
        }
    }

    // ===== Resolve / Corner Guards =====
    void ResolveContactsAndClampVelocity()
    {
        var col = controller.collisions;

        // 수평 충돌 시 즉시 차단
        if (col.left && velocity.x < 0f) { velocity.x = 0f; velocityXSmoothing = 0f; }
        if (col.right && velocity.x > 0f) { velocity.x = 0f; velocityXSmoothing = 0f; }

        if (!_selfCol) return;

        // Unity 6: OverlapCollider -> Overlap(contactFilter, results)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ZeroX()
    {
        velocity.x = 0f;
        velocityXSmoothing = 0f;
    }

    // 겹치지 않았어도 코너 근접 시 살짝 밀어내기
    void MicroSeparateFromWall()
    {
        if (!_selfCol) return;

        var b = _selfCol.bounds;
        Vector2 center = b.center;
        Vector2 size = new Vector2(b.size.x + cornerEpsilon * 2f, b.size.y + cornerEpsilon * 2f);

        int n = Physics2D.OverlapBoxNonAlloc(center, size, 0f, _probeHits, solidMask);
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
