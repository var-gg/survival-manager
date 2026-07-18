using System;

namespace SM.SealedLlmBridge;

public enum SealedLlmActionDecodeReason
{
    NullAction = 0,
    MalformedGrammar = 1,
    NonCanonicalOrder = 2,
    DuplicateAssignment = 3,
    OffMenu = 4,
}

/// <summary>selected_action이 canonical grammar 또는 현재 legal menu를 통과하지 못했음을 나타낸다.</summary>
public sealed class SealedLlmActionDecodeException : Exception
{
    public SealedLlmActionDecodeException(
        SealedLlmActionDecodeReason reason,
        string selectedAction,
        string message,
        Exception innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
        SelectedAction = selectedAction;
    }

    public SealedLlmActionDecodeReason Reason { get; }
    public string SelectedAction { get; }
}
