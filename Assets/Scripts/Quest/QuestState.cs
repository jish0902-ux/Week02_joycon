using static QuestManager;

/*public static class QuestState
{

    public class QuestState
    {
        public QuestSO so;
        public bool started;
        public bool completed;
        public ObjectiveState[] objectives;
    }

    // TODO: 실제로는 세이브/로딩/Dictionary 등으로 관리
    public static bool HasFlag(string flagId)
    {
        // ex) return SaveData.Flags.Contains(flagId);
        return false;
    }
}
*/
public static class Inventory
{
    public static bool HasItem(string itemId)
    {
        // ex) return SaveData.Items.TryGetValue(itemId, out var count) && count > 0;
        return false;
    }
}

/*public static class QuestState
{
    public static bool HasFlag(string flagId) => false; // 프로젝트 세이브 시스템과 연결
}

public static class Inventory
{
    public static bool HasItem(string itemId) => false;
    public static bool Consume(string itemId) => true;
}
*/
