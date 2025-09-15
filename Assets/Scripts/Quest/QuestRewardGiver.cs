using UnityEngine;

public sealed class QuestRewardGiver : MonoBehaviour
{
    [SerializeField] private QuestSO quest;

    PauseMenu pauseMenu;
    private void Awake()
    {
        pauseMenu = GameObject.FindAnyObjectByType<PauseMenu>();


    }
    void OnEnable()
    {
        if (quest) quest.Completed += OnQuestCompleted; // 구독

    }

    void OnDisable()
    {
        if (quest) quest.Completed -= OnQuestCompleted; // 꼭 해제
    }

    private void OnQuestCompleted(QuestSO so)
    {

        pauseMenu.ShowEndingScreen();
        // 가벼운 로직: 코인 지급, 플래그 세팅, UI 토스트 등
        // 예) PlayerInventory.AddCoins(100);
        Debug.Log($"[Quest] Completed: {so.name}");
    }
}
