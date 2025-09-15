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

    public void SetQuest(uint newId)
    {
        questId = newId;
        Redraw(); // 변경 즉시 UI 갱신
    }



    void OnQuestUpdated(uint changedId)
    {
        if (changedId == questId) Redraw();
    }

    void Redraw()
    {
        if (!manager || !ContentsText) return;
        if (!manager.TryGetSnapshot(questId, out var qs))
        {
            ContentsText.text = "";
            return;
        }

        var sb = new StringBuilder(256); // 본문
        var tb = new StringBuilder(256); // 제목

        // 제목: 전체 완료 시 취소선
        AppendLineWithStrike(tb, qs.so.title, qs.completed);

        for (int i = 0; i < qs.objectives.Length; ++i)
        {
            var o = qs.objectives[i];

            sb.Append("- ");
            if (o.completed)
            {
                sb.Append(S_OPEN).Append(o.def.displayName);
                if (o.def.optional) sb.Append(" (선택)");
                sb.Append(S_CLOSE);
            }
            else
            {
                sb.Append(o.def.displayName);
                if (o.def.optional) sb.Append(" (선택)");
            }
            sb.AppendLine();

            // --- 서브태스크 출력 (targetId가 '_'로 시작하면 숨김) ---
            var subs = o.subs;
            if (subs != null && subs.Length > 0)
            {
                bool hasVisible = false;
                for (int s = 0; s < subs.Length; ++s)
                {
                    var tid = subs[s].targetId;
                    if (!string.IsNullOrEmpty(tid) && !tid.StartsWith("_"))
                    {
                        hasVisible = true;
                        break;
                    }
                }

                if (hasVisible)
                {
                    sb.Append("   · ( ");
                    bool firstPrinted = false;

                    for (int s = 0; s < subs.Length; ++s)
                    {
                        var st = subs[s];
                        var tid = st.targetId;

                        // 숨김 규칙
                        if (string.IsNullOrEmpty(tid) || tid.StartsWith("_"))
                            continue;

                        if (firstPrinted) sb.Append(", ");
                        firstPrinted = true;

                        if (st.done)
                            sb.Append(S_OPEN).Append(tid).Append(S_CLOSE);
                        else
                            sb.Append(tid);
                    }

                    sb.Append(" )");
                    sb.AppendLine();
                }
                // hasVisible == false면 아무 것도 추가하지 않음
            }
            // subs가 없으면 아무 것도 추가하지 않음
        }


        if (Titletext) { Titletext.richText = true; Titletext.text = tb.ToString(); }
        ContentsText.richText = true;
        ContentsText.text = sb.ToString();
    }

    static void AppendLineWithStrike(StringBuilder sb, string str, bool strike)
    {
        if (strike)
        {
            sb.Append(S_OPEN).Append(str).Append(S_CLOSE);
            sb.Append("              >> 완료!").AppendLine();
        }
        else
        {
            sb.AppendLine(str);
        }
    }
}
