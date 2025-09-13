using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public sealed class SpikeController : MonoBehaviour
{
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool oneShot = false;     // 한 번만 작동
    [SerializeField] private float cooldown = 0f;      // 재발동 쿨다운(초)

    bool _used;
    float _last;

    void Reset()
    {
        // 스파이크 콜라이더는 트리거로!
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_used && oneShot) return;
        if (cooldown > 0f && Time.time - _last < cooldown) return;
        if (!other.CompareTag(targetTag)) return;

        // PlayerRespawn2D_RuntimeSO 찾아서 Die()
        // 우선 attachedRigidbody에서 찾고, 없으면 상위 계층에서 백업 검색
        PlayerRespawn2D_RuntimeSO respawn = null;

        var rb2d = other.attachedRigidbody;
        if (rb2d) respawn = rb2d.GetComponent<PlayerRespawn2D_RuntimeSO>();
        if (!respawn) respawn = other.GetComponentInParent<PlayerRespawn2D_RuntimeSO>();

        if (respawn)
        {
            Debug.Log("Player Trigger");

            respawn.Die();
            _last = Time.time;
            if (oneShot) _used = true;
        }
    }
}
