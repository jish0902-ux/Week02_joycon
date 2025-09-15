using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.U2D.IK;

public class Controller2D : RaycastController
{

    public float maxSlopeAngle = 80;

    public CollisionInfo collisions;
    [HideInInspector]
    public Vector2 playerInput;

    public override void Start()
    {
        base.Start();
        collisions.faceDir = 1;

    }

    public void Move(Vector2 moveAmount, bool standingOnPlatform)
    {
        Move(moveAmount, Vector2.zero, standingOnPlatform);
    }

    public void Move(Vector2 moveAmount, Vector2 input, bool standingOnPlatform = false)
    {

        //Debug.Log($"move {moveAmount},input {input},  ");

        UpdateRaycastOrigins();

        collisions.Reset();
        collisions.moveAmountOld = moveAmount;
        playerInput = input;

        if (moveAmount.y < 0)
        {
            DescendSlope(ref moveAmount);
        }

        if (moveAmount.x != 0)
        {
            collisions.faceDir = (int)Mathf.Sign(moveAmount.x);
        }

        HorizontalCollisions(ref moveAmount);
        if (moveAmount.y != 0)
        {
            VerticalCollisions(ref moveAmount);
        }

        transform.Translate(moveAmount);

        if (standingOnPlatform)
        {
            collisions.below = true;
        }
    }


    void HorizontalCollisions(ref Vector2 moveAmount)
    {
        float directionX = collisions.faceDir;
        float rayLength = Mathf.Abs(moveAmount.x) + skinWidth;
        if (Mathf.Abs(moveAmount.x) < skinWidth) rayLength = 2 * skinWidth;

        bool ignoreThrough = IsDroppingThrough() || moveAmount.y < 0f; // ↓중엔 가로도 무시

        for (int i = 0; i < horizontalRayCount; i++)
        {
            Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight;
            rayOrigin += Vector2.up * (horizontalRaySpacing * i);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

            Debug.DrawRay(rayOrigin, Vector2.right * directionX, Color.red);

            if (!hit) continue;

            if (ignoreThrough && hit.collider.CompareTag("Through")) continue;

            if (hit.distance == 0)
            {
                moveAmount.x = 0f;
                collisions.left = directionX == -1;
                collisions.right = directionX == 1;
                continue;
            }

            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

            const float kMinClimbDeg = 1.0f;
            if (i == 0 && slopeAngle >= kMinClimbDeg && slopeAngle <= maxSlopeAngle
                && Mathf.Sign(hit.normal.x) == -directionX)
            {
                if (collisions.descendingSlope)
                {
                    collisions.descendingSlope = false;
                    moveAmount = collisions.moveAmountOld;
                }
                float distanceToSlopeStart = 0;
                if (slopeAngle != collisions.slopeAngleOld)
                {
                    distanceToSlopeStart = hit.distance - skinWidth;
                    moveAmount.x -= distanceToSlopeStart * directionX;
                }
                ClimbSlope(ref moveAmount, slopeAngle, hit.normal);
                moveAmount.x += distanceToSlopeStart * directionX;
            }

            if (!collisions.climbingSlope || slopeAngle > maxSlopeAngle)
            {
                moveAmount.x = (hit.distance - skinWidth) * directionX;
                rayLength = hit.distance;

                if (collisions.climbingSlope)
                {
                    float t = Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad);
                    if (t > 1e-4f)
                    {
                        // 경사 위에서의 상승량 = |X| * tan(theta)
                        float climbY = Mathf.Abs(moveAmount.x) * t;
                        // 위로만 보정(아래로 빨려들지 않도록)
                        if (moveAmount.y < climbY) moveAmount.y = climbY;
                    }
                }
                collisions.left = directionX == -1;
                collisions.right = directionX == 1;
            }
        }
    }


    void VerticalCollisions(ref Vector2 moveAmount)
    {
        float directionY = Mathf.Sign(moveAmount.y);
        float rayLength = Mathf.Abs(moveAmount.y) + skinWidth;

        //Debug.Log($"directionY {directionY},rayLength {rayLength}, moveAmount.y {moveAmount.y}");
        for (int i = 0; i < verticalRayCount; i++)
        {

            Vector2 rayOrigin = (directionY == -1) ? raycastOrigins.bottomLeft : raycastOrigins.topLeft;
            rayOrigin += Vector2.right * (verticalRaySpacing * i + moveAmount.x);

            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up * directionY, rayLength, collisionMask);

            Debug.DrawRay(rayOrigin, Vector2.up * directionY, Color.red);

            if (hit)
            {
                if (hit.collider.tag == "Through")
                {
                    if (directionY == 1 || hit.distance == 0)
                    {
                        continue;
                    }
                    if (collisions.fallingThroughPlatform)
                    {
                        continue;
                    }
                    if (playerInput.y == -1)
                    {
                        Debug.Log("벽 뚤림");
                        collisions.fallingThroughPlatform = true;
                        Invoke("ResetFallingThroughPlatform", .01f);
                        continue;
                    }
                }

                moveAmount.y = (hit.distance - skinWidth) * directionY;    // 바닥에 닿으면 다음 값 -7.019378E-06

                if (moveAmount.y < 0.001f)
                    moveAmount.y = 0;

                rayLength = hit.distance;

                if (collisions.climbingSlope)
                {
                    moveAmount.x = moveAmount.y / Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Sign(moveAmount.x);
                }

                collisions.below = directionY == -1;
                collisions.above = directionY == 1;
            }
        }

        if (collisions.climbingSlope)
        {
            float directionX = Mathf.Sign(moveAmount.x);
            rayLength = Mathf.Abs(moveAmount.x) + skinWidth;
            Vector2 rayOrigin = ((directionX == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight) + Vector2.up * moveAmount.y;
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

            if (hit)
            {
                if (!(IsDroppingThrough() && hit.collider.CompareTag("Through")))
                {
                    float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                    if (slopeAngle != collisions.slopeAngle)
                    {
                        moveAmount.x = (hit.distance - skinWidth) * directionX;
                        collisions.slopeAngle = slopeAngle;
                        collisions.slopeNormal = hit.normal;
                    }
                }
            }
        }


        if (collisions.below && (moveAmount.x != 0f))
        {
            float dirX = Mathf.Sign(moveAmount.x);
            float len = Mathf.Abs(moveAmount.x) + skinWidth * 2f;
            Vector2 o = (dirX == -1 ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight) + Vector2.up * moveAmount.y;
            var hit = Physics2D.Raycast(o, Vector2.right * dirX, len, collisionMask);
            if (hit)
            {
                if (IsDroppingThrough() && hit.collider.CompareTag("Through"))
                {
                    // 무시
                }
                else
                {
                    float allowed = (hit.distance - skinWidth) * dirX;
                    if ((dirX > 0f && allowed < moveAmount.x) || (dirX < 0f && allowed > moveAmount.x))
                    {
                        moveAmount.x = allowed;
                        collisions.left = dirX < 0f;
                        collisions.right = dirX > 0f;
                    }
                }
            }
        }
    }

    void ClimbSlope(ref Vector2 moveAmount, float slopeAngle, Vector2 slopeNormal)
    {
        float moveDistance = Mathf.Abs(moveAmount.x);
        float climbmoveAmountY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

        if (moveAmount.y <= climbmoveAmountY)
        {
            moveAmount.y = climbmoveAmountY;
            moveAmount.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(moveAmount.x);
            collisions.below = true;
            collisions.climbingSlope = true;
            collisions.slopeAngle = slopeAngle;
            collisions.slopeNormal = slopeNormal;
        }
    }

    void DescendSlope(ref Vector2 moveAmount)
    {
        // ↓ 방향 레이 2개
        RaycastHit2D hitL = Physics2D.Raycast(raycastOrigins.bottomLeft, Vector2.down, Mathf.Abs(moveAmount.y) + skinWidth, collisionMask);
        RaycastHit2D hitR = Physics2D.Raycast(raycastOrigins.bottomRight, Vector2.down, Mathf.Abs(moveAmount.y) + skinWidth, collisionMask);

        // ★ 하단 점프 중엔 Through를 지면으로 보지 않음
        if (IsDroppingThrough())
        {
            if (hitL && hitL.collider.CompareTag("Through")) hitL = default;
            if (hitR && hitR.collider.CompareTag("Through")) hitR = default;
        }

        if (hitL ^ hitR)
        {
            SlideDownMaxSlope(hitL, ref moveAmount);
            SlideDownMaxSlope(hitR, ref moveAmount);
        }

        if (!collisions.slidingDownMaxSlope)
        {
            float directionX = Mathf.Sign(moveAmount.x);
            Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomRight : raycastOrigins.bottomLeft;
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, -Vector2.up, Mathf.Infinity, collisionMask);

            if (hit)
            {
                if (IsDroppingThrough() && hit.collider.CompareTag("Through"))
                    return;

                float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                if (slopeAngle != 0 && slopeAngle <= maxSlopeAngle)
                {
                    if (Mathf.Sign(hit.normal.x) == directionX)
                    {
                        if (hit.distance - skinWidth <= Mathf.Tan(slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(moveAmount.x))
                        {
                            float moveDistance = Mathf.Abs(moveAmount.x);
                            float descendY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;
                            moveAmount.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(moveAmount.x);
                            moveAmount.y -= descendY;

                            collisions.slopeAngle = slopeAngle;
                            collisions.descendingSlope = true;
                            collisions.below = true;
                            collisions.slopeNormal = hit.normal;
                        }
                    }
                }
            }
        }
    }

    void SlideDownMaxSlope(RaycastHit2D hit, ref Vector2 moveAmount)
    {
        if (!hit) return;

        if (IsDroppingThrough() && hit.collider.CompareTag("Through")) return;

        float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
        if (slopeAngle > maxSlopeAngle)
        {
            float overY = Mathf.Abs(moveAmount.y) - hit.distance;
            if (overY < 0f) overY = 0f; // ← 역방향 방지
            float t = Mathf.Tan(slopeAngle * Mathf.Deg2Rad);
            if (t > 1e-4f)
                moveAmount.x = Mathf.Sign(hit.normal.x) * (overY / t);
            collisions.slopeAngle = slopeAngle;
            collisions.slidingDownMaxSlope = true;
            collisions.slopeNormal = hit.normal;
        }
    }
    void ResetFallingThroughPlatform()
    {
        collisions.fallingThroughPlatform = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IsDroppingThrough()
    {
        // 하단 점프을 의도했거나(입력), 이미 통과 중인 상태
        return collisions.fallingThroughPlatform || playerInput.y == -1;
    }

    public struct CollisionInfo
    {
        public bool above, below;
        public bool left, right;

        public bool climbingSlope;
        public bool descendingSlope;
        public bool slidingDownMaxSlope;

        public float slopeAngle, slopeAngleOld;
        public Vector2 slopeNormal;
        public Vector2 moveAmountOld;
        public int faceDir;
        public bool fallingThroughPlatform;

        public void Reset()
        {
            above = below = false;
            left = right = false;
            climbingSlope = false;
            descendingSlope = false;
            slidingDownMaxSlope = false;
            slopeNormal = Vector2.zero;

            slopeAngleOld = slopeAngle;
            slopeAngle = 0;
        }
    }

}
