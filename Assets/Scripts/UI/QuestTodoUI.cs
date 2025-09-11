using UnityEngine;
using TMPro;
using System.Text;

public sealed class QuestTodoUI : MonoBehaviour
{
    [SerializeField] private QuestManager manager;
    [SerializeField] private uint questId;
    [SerializeField] private TMP_Text text;

    const string CHECK = "☑";
    const string BOX = "☐";

    void OnEnable()
    {
        if (manager != null)
            manager.OnQuestUpdated += OnQuestUpdated;
        Redraw();
    }

    void OnDisable()
    {
        if (manager != null)
            manager.OnQuestUpdated -= OnQuestUpdated;
    }

    void OnQuestUpdated(uint changedId)
    {
        if (changedId == questId) Redraw();
    }

    void Redraw()
    {
        if (!manager || !text) return;
        if (!manager.TryGetSnapshot(questId, out var qs)) { text.text = ""; return; }

        var sb = new StringBuilder(256);
        sb.AppendLine(qs.so.title);

        for (int i = 0; i < qs.objectives.Length; ++i)
        {
            var o = qs.objectives[i];
            string mark = o.completed ? CHECK : BOX;
            sb.Append("- ").Append(mark).Append(' ').Append(o.def.displayName);
            if (o.def.optional) sb.Append(" (선택)");
            sb.AppendLine();

            // 서브태스크(예: 상자 3개)
            for (int s = 0; s < o.subs.Length; ++s)
            {
                var st = o.subs[s];
                sb.Append("   · ")
                  .Append(st.done ? CHECK : BOX)
                  .Append(' ')
                  .Append(st.targetId)
                  .AppendLine();
            }
        }

        if (qs.completed) sb.AppendLine(">> 완료!");
        text.text = sb.ToString();
    }
}
