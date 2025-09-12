using UnityEngine;
using TMPro;
using System.Text;

public sealed class QuestTodoUI : MonoBehaviour
{
    [SerializeField] private QuestManager manager;
    [SerializeField] private uint questId;
    [SerializeField] private TMP_Text Titletext;
    [SerializeField] private TMP_Text ContentsText;

    // 취소선 태그
    const string S_OPEN = "<s>";
    const string S_CLOSE = "</s>";

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
        if (!manager || !ContentsText) return;
        if (!manager.TryGetSnapshot(questId, out var qs)) { ContentsText.text = ""; return; }

        // 제목: 퀘스트가 전부 완료되면 제목에도 취소선
        var sb = new StringBuilder(256);
        var tb = new StringBuilder(256);

        AppendLineWithStrike(tb, qs.so.title, qs.completed);

        for (int i = 0; i < qs.objectives.Length; ++i)
        {
            var o = qs.objectives[i];

            sb.Append("- ");

            if (o.completed)
            {
                sb.Append(S_OPEN);
                sb.Append(o.def.displayName);
                if (o.def.optional) sb.Append(" (선택)");
                sb.Append(S_CLOSE);
            }
            else
            {
                sb.Append(o.def.displayName);
                if (o.def.optional) sb.Append(" (선택)");
            }

            sb.AppendLine();

            // 서브태스크(예: 상자 3개 -> 각각 targetId 표시)
            for (int s = 0; s < o.subs.Length; ++s)
            {
                if(s == 0)
                {
                    sb.Append("   · ");

                    sb.Append("( ");
                }
                var st = o.subs[s];
                if (st.done)
                {
                    sb.Append(S_OPEN);
                    sb.Append(st.targetId);
                    sb.Append(S_CLOSE);
                }
                else
                {
                    sb.Append(st.targetId);
                }
                
                if(s != o.subs.Length - 1)
                    sb.Append(", ");



            }

            sb.Append(" )");

            sb.AppendLine();
        }

        if (qs.completed) tb.Append(">> 완료!");

        Titletext.richText = true;
        Titletext.text = tb.ToString();


        ContentsText.richText = true; // 중요: 리치 텍스트 켜기
        ContentsText.text = sb.ToString();
    }

    static void AppendLineWithStrike(StringBuilder sb, string str, bool strike)
    {
        if (strike)
        {
            sb.Append(S_OPEN).Append(str).Append(S_CLOSE).AppendLine();
        }
        else
        {
            sb.AppendLine(str);
        }
    }
}
