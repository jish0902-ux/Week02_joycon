using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class AreaZoneDetector : MonoBehaviour
{
    public AreaNameUI areaUI;
    public string defaultName = "거리";

    [Header("Filter")]
    [SerializeField] private LayerMask zoneMask;

    QuestTodoUI questTodoUI;

    // --- Dictionary 매핑 ---
    private static readonly Dictionary<string, uint> ZoneToQuestId = new()
    {
        { "복도", 1000 },
        { "식당", 2000 },
        { "화장실", 2000 },
        { "훈련소", 2000 },
        { "무기고", 3000 },
        { "보물창고", 3000 },
        { "테스트", 9000 },
        // 필요하면 계속 추가
    };

    void Awake()
    {
        if (areaUI == null)
            Debug.LogWarning("[AreaZoneDetector] areaUI가 비었습니다.", this);

        questTodoUI = GameObject.FindAnyObjectByType<QuestTodoUI>();
    }

    void Start()
    {
        if (areaUI != null)
            areaUI.SetAreaName(defaultName);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (zoneMask.value != 0 && (zoneMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        var zone = other.GetComponentInParent<AreaZone>();
        if (zone == null) return;

        // UI 변경
        areaUI?.SetAreaName(zone.displayName);

        // --- QuestTodoUI 변경 ---
        if (questTodoUI != null && ZoneToQuestId.TryGetValue(zone.displayName, out uint qid))
        {
            questTodoUI.SetQuest(qid);
        }
    }
}
