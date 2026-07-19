namespace SM.SealedLlmBridge;

/// <summary>Exact ordinal seam_type tokens used by the sealed capture core.</summary>
public static class SealedLlmSeamTypes
{
    public const string Deployment = "deployment";
    public const string Prep = "prep";
    public const string Reward = "reward";
    public const string Recruit = "recruit";
    public const string LevelNode = "level_node";
    public const string Passive = LevelNode;
    public const string Refit = "refit";
    public const string RunReport = "run_report";
}
