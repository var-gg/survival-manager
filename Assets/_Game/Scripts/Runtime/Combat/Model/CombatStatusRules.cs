using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;

namespace SM.Combat.Model;

public sealed record CombatStatusFamilyRule(
    string Id,
    StatusGroupValue Group,
    bool IsHardControl,
    bool UsesControlDiminishing,
    bool AffectedByTenacity,
    float TenacityScale,
    bool AppliesPeriodicDamage,
    bool IsRuleModifierOnly,
    IReadOnlyList<string>? CompileTags = null,
    string VfxCueId = "",
    // 이 상태가 유닛의 받는 피해 배수에 주는 delta (guarded=-0.1) — sim 리터럴의 콘텐츠 승격.
    float IncomingDamageDelta = 0f,
    // 적용 magnitude가 이 상태의 숫자 채널에 실리는 배율 (sunder=방어/저항 차감, marked/exposed=받는 피해 가산,
    // wound=치유 감소, slow=공속/이속 감쇠). 1=magnitude 직소비(현행 공식) — 숫자 콘텐츠화 2보.
    float MagnitudeScale = 1f);

public sealed record CombatCleanseProfileRule(
    string Id,
    IReadOnlyList<string> RemovesStatusIds,
    bool RemovesOneHardControl,
    bool GrantsUnstoppable,
    float GrantedUnstoppableDurationSeconds);

public sealed record CombatControlDiminishingRule(
    string Id,
    float ControlResistMultiplier,
    float WindowSeconds,
    IReadOnlyList<string> FullTenacityStatusIds,
    IReadOnlyList<string> PartialTenacityStatusIds);

public sealed class CombatStatusRules
{
    public static readonly CombatControlDiminishingRule DefaultControlDiminishing = new(
        "control_diminishing_launch_floor",
        0.5f,
        1.5f,
        new[] { "stun", "root" },
        new[] { "silence" });

    public static CombatStatusRules Default { get; } = CreateDefault();

    public CombatStatusRules(
        IReadOnlyDictionary<string, CombatStatusFamilyRule>? statusFamilies,
        IReadOnlyDictionary<string, CombatCleanseProfileRule>? cleanseProfiles,
        CombatControlDiminishingRule? controlDiminishing)
    {
        StatusFamilies = statusFamilies ?? Default.StatusFamilies;
        CleanseProfiles = cleanseProfiles ?? Default.CleanseProfiles;
        ControlDiminishing = controlDiminishing ?? Default.ControlDiminishing;
    }

    private CombatStatusRules(
        IReadOnlyDictionary<string, CombatStatusFamilyRule> statusFamilies,
        IReadOnlyDictionary<string, CombatCleanseProfileRule> cleanseProfiles,
        CombatControlDiminishingRule controlDiminishing,
        bool _)
    {
        StatusFamilies = statusFamilies;
        CleanseProfiles = cleanseProfiles;
        ControlDiminishing = controlDiminishing;
    }

    public IReadOnlyDictionary<string, CombatStatusFamilyRule> StatusFamilies { get; }
    public IReadOnlyDictionary<string, CombatCleanseProfileRule> CleanseProfiles { get; }
    public CombatControlDiminishingRule ControlDiminishing { get; }

    public bool TryGetStatusFamily(string statusId, out CombatStatusFamilyRule rule)
    {
        if (!string.IsNullOrWhiteSpace(statusId) && StatusFamilies.TryGetValue(statusId, out rule!))
        {
            return true;
        }

        rule = null!;
        return false;
    }

    public bool TryGetCleanseProfile(string profileId, out CombatCleanseProfileRule rule)
    {
        if (!string.IsNullOrWhiteSpace(profileId) && CleanseProfiles.TryGetValue(profileId, out rule!))
        {
            return true;
        }

        rule = null!;
        return false;
    }

    public bool IsHardControl(string statusId)
        => TryGetStatusFamily(statusId, out var rule) && rule.IsHardControl;

    public bool AppliesPeriodicDamage(string statusId)
        => TryGetStatusFamily(statusId, out var rule) && rule.AppliesPeriodicDamage;

    /// <summary>해당 상태가 받는 피해 배수에 주는 delta — 콘텐츠(StatusFamilyDefinition) 튜닝값.</summary>
    public float ResolveIncomingDamageDelta(string statusId)
        => TryGetStatusFamily(statusId, out var rule) ? rule.IncomingDamageDelta : 0f;

    /// <summary>적용 magnitude가 해당 상태의 숫자 채널에 실리는 배율 — 콘텐츠(StatusFamilyDefinition) 튜닝값.
    /// 미등록 family는 1(=magnitude 직소비, 현행 공식 보존).</summary>
    public float ResolveMagnitudeScale(string statusId)
        => TryGetStatusFamily(statusId, out var rule) ? rule.MagnitudeScale : 1f;

    public float ResolveTenacityScale(string statusId)
    {
        if (!TryGetStatusFamily(statusId, out var statusRule) || !statusRule.AffectedByTenacity)
        {
            return 0f;
        }

        if (ControlDiminishing.FullTenacityStatusIds.Contains(statusId, StringComparer.Ordinal))
        {
            return 1f;
        }

        if (ControlDiminishing.PartialTenacityStatusIds.Contains(statusId, StringComparer.Ordinal))
        {
            return Math.Clamp(statusRule.TenacityScale, 0f, 1f);
        }

        return Math.Clamp(statusRule.TenacityScale, 0f, 1f);
    }

    private static CombatStatusRules CreateDefault()
    {
        var families = new[]
            {
                new CombatStatusFamilyRule("stun", StatusGroupValue.Control, true, true, true, 1f, false, false, new[] { "stun" }, "vfx.status_stun"),
                new CombatStatusFamilyRule("root", StatusGroupValue.Control, true, true, true, 1f, false, false, new[] { "root" }, "vfx.status_root"),
                new CombatStatusFamilyRule("silence", StatusGroupValue.Control, true, true, true, 0.5f, false, false, new[] { "silence" }, "vfx.status_silence"),
                new CombatStatusFamilyRule("slow", StatusGroupValue.Control, false, false, false, 0f, false, false, new[] { "slow" }, "vfx.status_slow"),
                new CombatStatusFamilyRule("burn", StatusGroupValue.Attrition, false, false, false, 0f, true, false, new[] { "burn" }, "vfx.status_burn"),
                new CombatStatusFamilyRule("bleed", StatusGroupValue.Attrition, false, false, false, 0f, true, false, new[] { "bleed" }, "vfx.status_bleed"),
                new CombatStatusFamilyRule("wound", StatusGroupValue.Attrition, false, false, false, 0f, false, false, new[] { "wound" }, "vfx.status_wound"),
                new CombatStatusFamilyRule("sunder", StatusGroupValue.Attrition, false, false, false, 0f, false, false, new[] { "sunder" }, "vfx.status_sunder"),
                new CombatStatusFamilyRule("marked", StatusGroupValue.TacticalMark, false, false, false, 0f, false, false, new[] { "marked" }, "vfx.status_marked"),
                new CombatStatusFamilyRule("exposed", StatusGroupValue.TacticalMark, false, false, false, 0f, false, false, new[] { "exposed" }, "vfx.status_exposed"),
                new CombatStatusFamilyRule("barrier", StatusGroupValue.DefensiveBoon, false, false, false, 0f, false, false, new[] { "barrier" }, "vfx.status_barrier"),
                new CombatStatusFamilyRule("guarded", StatusGroupValue.DefensiveBoon, false, false, false, 0f, false, false, new[] { "guarded" }, "vfx.status_guarded", IncomingDamageDelta: -0.1f),
                new CombatStatusFamilyRule("unstoppable", StatusGroupValue.DefensiveBoon, false, false, false, 0f, false, false, new[] { "unstoppable" }, "vfx.status_unstoppable"),
            }
            .ToDictionary(rule => rule.Id, StringComparer.Ordinal);
        var cleanse = new[]
            {
                new CombatCleanseProfileRule("cleanse_basic", new[] { "slow", "burn", "bleed", "wound", "sunder", "marked", "exposed" }, false, false, 0f),
                new CombatCleanseProfileRule("cleanse_control", new[] { "root", "silence", "slow", "burn", "bleed", "wound", "sunder", "marked", "exposed" }, false, false, 0f),
                new CombatCleanseProfileRule("break_and_unstoppable", new[] { "stun", "root", "silence" }, true, true, 0.8f),
            }
            .ToDictionary(rule => rule.Id, StringComparer.Ordinal);
        return new CombatStatusRules(families, cleanse, DefaultControlDiminishing, true);
    }
}
