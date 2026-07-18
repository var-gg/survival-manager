using System;
using SM.HeadlessMetrics;

namespace SM.SealedLlmBridge;

/// <summary>봉인 decision의 decode/guard 실패를 scripted fallback 없이 capture runner까지 운반한다.</summary>
public sealed class SealedLlmTerminalFailureException : Exception
{
    public SealedLlmTerminalFailureException(
        SealedDecisionSeamKey seamKey,
        Exception innerException)
        : base(
            $"Sealed LLM decision {Format(seamKey)} failed terminal decode or guard validation.",
            innerException ?? throw new ArgumentNullException(nameof(innerException)))
    {
        SeamKey = seamKey ?? throw new ArgumentNullException(nameof(seamKey));
    }

    public SealedDecisionSeamKey SeamKey { get; }

    private static string Format(SealedDecisionSeamKey? seamKey)
        => seamKey == null
            ? "<null>"
            : $"{seamKey.DecisionIndex}:{seamKey.SeamType}:{seamKey.Ordinal}";
}
