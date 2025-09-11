using UnityEngine;
using System;

public enum ObjectiveType { InteractSet /* ������ ID���� ���� 1���� ��ȣ�ۿ� */ }

[CreateAssetMenu(menuName = "Quest/Quest")]
public sealed class QuestSO : ScriptableObject
{
    [Header("ID / Meta")]
    public uint id;
    public string title;
    [TextArea] public string description;
    public bool sequentialObjectives = false; // ��ǥ�� ������θ� �Ϸ�����

    [Header("Objectives")]
    public ObjectiveDef[] objectives;
}

[Serializable]
public struct ObjectiveDef
{
    public string displayName;     // UI�� ����(��: "���� 3�� ����")
    public ObjectiveType type;

    [Tooltip("InteractSet�� �� �ʿ��� ��ȣ�ۿ� ID��(��: Box_A, Box_B, Box_C)")]
    public string[] targetIds;     // �� ID�� �߾� �Է� ��ĳ�ʰ� Raise�ϴ� ���ڿ��� ��Ī
    public bool optional;          // ���� ��ǥ ���� (��� �Ϸᰡ �ƴϾ ����Ʈ �Ϸ� ����)
}
