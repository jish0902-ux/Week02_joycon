using UnityEngine;

/// <summary>
/// 이동은 외부 스크립트가 관리하고, 본 컴포넌트는 "로프 상태 + 제약"만 담당.
/// - 좌클릭: 그랩 사격(마우스 조준)
/// - 우클릭: 해제
/// - 휠: 로프 길이 조절(릴/언릴)
/// - 외부에서 Move 호출 전: ConstrainMoveYOnly(ref move)로 Y축만 수정
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Controller2D))]
public sealed class KinematicGrapple2D : MonoBehaviour
{
    #region Inspector
    [Header("Refs")]
    [SerializeField] Controller2D controller;
    [SerializeField] Transform gunTip;        // 레이 원점(없으면 transform)
    [SerializeField] Camera cam;              // 조준 카메라(없으면 Camera.main)
    [SerializeField] LineRenderer lr;         // 선택: 로프 표시

    [Header("Grapple")]
    [SerializeField] LayerMask grappleMask = ~0;
    [SerializeField, Range(1f, 50f)] float maxGrappleDistance = 20f;
    [SerializeField, Range(0.5f, 40f)] float ropeMin = 1.2f;
    [SerializeField, Range(1f, 60f)] float ropeMax = 25f;
    [SerializeField, Range(0.5f, 12f)] float reelSpeed = 6f;     // 휠 민감도

    [Header("Constraint")]
    [SerializeField, Range(0f, 1f)] float tensionDamp = 0.2f;    // 원방향 팽창 감쇠(예: 외부 move.y가 과도할 때)
    [SerializeField, Range(5f, 60f)] float maxFallSpeed = 30f;   // 하강 속도 제한(선택적 Clamp)
    [SerializeField, Range(0f, 0.02f)] float skin = 0.001f;      // 반경 여유값

    [Header("Auto Detach")]
    [SerializeField] bool autoDetachOnTooClose = true;           // 앵커 지나치게 가까우면 해제
    [SerializeField, Range(0.1f, 2f)] float tooCloseDist = 0.6f;
    [SerializeField] bool checkLineObstruction = true;           // 앵커-플레이어 사이 가림 시 해제
    #endregion

    #region State
    bool _grappling;
    Vector2 _anchor;
    float _ropeLength;
    #endregion

    #region Caches
    static readonly RaycastHit2D[] sRayHit = new RaycastHit2D[1];
    static readonly RaycastHit2D[] sLineHits = new RaycastHit2D[4];
    #endregion

    #region Public Properties
    public bool IsGrappling => _grappling;
    public Vector2 Anchor => _anchor;
    public float RopeLength => _ropeLength;
    #endregion

    #region Unity
    void Reset()
    {
        controller = GetComponent<Controller2D>();
        if (!cam) cam = Camera.main;
        TryInitLineRenderer();
    }

    void Awake()
    {
        if (!controller) controller = GetComponent<Controller2D>();
        if (!cam) cam = Camera.main;
        TryInitLineRenderer();
    }

    void Update()
    {
        HandleShootInput();
        HandleDetachInput();
        HandleReelInput(Time.deltaTime);
        AutoDetachChecks();
        UpdateLineRenderer();
    }
    #endregion

    #region Input Handlers
    void HandleShootInput()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 origin = GetGunOrigin();
        Vector2 dir = GetMouseDirFrom(origin);
        if (TryRayGrapple(origin, dir, out var hitPoint))
            AttachToPoint(hitPoint, initialLength: -1f);
    }

    void HandleDetachInput()
    {
        if (Input.GetMouseButtonDown(1))
            Detach();
    }

    void HandleReelInput(float dt)
    {
        if (!_grappling) return;
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 1e-3f) return;

        float len = _ropeLength - scroll * reelSpeed * dt;
        SetRopeLength(len);
    }
    #endregion

    #region Public API (외부 이동 스크립트에서 사용)
    /// <summary>
    /// 외부 이동 벡터를 Y축만 수정하여 로프 반경을 넘지 않도록 한다.
    /// - pos: 현재 위치
    /// - move: 외부에서 계산한 이동량(여기서 y만 수정)
    /// return: 수정되었으면 true
    /// </summary>
    public bool ConstrainMoveYOnly(Vector2 pos, ref Vector2 move)
    {
        if (!_grappling) return false;

        // 예측 위치(외부 x, 외부 y)
        Vector2 pred = pos + move;
        float dx = pred.x - _anchor.x;
        float dy = pred.y - _anchor.y;

        float L = _ropeLength;
        float L2 = L * L;
        float dx2 = dx * dx;

        // 수평만으로 L 초과면, y만으로는 해답이 없으므로 detach or y만 최소화
        if (dx2 > (L2 + skin))
        {
            // 1) 강제 해제
            // Detach();
            // return false;

            // 2) 혹은 가능한 한 앵커에 가까운 y로 클램프(원 위 최단 y 선택)
            float absDx = Mathf.Sqrt(dx2);
            float clampedDy = 0f; // 원의 접점으로 스냅할 수 없으니 y는 앵커 높이로 유도
            move.y = (_anchor.y + clampedDy) - pos.y;
            ClampFall(ref move); // 과도한 낙하 방지
            return true;
        }

        // 원 방정식: dx^2 + dy^2 = L^2
        // 주어진 dx로 가능한 dy = ±sqrt(L^2 - dx^2)
        float allowedDy = Mathf.Sqrt(Mathf.Max(0f, L2 - dx2));

        // 예측 y가 원 바깥( |dy| > allowedDy )이면 y만 조정
        float absDy = Mathf.Abs(dy);
        if (absDy > allowedDy + skin)
        {
            // 현재 dy의 부호 유지하면서 경계로 스냅
            float newDy = Mathf.Sign(dy) * allowedDy;
            float targetY = _anchor.y + newDy;
            float newMoveY = targetY - pos.y;

            // 원방향 팽창 감쇠(바깥으로 더 밀려나려는 move.y를 완화)
            if (newMoveY > move.y) // 위로 팽창하려는 경우
                move.y = Mathf.Lerp(move.y, newMoveY, Mathf.Clamp01(tensionDamp));
            else
                move.y = newMoveY;

            ClampFall(ref move);
            return true;
        }

        // 반경 내 → 선택적으로 낙하속도만 클램프
        ClampFall(ref move);
        return false;
    }

    /// <summary>외부에서 임의의 지점으로 붙이기(초기 길이 지정: 음수면 현재 거리로).</summary>
    public void AttachToPoint(Vector2 worldPoint, float initialLength = -1f)
    {
        _grappling = true;
        _anchor = worldPoint;
        float d = Vector2.Distance(_anchor, (Vector2)transform.position);
        _ropeLength = Mathf.Clamp(initialLength > 0f ? initialLength : d, ropeMin, ropeMax);
    }

    /// <summary>로프 해제.</summary>
    public void Detach()
    {
        _grappling = false;
        if (lr)
        {
            lr.positionCount = 0;
            lr.enabled = false;
        }
    }

    /// <summary>로프 길이 설정(클램프).</summary>
    public void SetRopeLength(float length)
    {
        _ropeLength = Mathf.Clamp(length, ropeMin, ropeMax);
    }
    #endregion

    #region Helpers: Attach/Raycast/Obstruction
    bool TryRayGrapple(Vector2 origin, Vector2 dir, out Vector2 hitPoint)
    {
        int hits = Physics2D.RaycastNonAlloc(origin, dir.normalized, sRayHit, maxGrappleDistance, grappleMask);
        if (hits > 0)
        {
            hitPoint = sRayHit[0].point;
            return true;
        }
        hitPoint = default;
        return false;
    }

    void AutoDetachChecks()
    {
        if (!_grappling) return;

        // 너무 가까우면 해제
        if (autoDetachOnTooClose && Vector2.Distance(transform.position, _anchor) < tooCloseDist)
        {
            Detach();
            return;
        }

        // 라인 가림 체크
        if (checkLineObstruction && _grappling)
        {
            Vector2 from = transform.position;
            int hitCount = Physics2D.LinecastNonAlloc(from, _anchor, sLineHits, grappleMask);
            if (hitCount > 0)
            {
                bool obstructed = true;
                for (int i = 0; i < hitCount; i++)
                {
                    // 앵커 지점 히트는 허용
                    if ((sLineHits[i].point - _anchor).sqrMagnitude < 0.0001f)
                    {
                        obstructed = false; break;
                    }
                }
                if (obstructed) Detach();
            }
        }
    }
    #endregion

    #region Helpers: Rendering & Util
    void UpdateLineRenderer()
    {
        if (!lr) return;

        if (_grappling)
        {
            lr.enabled = true;
            lr.positionCount = 2;
            lr.SetPosition(0, gunTip ? gunTip.position : transform.position);
            lr.SetPosition(1, (Vector3)_anchor);
        }
        else
        {
            lr.positionCount = 0;
            lr.enabled = false;
        }
    }

    void TryInitLineRenderer()
    {
        if (!lr) TryGetComponent(out lr);
        if (lr)
        {
            lr.useWorldSpace = true;
            lr.positionCount = 0;
            lr.widthMultiplier = 0.04f;
        }
    }

    Vector2 GetGunOrigin() => gunTip ? (Vector2)gunTip.position : (Vector2)transform.position;

    Vector2 GetMouseDirFrom(Vector2 origin)
    {
        Vector2 mouseW = cam ? (Vector2)cam.ScreenToWorldPoint(Input.mousePosition)
                             : origin + Vector2.right;
        Vector2 dir = (mouseW - origin);
        return dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
    }

    void ClampFall(ref Vector2 move)
    {
        // 여기서는 "이 프레임 이동량" 기준으로 단순 클램프.
        // 외부가 dt를 곱한 후 move를 넣는 전제이므로, 프레임당 최대 하강량 제한 느낌.
        float maxDown = -maxFallSpeed * Time.deltaTime;
        if (move.y < maxDown) move.y = maxDown;
    }
    #endregion
}
