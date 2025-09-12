#nullable enable
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Game.Quests
{
    /// <summary>
    /// 얇은 정적 퍼사드: 내부 SSOT인 QuestRuntime으로 위임.
    /// - 기존 코드에서 static QuestState.HasFlag(...)를 대체
    /// - 최적화: AggressiveInlining
    /// </summary>
    public static class QuestFlags
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(string? flagId)
            => QuestRuntime.Instance.HasFlag(flagId ?? string.Empty);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(int flagHash)
            => QuestRuntime.Instance.HasFlag(flagHash);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Set(string flagId)
            => QuestRuntime.Instance.SetFlag(flagId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Clear(string flagId)
            => QuestRuntime.Instance.ClearFlag(flagId);
    }
}
