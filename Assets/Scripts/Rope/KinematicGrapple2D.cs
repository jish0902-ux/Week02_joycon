using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// 이동은 외부(플레이어) 스크립트가 계산하고,
/// 이 컴포넌트는 "로프 상태 + 액션(스윙/펌핑/릴/슬링샷 해제)"만 담당.
/// - 좌클릭: 그랩 사격(마우스 조준)
/// - 우클릭: 해제
/// - 휠: 로프 길이 조절(릴/언릴)
/// - 외부에서 Move 호출 전: ApplyRopeAction(...)으로 move 재구성
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Controller2D))]
[DefaultExecutionOrder(1000)] // 보통의 이동 스크립트보다 늦게 돌도록(입력 처리 후)
public sealed class KinematicGrapple2D : MonoBehaviour
{
    #region Inspector - Refs
    [Header("Refs")]
    [SerializeField] Controller2D controller;
    [SerializeField] Transform gunTip;        // 레이 원점(없으면 transform)
    [SerializeField] Camera cam;              // 조준 카메라(없으면 Camera.main)
    [SerializeField] LineRenderer lr;         // 선택: 로프 표시
    #endregion

    #region Inspector - Grapple
    [Header("Grapple")]
    [SerializeField] LayerMask grappleMask = ~0;
    [SerializeField, Range(1f, 50f)] float maxGrappleDistance = 20f;
    [SerializeField, Range(0.5f, 40f)] float ropeMin = 1.2f;
    [SerializeField, Range(1f, 60f)] float ropeMax = 25f;
    [SerializeField, Range(0.5f, 12f)] float reelSpeed = 6f;     // 휠 민감도
    #endregion

    #region Inspector - Auto Detach
    [Header("Auto Detach")]
    [SerializeField] bool autoDetachOnTooClose = true;           // 앵커 지나치게 가까우면 해제
    [SerializeField, Range(0.1f, 2f)] float tooCloseDist = 0.6f;
    [SerializeField] bool checkLineObstruction = true;           // 앵커-플레이어 사이 가림 시 해제
    #endregion

    #region Inspector - Action Tuning
    [Header("Action Tuning")]
    [SerializeField, Range(0f, 200f)] float tangentAccel = 90f;     // 좌/우 입력 → 접선 가속(각가속으로 환산)
    [SerializeField, Range(0f, 10f)] float angularDamp = 2.0f;    // 각속도 감쇠(공기저항 느낌)
    [SerializeField, Range(60f, 1440f)] float maxAngularSpeedDeg = 720f; // 최대 각속도(deg/s)
    [SerializeField] bool invertDirection = false; // 좌/우 반전
    [SerializeField, Range(0f, 3f)] float pumpAssist = 0.75f;   // 아래쪽에서 상/하 입력 시 각속도 보너스
    [SerializeField, Range(0.1f, 1f)] float pumpWindow = 0.45f;   // '아래쪽' 판정(코사인 임계치)
    [Header("Release")]
    [SerializeField, Range(0f, 2.5f)] float releaseBoost = 1.0f;    // 해제 시 접선 속도 배율
    [SerializeField, Range(0f, 15f)] float radialBoost = 0f;      // 해제 시 방사(바깥) 임펄스
    #endregion

    #region State
    bool _grappling;
    Vector2 _anchor;
    float _ropeLength;
    float _angVel;       // rad/s
    float _maxAngVel;    // rad/s (캐시)
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
        _maxAngVel = maxAngularSpeedDeg * Mathf.Deg2Rad;
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
    /// 외부에서 호출: 입력 기반 로프 액션을 계산해 move를 재구성한다.
    /// - pos: 현재 위치(월드)
    /// - move: 외부가 계산한 이동량(이 함수가 '로프 액션'에 맞게 덮어씀)
    /// - inputX: 좌/우 입력(-1..1)  접선 가속
    /// - inputY: 상/하 입력(-1..1)  펌핑/릴 보조(옵션)
    /// - jumpPressed: true면 이번 프레임에 해제(슬링샷 임펄스 out)
    /// - dt: deltaTime
    /// 반환: true면 move가 로프 기준으로 수정됨
    /// </summary>
    public bool ApplyRopeAction(Vector2 pos, ref Vector2 move, float inputX, float inputY, bool jumpPressed, float dt, out Vector2 releaseImpulse)
    {
        releaseImpulse = Vector2.zero;
        if (!_grappling || dt <= 0f) return false;

        // 현재 로프 좌표계
        Vector2 r = pos - _anchor;
        float L = _ropeLength;
        float rMag = r.magnitude;
        if (rMag < 1e-6f || L < 1e-6f) return false;

        Vector2 n = r / rMag;               // 방사 단위벡터
        Vector2 t = new Vector2(-n.y, n.x); // 접선 단위벡터(좌회전)

        // 1) 각속도 갱신(입력 + 펌핑 + 감쇠)
        float ix = invertDirection ? -inputX : inputX;

        // 입력을 각가속으로 환산: a_theta ≈ (tangentAccel / L)
        float angAcc = 0f;
        angAcc += (tangentAccel * ix) / Mathf.Max(L, 1e-4f);

        // '아래쪽' 근방에서 상/하 입력으로 펌핑(코사인>threshold일 때 가산)
        float cosDown = Vector2.Dot(n, Vector2.down); // 1에 가까울수록 아래
        if (cosDown > pumpWindow)
            angAcc += pumpAssist * inputY * cosDown / Mathf.Max(L, 1f);

        // 감쇠
        angAcc -= angularDamp * _angVel;

        _angVel += angAcc * dt;
        _angVel = Mathf.Clamp(_angVel, -_maxAngVel, _maxAngVel);

        // 2) 각변위 & 목표점 계산
        float dTheta = _angVel * dt;
        Vector2 target;
        if (Mathf.Abs(dTheta) < 1e-6f)
        {
            // 외부 예측을 반경 L로 투영(반지름 유지)
            Vector2 pred = pos + move;
            Vector2 rp = pred - _anchor;
            float pmag = rp.magnitude;
            Vector2 newR = (pmag > 1e-6f) ? rp * (L / pmag) : r.normalized * L;
            target = _anchor + newR;
        }
        else
        {
            // 정확 회전
            Vector2 newR = Rotate(r, dTheta).normalized * L;
            target = _anchor + newR;
        }
        move = target - pos;

        // 3) 자동 해제/가림 체크
        AutoDetachChecks();
        if (!_grappling) return true;

        // 4) 점프 해제(슬링샷)
        if (jumpPressed)
        {
            // 접선 속도 v = ω × L
            float tangentialSpeed = Mathf.Abs(_angVel) * L * releaseBoost;
            Vector2 vTan = t * Mathf.Sign(_angVel) * tangentialSpeed;
            Vector2 vRad = n * radialBoost;

            releaseImpulse = vTan + vRad; // 외부 "속도"에 더해 쓰길 권장
            Detach();
            // move는 target 기반으로 유지하여 자연스럽게 이어지게 함
        }

        return true;
    }
    #endregion

    #region Public Grapple Controls
    /// <summary>외부에서 임의의 지점으로 붙이기(초기 길이 지정: 음수면 현재 거리로).</summary>
    public void AttachToPoint(Vector2 worldPoint, float initialLength = -1f)
    {
        _grappling = true;
        _anchor = worldPoint;
        float d = Vector2.Distance(_anchor, (Vector2)transform.position);
        _ropeLength = Mathf.Clamp(initialLength > 0f ? initialLength : d, ropeMin, ropeMax);
        _angVel = 0f; // 부정합 방지
    }

    /// <summary>로프 해제.</summary>
    public void Detach()
    {
        _grappling = false;
        _angVel = 0f;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Vector2 Rotate(in Vector2 v, float angleRad)
    {
        float c = Mathf.Cos(angleRad);
        float s = Mathf.Sin(angleRad);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }
    #endregion
}
