namespace SM.HeadlessCensus;

public sealed record BuildSpaceFlag(
    string Id,
    string Severity,
    int AffectedBuildCount,
    string Message);
