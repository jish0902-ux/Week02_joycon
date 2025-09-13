using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class ArrowProjectile2D : MonoBehaviour
{
    [Header("Visual/Rot")]
    [SerializeField] private bool rotateToVelocity = true;
    [SerializeField] private bool forwardIsRight = true; // true: sprite가 오른쪽 바라봄, false: 위

    [Header("Collision")]
    [SerializeField] private LayerMask blockMask = ~0;
    [SerializeField] private string playerTag = "Player";

    Rigidbody2D _rb;
    Collider2D _col;

    Vector2 _dir;
    float _speed;
    float _life;
    ArrowSpawner2D _owner;

    Collider2D _ignored; // 스포너 충돌무시용
    bool _active;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();

        // 추천 세팅: Kinematic + Continuous + isTrigger = true
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.simulated = true;
        _rb.gravityScale = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _col.isTrigger = true;
    }

    public void Activate(Vector2 position, Vector2 direction, float speed, float life, ArrowSpawner2D owner, Collider2D ignore)
    {
        _owner = owner;
        _dir = direction.normalized;
        _speed = speed;
        _life = life;
        _active = true;

        // 위치/회전
        transform.position = position;
        if (rotateToVelocity)
        {
            var angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, forwardIsRight ? angle : (angle - 90f));
        }

        // 이전 ignore 해제
        if (_ignored && ignore != _ignored && _col)
            Physics2D.IgnoreCollision(_col, _ignored, false);

        // 새 ignore 등록
        _ignored = ignore;
        if (_ignored && _col)
            Physics2D.IgnoreCollision(_col, _ignored, true);
    }

    void Update()
    {
        if (!_active) return;

        // 이동(트리거 충돌 이벤트만 신뢰)
        var pos = (Vector2)transform.position + _dir * (_speed * Time.deltaTime);
        _rb.MovePosition(pos);

        // 생명시간
        _life -= Time.deltaTime;
        if (_life <= 0f)
            Despawn();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!_active) return;

        // 스포너 무시
        if (_ignored && other == _ignored) return;

        // 플레이어 타격
        if (other.CompareTag(playerTag))
        {
            PlayerRespawn2D_RuntimeSO respawn = null;

            // Rigidbody2D가 붙은 루트에서 먼저 탐색
            var rb2d = other.attachedRigidbody;
            if (rb2d) respawn = rb2d.GetComponent<PlayerRespawn2D_RuntimeSO>();
            if (!respawn) respawn = other.GetComponentInParent<PlayerRespawn2D_RuntimeSO>();

            if (respawn) respawn.Die();

            Despawn();
            return;
        }

        // 벽/바닥 등 블로킹 레이어
        if (((1 << other.gameObject.layer) & blockMask) != 0)
        {
            Despawn();
            return;
        }
    }

    void OnDisable()
    {
        // 재사용 대비: 이전 ignore 관계 해제
        if (_ignored && _col)
            Physics2D.IgnoreCollision(_col, _ignored, false);
        _ignored = null;
        _active = false;
    }

    void Despawn()
    {
        _active = false;
        if (_owner) _owner.Despawn(this);
        else gameObject.SetActive(false);
    }
}
