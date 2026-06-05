namespace SM.Unity;

/// <summary>
/// Authored timing markers for one one-shot action clip, expressed in normalized clip time (0..1).
/// The contact-pin scheduler aligns <see cref="ContactNorm"/> to the sim's canonical ContactTick so the
/// visual strike frame lands on the exact damage tick (GPT Pro C2 / J3). These are <em>authored</em>
/// values (per-clip metadata in production, an explicit graybox default table here) — never runtime
/// heuristics over keyframes (GPT Pro J18).
/// </summary>
public readonly record struct BattleClipTiming(
    float ContactNorm,
    float ReleaseNorm,
    float RecoveryNorm,
    bool CanContactPin,
    float RequiredFullWeightLeadSeconds,
    float RequiredFullWeightHoldSeconds)
{
    /// <summary>A non-pinnable clip (looping locomotion, held pose) whose start must not be time-shifted.</summary>
    public static readonly BattleClipTiming NonPinnable = new(0f, 0f, 0f, false, 0f, 0f);
}

/// <summary>
/// Explicit graybox default timing table, keyed by <see cref="BattleAnimationSemantic"/>. Every semantic
/// resolves to a value (no gaps — guarded by BattleClipTimingCatalogTests / J18). Production clips
/// override per-asset by GUID; this table is the deterministic fallback so the same recorded step stream
/// schedules identically across builds (the version travels in the replay/save contract, J26). Values
/// are authoring intent, not analysis:
/// <list type="bullet">
/// <item>melee strike lands near the swing midpoint (0.40);</item>
/// <item>bow looses late (0.70), spell releases earlier (0.45);</item>
/// <item>hit-reactions start on the contact frame (0.00).</item>
/// </list>
/// </summary>
public static class BattleClipTimingCatalog
{
    /// <summary>Bumped whenever the default table changes; included in the replay/save schedule contract
    /// so a recording made under an older table is detected rather than silently re-timed (GPT Pro J26).</summary>
    public const int CatalogVersion = 1;

    private const float DefaultLeadSeconds = 0.02f;
    private const float DefaultHoldSeconds = 0.04f;

    public static BattleClipTiming Resolve(BattleAnimationSemantic semantic)
    {
        return semantic switch
        {
            // Hit-reactions and death react on the contact frame itself.
            BattleAnimationSemantic.HitLight => Contact(0.00f, recovery: 0.45f),
            BattleAnimationSemantic.HitHeavy => Contact(0.00f, recovery: 0.55f),
            BattleAnimationSemantic.CriticalImpact => Contact(0.00f, recovery: 0.60f),
            BattleAnimationSemantic.BlockImpact => Contact(0.00f, recovery: 0.45f),
            BattleAnimationSemantic.Knockdown => Contact(0.00f, recovery: 0.70f),
            BattleAnimationSemantic.Dodge => Contact(0.00f, recovery: 0.50f),
            BattleAnimationSemantic.Miss => Contact(0.00f, recovery: 0.45f),
            BattleAnimationSemantic.Death => Contact(0.00f, recovery: 0.80f),

            // Melee commits strike around the swing midpoint; the lunge variant a touch later.
            BattleAnimationSemantic.None => Strike(contact: 0.40f),
            BattleAnimationSemantic.DashEngage => Strike(contact: 0.45f),

            // Ranged: release (the body's decisive marker) is the contact pin; the projectile travels
            // from release to the sim ContactTick separately (J12). Release == Contact at this layer.
            BattleAnimationSemantic.BowShot => Release(release: 0.70f),
            BattleAnimationSemantic.ProjectileCast => Release(release: 0.45f),

            // Windups are anticipation that completes into the commit; they pin their end to the windup
            // budget rather than a contact, so ContactNorm sits at the clip end.
            BattleAnimationSemantic.BowDraw => Anticipation(),
            BattleAnimationSemantic.ProjectileWindup => Anticipation(),

            // Locomotion loops and held poses are cadence/blend driven, never contact-pinned.
            BattleAnimationSemantic.BackstepDisengage => BattleClipTiming.NonPinnable,
            BattleAnimationSemantic.LateralStrafe => BattleClipTiming.NonPinnable,
            BattleAnimationSemantic.GuardPose => BattleClipTiming.NonPinnable,

            _ => Strike(contact: 0.40f),
        };
    }

    private static BattleClipTiming Strike(float contact)
    {
        return new BattleClipTiming(contact, contact, System.Math.Min(1f, contact + 0.2f), true, DefaultLeadSeconds, DefaultHoldSeconds);
    }

    private static BattleClipTiming Release(float release)
    {
        return new BattleClipTiming(release, release, System.Math.Min(1f, release + 0.15f), true, DefaultLeadSeconds, DefaultHoldSeconds);
    }

    private static BattleClipTiming Contact(float contact, float recovery)
    {
        return new BattleClipTiming(contact, contact, recovery, true, DefaultLeadSeconds, DefaultHoldSeconds);
    }

    private static BattleClipTiming Anticipation()
    {
        // Windup completes at the clip end; the contact happens on the following commit clip.
        return new BattleClipTiming(1f, 1f, 1f, true, DefaultLeadSeconds, DefaultHoldSeconds);
    }
}
