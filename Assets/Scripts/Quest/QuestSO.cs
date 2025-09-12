using UnityEngine;
using System;

public enum ObjectiveType : byte
{
    InteractSet,        // Interact with each specified ID once
    InteractSequence,   // Interact in the specified order
    HoldOnTargets,      // Complete by holding on targets
    TriggerFlags,       // Complete when all required flags are raised

    StayInArea,         // Stay inside an area for N seconds (accumulated)
    Delivery            // Deliver an item to the target (optionally consume)
}

[CreateAssetMenu(menuName = "Quest/Quest")]
public sealed class QuestSO : ScriptableObject
{
    [Header("ID / Meta")]
    public uint id;
    public string title;
    [TextArea] public string description;

    [Header("Progress Policy")]
    [Tooltip("If enabled, objectives must be completed strictly in order (used by QuestManager).")]
    public bool sequentialObjectives = false;

    [Header("Objectives")]
    public ObjectiveDef[] objectives;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (objectives == null) return;
        for (int i = 0; i < objectives.Length; ++i)
            objectives[i].Validate();
    }
#endif
}

[Serializable]
public struct ObjectiveDef
{
    [Header("Display / Basics")]
    public string displayName;
    public ObjectiveType type;
    [Tooltip("If true, this objective is optional and is excluded from the quest's completion check.")]
    public bool optional;

    [Header("Targets / Sequence (Common)")]
    [Tooltip("Common target IDs for Interact/Sequence/Hold/Stay/Delivery.\nStayInArea: area IDs, Delivery: receiver IDs.")]
    public string[] targetIds;

    [Header("Sequence Options")]
    [Tooltip("Require targets to be completed in the defined order.")]
    public bool mustFollowOrder;
    [Tooltip("If the wrong order is attempted, reset the progress of this objective.")]
    public bool resetOnWrongOrder;

    [Header("Hold / Stay Options")]
    [Tooltip("Required hold duration (seconds) for HoldOnTargets.")]
    public float requiredHoldSeconds;   // HoldOnTargets
    [Tooltip("Required accumulated stay duration (seconds) for StayInArea.")]
    public float requiredStaySeconds;   // StayInArea
    [Tooltip("Grace time (seconds) to keep accumulated stay when briefly leaving the area.")]
    public float stayExitGraceSeconds;  // optional use
    [Tooltip("If true, leaving the area resets the accumulated stay time.")]
    public bool resetStayOnExit;

    [Header("Item / Delivery (Delivery/UseItem)")]
    [Tooltip("Item ID required for interaction/delivery (leave empty to ignore).")]
    public string requiredItemId;       // Interact/UseItem
    [Tooltip("Delivery specific: explicit item to deliver (falls back to requiredItemId if empty).")]
    public string deliveryItemId;       // Delivery (uses requiredItemId if empty)
    [Tooltip("If true, consume the item on delivery.")]
    public bool consumeOnDelivery;
    [Tooltip("Amount to consume when delivering (only used if consumeOnDelivery == true; <= 0 treated as 1).")]
    public int consumeAmount;

    [Header("Repetition / Count")]
    [Tooltip("Required number of completions.\n<= 0 uses type-specific defaults (e.g., InteractSet = all targets, others = 1).")]
    public int requiredCount;

    [Header("TriggerFlags Only")]
    [Tooltip("Complete when ALL of these flags are raised (used by QuestManager's flag re-check).")]
    public string[] requiredFlags;

    [Header("Completion / Failure Policy (Optional)")]
    [Tooltip("Time limit in seconds for this objective (0 = no limit).")]
    public float timeLimitSeconds;
    [Tooltip("If true and time limit elapses, mark this objective as failed (implementation up to the project).")]
    public bool failOnTimeout;

    [Header("Rewards on Completion (Optional)")]
    [Tooltip("Flags to raise when this objective completes.")]
    public string[] grantFlagsOnComplete;
    [Tooltip("Items to grant when this objective completes.")]
    public ItemReward[] grantItemsOnComplete;

    // --- Validation / Normalization ---
    public void Validate()
    {
        if (requiredHoldSeconds < 0f) requiredHoldSeconds = 0f;
        if (requiredStaySeconds < 0f) requiredStaySeconds = 0f;
        if (stayExitGraceSeconds < 0f) stayExitGraceSeconds = 0f;
        if (consumeAmount <= 0) consumeAmount = 1;
        if (resetOnWrongOrder) mustFollowOrder = true; // implies ordered sequence
        if (string.IsNullOrEmpty(deliveryItemId)) deliveryItemId = requiredItemId;
        if (timeLimitSeconds < 0f) timeLimitSeconds = 0f;

        if (targetIds == null) targetIds = Array.Empty<string>();
        if (requiredFlags == null) requiredFlags = Array.Empty<string>();
        if (grantFlagsOnComplete == null) grantFlagsOnComplete = Array.Empty<string>();
        if (grantItemsOnComplete == null) grantItemsOnComplete = Array.Empty<ItemReward>();
    }
}

[Serializable]
public struct ItemReward
{
    public string itemId;
    public int count;
}
