using UnityEngine;

/// <summary>
/// �̵��� �ܺ� ��ũ��Ʈ�� �����ϰ�, �� ������Ʈ�� "���� ���� + ����"�� ���.
/// - ��Ŭ��: �׷� ���(���콺 ����)
/// - ��Ŭ��: ����
/// - ��: ���� ���� ����(��/��)
/// - �ܺο��� Move ȣ�� ��: ConstrainMoveYOnly(ref move)�� Y�ุ ����
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Controller2D))]
public sealed class KinematicGrapple2D : MonoBehaviour
{
    #region Inspector
    [Header("Refs")]
    [SerializeField] Controller2D controller;
    [SerializeField] Transform gunTip;        // ���� ����(������ transform)
    [SerializeField] Camera cam;              // ���� ī�޶�(������ Camera.main)
    [SerializeField] LineRenderer lr;         // ����: ���� ǥ��

    [Header("Grapple")]
    [SerializeField] LayerMask grappleMask = ~0;
    [SerializeField, Range(1f, 50f)] float maxGrappleDistance = 20f;
    [SerializeField, Range(0.5f, 40f)] float ropeMin = 1.2f;
    [SerializeField, Range(1f, 60f)] float ropeMax = 25f;
    [SerializeField, Range(0.5f, 12f)] float reelSpeed = 6f;     // �� �ΰ���

    [Header("Constraint")]
    [SerializeField, Range(0f, 1f)] float tensionDamp = 0.2f;    // ������ ��â ����(��: �ܺ� move.y�� ������ ��)
    [SerializeField, Range(5f, 60f)] float maxFallSpeed = 30f;   // �ϰ� �ӵ� ����(������ Clamp)
    [SerializeField, Range(0f, 0.02f)] float skin = 0.001f;      // �ݰ� ������

    [Header("Auto Detach")]
    [SerializeField] bool autoDetachOnTooClose = true;           // ��Ŀ ����ġ�� ������ ����
    [SerializeField, Range(0.1f, 2f)] float tooCloseDist = 0.6f;
    [SerializeField] bool checkLineObstruction = true;           // ��Ŀ-�÷��̾� ���� ���� �� ����
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

    #region Public API (�ܺ� �̵� ��ũ��Ʈ���� ���)
    /// <summary>
    /// �ܺ� �̵� ���͸� Y�ุ �����Ͽ� ���� �ݰ��� ���� �ʵ��� �Ѵ�.
    /// - pos: ���� ��ġ
    /// - move: �ܺο��� ����� �̵���(���⼭ y�� ����)
    /// return: �����Ǿ����� true
    /// </summary>
    public bool ConstrainMoveYOnly(Vector2 pos, ref Vector2 move)
    {
        if (!_grappling) return false;

        // ���� ��ġ(�ܺ� x, �ܺ� y)
        Vector2 pred = pos + move;
        float dx = pred.x - _anchor.x;
        float dy = pred.y - _anchor.y;

        float L = _ropeLength;
        float L2 = L * L;
        float dx2 = dx * dx;

        // �������� L �ʰ���, y�����δ� �ش��� �����Ƿ� detach or y�� �ּ�ȭ
        if (dx2 > (L2 + skin))
        {
            // 1) ���� ����
            // Detach();
            // return false;

            // 2) Ȥ�� ������ �� ��Ŀ�� ����� y�� Ŭ����(�� �� �ִ� y ����)
            float absDx = Mathf.Sqrt(dx2);
            float clampedDy = 0f; // ���� �������� ������ �� ������ y�� ��Ŀ ���̷� ����
            move.y = (_anchor.y + clampedDy) - pos.y;
            ClampFall(ref move); // ������ ���� ����
            return true;
        }

        // �� ������: dx^2 + dy^2 = L^2
        // �־��� dx�� ������ dy = ��sqrt(L^2 - dx^2)
        float allowedDy = Mathf.Sqrt(Mathf.Max(0f, L2 - dx2));

        // ���� y�� �� �ٱ�( |dy| > allowedDy )�̸� y�� ����
        float absDy = Mathf.Abs(dy);
        if (absDy > allowedDy + skin)
        {
            // ���� dy�� ��ȣ �����ϸ鼭 ���� ����
            float newDy = Mathf.Sign(dy) * allowedDy;
            float targetY = _anchor.y + newDy;
            float newMoveY = targetY - pos.y;

            // ������ ��â ����(�ٱ����� �� �з������� move.y�� ��ȭ)
            if (newMoveY > move.y) // ���� ��â�Ϸ��� ���
                move.y = Mathf.Lerp(move.y, newMoveY, Mathf.Clamp01(tensionDamp));
            else
                move.y = newMoveY;

            ClampFall(ref move);
            return true;
        }

        // �ݰ� �� �� ���������� ���ϼӵ��� Ŭ����
        ClampFall(ref move);
        return false;
    }

    /// <summary>�ܺο��� ������ �������� ���̱�(�ʱ� ���� ����: ������ ���� �Ÿ���).</summary>
    public void AttachToPoint(Vector2 worldPoint, float initialLength = -1f)
    {
        _grappling = true;
        _anchor = worldPoint;
        float d = Vector2.Distance(_anchor, (Vector2)transform.position);
        _ropeLength = Mathf.Clamp(initialLength > 0f ? initialLength : d, ropeMin, ropeMax);
    }

    /// <summary>���� ����.</summary>
    public void Detach()
    {
        _grappling = false;
        if (lr)
        {
            lr.positionCount = 0;
            lr.enabled = false;
        }
    }

    /// <summary>���� ���� ����(Ŭ����).</summary>
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

        // �ʹ� ������ ����
        if (autoDetachOnTooClose && Vector2.Distance(transform.position, _anchor) < tooCloseDist)
        {
            Detach();
            return;
        }

        // ���� ���� üũ
        if (checkLineObstruction && _grappling)
        {
            Vector2 from = transform.position;
            int hitCount = Physics2D.LinecastNonAlloc(from, _anchor, sLineHits, grappleMask);
            if (hitCount > 0)
            {
                bool obstructed = true;
                for (int i = 0; i < hitCount; i++)
                {
                    // ��Ŀ ���� ��Ʈ�� ���
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
        // ���⼭�� "�� ������ �̵���" �������� �ܼ� Ŭ����.
        // �ܺΰ� dt�� ���� �� move�� �ִ� �����̹Ƿ�, �����Ӵ� �ִ� �ϰ��� ���� ����.
        float maxDown = -maxFallSpeed * Time.deltaTime;
        if (move.y < maxDown) move.y = maxDown;
    }
    #endregion
}
