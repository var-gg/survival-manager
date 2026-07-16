namespace SM.HeadlessCensus;

/// <summary>레시피나 시스템 매핑을 소유하지 않는 owner fantasy anchor.</summary>
public sealed record OwnerConceptAnchor(
    string AnchorId,
    string DisplayName,
    string Fantasy,
    bool RatificationPending);
