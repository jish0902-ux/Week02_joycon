using UnityEngine;
using Game.Quests; // QuestFlags 퍼사드

[DisallowMultipleComponent]
public sealed class InteractionScanner2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Transform player;
    [SerializeField] InteractWorldPrompt promptUI;

    [Header("Probe")]
    [SerializeField] LayerMask interactableMask;
    [SerializeField, Min(0.2f)] float radius = 1.8f;

    [Header("Input (Legacy)")]
    [SerializeField] KeyCode key = KeyCode.E;
    [SerializeField] string moveAxisX = "Horizontal";
    [SerializeField] string moveAxisY = "Vertical";

    [Header("Buffer / Coyote / HoldGrace")]
    [SerializeField, Range(0.02f, 0.25f)] float inputBuffer = 0.12f;
    [SerializeField, Range(0.02f, 0.25f)] float coyoteTime = 0.12f;
    [SerializeField, Range(0.05f, 0.3f)] float holdGraceRelease = 0.12f;

    [Header("Scan")]
    [SerializeField, Range(0.02f, 0.2f)] float idleScanInterval = 0.06f; // 프롬프트용 저주기 스캔

    [Header("Stay (auto, no input)")]
    [SerializeField, Range(0f, 0.3f)] float stayGrace = 0.1f; // 잠깐 벗어나도 유지

    static readonly Collider2D[] s_Hits = new Collider2D[16];
    ContactFilter2D _filter;

    // 입력/감지 타임스탬프
    float _lastPressAt = -999f;
    float _lastTargetSeenAt = -999f;

    // 홀드 상태
    Interactable2D _holdTarget;
    float _holdElapsed;
    float _holdReleaseT;

    // 프롬프트/스캔 캐시
    Interactable2D _focus;
    float _scanAcc;

    // Stay(EnterArea) 상태
    Interactable2D _stayTarget;    // 현재 머무는 대상
    float _stayElapsed;            // 누적 체류 시간
    float _stayLastSeenAt;         // 마지막으로 스캔에서 보인 시각
    Interactable2D _stayScanCandidate; // 이번 스캔에서 가장 가까운 Stay 후보

    void Awake()
    {
        _filter = new ContactFilter2D();
        _filter.SetLayerMask(interactableMask);
        _filter.useTriggers = true;
    }

    void Update()
    {
        if (!player) return;

        // ----- HOLD 진행 우선 -----
        bool isDown = Input.GetKey(key);
        bool pressed = Input.GetKeyDown(key);
        if (pressed) _lastPressAt = Time.time;

        if (_holdTarget)
        {
            if (isDown) _holdReleaseT = 0f;
            else _holdReleaseT += Time.deltaTime;

            if ((!isDown && _holdReleaseT > holdGraceRelease) ||
                (_holdTarget.CancelOnMove && IsMoving()))
            {
                QuestEvents.RaiseHoldCanceled(_holdTarget.Id);
                _holdTarget = null;
                _holdElapsed = 0f;
                promptUI?.SetProgress01(0f);
                return;
            }

            _holdElapsed += Time.deltaTime;
            QuestEvents.RaiseHoldProgress(_holdTarget.Id, _holdElapsed);
            float need = Mathf.Max(0.01f, _holdTarget.RequiredHoldSeconds);
            promptUI?.SetProgress01(_holdElapsed / need);

            if (_holdElapsed >= need)
            {
                var id = _holdTarget.Id;
                var pos = _holdTarget.transform.position;
                _holdTarget = null;
                _holdElapsed = 0f;
                _holdReleaseT = 0f;
                promptUI?.SetProgress01(0f);
                QuestEvents.RaiseInteract(id, pos, InteractionKind.Hold);
            }
            return;
        }

        // ----- STAY(EnterArea) 누적 처리 (프레임마다) -----
        if (_stayTarget != null)
        {
            bool seenRecently = (Time.time - _stayLastSeenAt) <= stayGrace;
            if (seenRecently)
            {
                _stayElapsed += Time.deltaTime;
                float need = Mathf.Max(0.01f, _stayTarget.RequiredStaySeconds);
                if (_stayElapsed >= need)
                {
                    var id = _stayTarget.Id;
                    var pos = _stayTarget.transform.position;
                    _stayTarget = null;
                    _stayElapsed = 0f;
                    QuestEvents.RaiseInteract(id, pos, InteractionKind.EnterArea);
                }
            }
            else
            {
                // 시야(반경)에서 벗어난 지 grace를 넘김 → 리셋
                _stayTarget = null;
                _stayElapsed = 0f;
            }
        }

        // ----- 저주기 스캔 (프롬프트 & Stay 후보 갱신) -----
        _scanAcc += Time.deltaTime;
        if (_scanAcc >= idleScanInterval || pressed)
        {
            _scanAcc = 0f;

            _stayScanCandidate = null; // 이번 스캔에서 가장 가까운 Stay 후보 초기화
            _focus = FindBestTarget((Vector2)player.position);

            // Stay 후보 갱신 & 타이머 유지
            if (_stayScanCandidate != null && _stayScanCandidate.RequiredStaySeconds > 0f)
            {
                if (_stayTarget == _stayScanCandidate)
                {
                    _stayLastSeenAt = Time.time; // 같은 타깃 계속 보고 있음
                }
                else
                {
                    _stayTarget = _stayScanCandidate;
                    _stayElapsed = 0f;
                    _stayLastSeenAt = Time.time;
                }
            }

            // 프롬프트: EnterArea는 UI 혼란 방지를 위해 숨김
            if (_focus)
            {
                _lastTargetSeenAt = Time.time;
                if (_focus.Kind != InteractionKind.EnterArea)
                    promptUI?.Show(_focus.PromptAnchor, _focus.PromptOffset, _focus.PromptText,
                                   _focus.Kind == InteractionKind.Hold);
                else
                    promptUI?.Hide();
            }
            else
            {
                promptUI?.Hide();
            }
        }

        // ----- 입력 버퍼 + 코요테 판정 (Press/Hold/UseItem 전용) -----
        bool bufferedPressOk =
            (Time.time - _lastPressAt) <= inputBuffer &&
            (Time.time - _lastTargetSeenAt) <= coyoteTime;

        if (!bufferedPressOk || !_focus) return;

        // 선행 조건 재검증
        if (!_focus.HasRequiredFlags(QuestFlags.Has)) return;
        if (!_focus.CheckItem(Inventory.HasItem)) return;

        if(_focus.ActiveToDestory == true)
        {
            Debug.Log($"ActiveToDestory {_focus.transform.name}");
            Destroy(_focus.gameObject);
        }

        // 실행 분기
        switch (_focus.Kind)
        {
            case InteractionKind.Press:
                QuestEvents.RaiseInteract(_focus.Id, _focus.transform.position, InteractionKind.Press);
                break;

            case InteractionKind.Hold:
                _holdTarget = _focus;
                _holdElapsed = 0f;
                _holdReleaseT = 0f;
                QuestEvents.RaiseHoldStarted(_holdTarget.Id, Mathf.Max(0.01f, _holdTarget.RequiredHoldSeconds));
                promptUI?.Show(_holdTarget.PromptAnchor, _holdTarget.PromptOffset,
                               _holdTarget.PromptText, showProgress: true);
                promptUI?.SetProgress01(0f);
                break;

            case InteractionKind.EnterArea:
                // EnterArea(머무르기)는 위 STAY 누적 블록이 자동 처리 → 입력으로는 아무 것도 하지 않음
                break;

            case InteractionKind.UseItem:
                QuestEvents.RaiseInteract(_focus.Id, _focus.transform.position, InteractionKind.UseItem);
                break;

            
        }
    }

    bool IsMoving()
    {
        float x = Input.GetAxisRaw(moveAxisX);
        float y = Input.GetAxisRaw(moveAxisY);
        return (x * x + y * y) > 0.0001f;
    }

    // 프롬프트 타깃 + Stay 후보 동시 선별
    Interactable2D FindBestTarget(Vector2 pos)
    {
        int count = Physics2D.OverlapCircle(pos, radius, _filter, s_Hits);
        if (count == 0) return null;

        float bestPrompt = float.MaxValue;
        Interactable2D bestPromptIt = null;

        float bestStay = float.MaxValue;
        Interactable2D bestStayIt = null;

        for (int i = 0; i < count; ++i)
        {
            var c = s_Hits[i];
            s_Hits[i] = null;
            if (!c || !c.TryGetComponent(out Interactable2D it)) continue;

            if (!it.HasRequiredFlags(QuestFlags.Has)) continue;
            if (!it.CheckItem(Inventory.HasItem)) continue;

            float d2 = ((Vector2)it.transform.position - pos).sqrMagnitude;

            if (it.Kind == InteractionKind.EnterArea && it.RequiredStaySeconds > 0f)
            {
                if (d2 < bestStay) { bestStay = d2; bestStayIt = it; }
                // EnterArea는 프롬프트 타깃에서 제외(원하면 포함해도 됨)
                continue;
            }

            if (d2 < bestPrompt) { bestPrompt = d2; bestPromptIt = it; }
        }

        _stayScanCandidate = bestStayIt; // 이번 스캔에서 본 가장 가까운 Stay 타깃
        return bestPromptIt;
    }
}
