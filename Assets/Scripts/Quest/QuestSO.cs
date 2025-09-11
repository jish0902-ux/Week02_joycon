using UnityEngine;
using System;

public enum ObjectiveType { InteractSet /* 지정된 ID들을 각각 1번씩 상호작용 */ }

[CreateAssetMenu(menuName = "Quest/Quest")]
public sealed class QuestSO : ScriptableObject
{
    [Header("ID / Meta")]
    public uint id;
    public string title;
    [TextArea] public string description;
    public bool sequentialObjectives = false; // 목표를 순서대로만 완료할지

    [Header("Objectives")]
    public ObjectiveDef[] objectives;
}

[Serializable]
public struct ObjectiveDef
{
    public string displayName;     // UI용 문구(예: "상자 3개 조사")
    public ObjectiveType type;

    [Tooltip("InteractSet일 때 필요한 상호작용 ID들(예: Box_A, Box_B, Box_C)")]
    public string[] targetIds;     // 각 ID는 중앙 입력 스캐너가 Raise하는 문자열과 매칭
    public bool optional;          // 선택 목표 여부 (모두 완료가 아니어도 퀘스트 완료 가능)
}
