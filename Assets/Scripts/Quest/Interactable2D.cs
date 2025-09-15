using Unity.Burst;
using UnityEngine;

public enum InteractionKind : byte
{
    None,
    Press,          // 키 눌렀을 때 즉시
    Hold,           // 일정 시간 홀드
    EnterArea,      // 트리거 진입형(스캐너 없이 트리거가 Raise)
    UseItem         // 특정 아이템 보유 조건 등
}

[DisallowMultipleComponent]
public sealed class Interactable2D : MonoBehaviour
{
    [Header("ID / Type")]
    [SerializeField] string id = "Box_A";
    [SerializeField] InteractionKind kind = InteractionKind.Press;

    [Header("Hold Option")]
    [SerializeField, Min(0f)] float requiredHoldSeconds = 0.8f; // 0이면 무시
    [SerializeField] bool cancelOnMove = false;


    [Header("Stay Option")]
    [SerializeField, Min(0f)] float requiredStaySeconds = 0.8f; // 0이면 무시



    [Header("prompt")]
    [SerializeField] string promptText = "상호작용";
    [SerializeField] Transform promptAnchor;            // 없으면 this
    [SerializeField] Vector3 promptOffset = new Vector3(0, 1f, 0);

    [Header("Preconditions (all satisfied if any)\r\n")]
    [SerializeField] string[] requiredEventFlags;
    [SerializeField] string requiredItemId;             // 빈 문자열이면 무시

    public string Id => id;
    public InteractionKind Kind => kind;
    public float RequiredHoldSeconds => requiredHoldSeconds;
    public float RequiredStaySeconds => requiredStaySeconds;
    public bool CancelOnMove => cancelOnMove;

    public bool ActiveToDestory = false;

    public Transform PromptAnchor => promptAnchor ? promptAnchor : transform;
    public Vector3 PromptOffset => promptOffset;
    public string PromptText => promptText;



    public bool HasRequiredFlags(System.Func<string, bool> flagChecker)
    {
        if (requiredEventFlags == null || requiredEventFlags.Length == 0) return true;
        if (flagChecker == null) return false;
        for (int i = 0; i < requiredEventFlags.Length; ++i)
            if (!flagChecker(requiredEventFlags[i])) return false;
        return true;
    }
    public bool CheckItem(System.Func<string, bool> hasItem)
        => string.IsNullOrEmpty(requiredItemId) || (hasItem != null && hasItem(requiredItemId));




    /*   float ComputeScore(Interactable2D it, Vector2 pos, Vector2 facing)
       {
           Vector2 to = (Vector2)it.transform.position - pos;
           float d2 = to.sqrMagnitude + 0.0001f;
           float score = d2 * it.PriorityBias;

           // 정면 각도(선택)
           if (it.MinFacingDot > -0.999f)
           {
               float dot = Vector2.Dot(facing.normalized, to.normalized);
               if (dot < it.MinFacingDot) return float.PositiveInfinity;
               // 정면일수록 가중치↓
               score *= Mathf.Lerp(2f, 0.5f, (dot + 1f) * 0.5f);
           }

           // 시야(선택)
           if (it.LOSBlockMask != 0 && !HasLOS(pos, it.transform.position, it.LOSBlockMask))
               score *= 10f; // 패널티

           // 퀘스트 관련성(있으면 가중치↓)
           if (QuestRuntime.IsRelevantTarget(it.Id)) score *= 0.5f;

           return score;
       }

       bool HasLOS(Vector2 from, Vector2 to, LayerMask block)
       {
           Vector2 dir = to - from; float dist = dir.magnitude;
           return dist <= 0.001f || !Physics2D.Raycast(from, dir / dist, dist, block);
       }

     [SerializeField] float hysteresis = 1.15f; // 15% 더 나빠지기 전까진 유지
   Interactable2D _stickyTarget; float _stickyScore;

   Interactable2D ApplyHysteresis(Interactable2D best, float bestScore)
   {
       if (_stickyTarget && _stickyTarget == best) { _stickyScore = bestScore; return best; }
       if (_stickyTarget && _stickyScore * hysteresis < bestScore) return _stickyTarget;
       _stickyTarget = best; _stickyScore = bestScore; return best;
   }


     */
}
