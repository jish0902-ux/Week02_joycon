using System.Collections.Generic;
using UnityEngine;
using Game.Quests; // QuestEvents, InteractionKind

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class StayScanner2D : MonoBehaviour
{
    [Header("Scanner ID")]
    [SerializeField] private uint scannerId = 1; // Carryable.ScannerID와 같아야 발동

    [Header("Actor Filter (Carryable가 있는 레이어)")]
    [SerializeField] private LayerMask actorMask = ~0;

    [Header("Stay Tuning")]
    [SerializeField, Min(0.01f)] private float requiredStaySeconds = 0.8f;
    [SerializeField, Range(0f, 0.5f)] private float stayGrace = 0.1f;
    [SerializeField] private bool excludeCarried = true; // 들고 있는 동안은 제외

    [Header("Fire Policy")]
    [SerializeField] private bool oneShot = true;
    [SerializeField, Range(0f, 10f)] private float cooldown = 0f;

    [Header("Boot")]
    [SerializeField] private bool scanOnEnable = true;

    // ─────────────────────────────────────────────────────────────────────
    private Collider2D _trigger;
    private ContactFilter2D _filter;
    private float _lastFireAt = -999f;
    private bool _firedOnce;

    private sealed class State
    {
        public Carryable carry;
        public float elapsed;
        public float lastSeenAt;
    }

    // Carryable 단위 추적
    private readonly Dictionary<Carryable, State> _tracked = new(64);
    // Collider2D → Carryable 캐시 (OnTriggerStay 비용 절감)
    private readonly Dictionary<Collider2D, Carryable> _col2Carry = new(128);

    private static readonly List<Carryable> _toRemove = new(32);
    private static readonly Collider2D[] _hits = new Collider2D[32];

    void Awake()
    {
        _trigger = GetComponent<Collider2D>();
        _trigger.isTrigger = true;

        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.simulated = true;

        _filter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = true,
            layerMask = actorMask
        };
    }

    void OnEnable()
    {
        _firedOnce = false;

        if (!scanOnEnable || _trigger == null) return;

#if UNITY_6000_0_OR_NEWER
        int count = _trigger.Overlap(_filter, _hits);
#else
        int count = _trigger.OverlapCollider(_filter, _hits);
#endif
        for (int i = 0; i < count; ++i)
        {
            var c = _hits[i];
            _hits[i] = null;
            if (!c) continue;

            var carry = ResolveCarryable(c);
            if (!carry) continue;
            if (carry.ScannerID != scannerId) continue; // 스캐너-타깃 매칭

            _col2Carry[c] = carry;
            TryTrack(carry, true);
        }
    }

    void Update()
    {
        if (oneShot && _firedOnce) return;

        float now = Time.time;
        bool allowFireNow = (now - _lastFireAt) >= cooldown;

        if (_tracked.Count == 0 || !allowFireNow) return;

        _toRemove.Clear();

        foreach (var kv in _tracked)
        {
            var st = kv.Value;
            var carry = st.carry;
            if (!carry) { _toRemove.Add(kv.Key); continue; }

            // 매칭이 바뀌었다면 제거(안전)
            if (carry.ScannerID != scannerId) { _toRemove.Add(kv.Key); continue; }

            if (excludeCarried && carry.carrying) { _toRemove.Add(kv.Key); continue; }

            bool seenRecently = (now - st.lastSeenAt) <= stayGrace;
            if (!seenRecently) { _toRemove.Add(kv.Key); continue; }

            st.elapsed += Time.deltaTime;
            if (st.elapsed >= requiredStaySeconds)
            {
                // ★ Carryable.Id로 이벤트 발행 (위치는 스캐너 기준; 필요시 carry.transform.position)
                QuestEvents.RaiseInteract(carry.Id, transform.position, InteractionKind.EnterArea);

                _lastFireAt = now;
                _firedOnce = true;

                _toRemove.Add(kv.Key);
                if (oneShot) break;
            }
        }

        for (int i = 0; i < _toRemove.Count; ++i)
            _tracked.Remove(_toRemove[i]);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Trigger Events
    // ─────────────────────────────────────────────────────────────────────
    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & actorMask.value) == 0) return;

        var carry = ResolveCarryable(other);
        if (!carry) return;
        if (carry.ScannerID != scannerId) return;

        _col2Carry[other] = carry;
        TryTrack(carry, true);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!_col2Carry.TryGetValue(other, out var carry))
        {
            carry = ResolveCarryable(other);
            if (!carry) return;
            if (carry.ScannerID != scannerId) return;
            _col2Carry[other] = carry;
        }

        if (_tracked.TryGetValue(carry, out var st))
            st.lastSeenAt = Time.time;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        _col2Carry.Remove(other); // grace 만료는 Update에서 정리

        if (!_col2Carry.TryGetValue(other, out var carry))
        {
            carry = ResolveCarryable(other);
            if (!carry) return;
            if (carry.ScannerID != scannerId) return;
            //_col2Carry[other] = carry;

            bool ok = QuestEvents.CancelLastInteract(carry.Id);
            if (!ok)
            {
                Debug.LogWarning($"CancelLastInteract 실패: id={carry.Id}");
            }

            //QuestEvents.RaiseInteract(carry.Id, transform.position, InteractionKind.EnterArea);

        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────
    private void TryTrack(Carryable carry, bool seenNow)
    {
        if (!carry) return;
        if (excludeCarried && carry.carrying) return;

        if (_tracked.ContainsKey(carry))
        {
            if (seenNow && _tracked.TryGetValue(carry, out var st))
                st.lastSeenAt = Time.time;
            return;
        }

        _tracked.Add(carry, new State
        {
            carry = carry,
            elapsed = 0f,
            lastSeenAt = seenNow ? Time.time : 0f
        });
    }

    private static Carryable ResolveCarryable(Collider2D col)
    {
        if (!col) return null;
        if (col.TryGetComponent(out Carryable c)) return c;
        return col.GetComponentInParent<Carryable>();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (TryGetComponent(out Collider2D col)) col.isTrigger = true;
        if (TryGetComponent(out Rigidbody2D rb))
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.freezeRotation = true;
        }
    }
#endif
}
