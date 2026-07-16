namespace SM.HeadlessMetrics;

/// <summary>가시 fact 안에서 사용되거나 설명된 status/tag/rule token.</summary>
public sealed record PlayerVisibleTokenUse(
    string SourceFactId,
    string TokenKind,
    string TokenId,
    bool IsDefinition);
