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
    float MagnitudeScale = 1f,
    // 적용 시 상태 잔존 대신 즉시 보호막 전환 (barrier=true) — StatusId 문자열 분기의 콘텐츠 승격
    // (효과 종류 데이터화 3보 1슬라이스). 미등록/미저작 family는 false(=일반 상태 잔존).
    bool GrantsBarrierOnApply = false,
    // 보유 시 저지불가 — 하드 컨트롤 적용 면역 + 저작 강제이동(넉백/끌기) 면역 (unstoppable=true).
    // HasStatus("unstoppable") 문자열 조회의 콘텐츠 승격(효과 종류 데이터화 3보 3b). 미등록/미저작은 false.
    bool GrantsUnstoppable = false,
    // 보유 시 액티브 시전 차단 — ActiveSkill 전술 게이트 + signature/flex 슬롯 차단 (silence=true).
    // HasStatus("silence") 문자열 조회의 콘텐츠 승격(효과 종류 데이터화 3보 3c). 미등록/미저작은 false.
    bool BlocksActiveSkills = false,
    // 보유 시 자발 이동 차단 — MoveTowards/전진·후퇴 스텝 + 모빌리티 (root=true).
    // HasStatus("root") 문자열 조회의 콘텐츠 승격(효과 종류 데이터화 3보 3d). 미등록/미저작은 false.
    bool BlocksMovement = false,
    // 보유 시 행동 차단 — 턴 스킵 + 진행 액션 취소 + 기본공격/스킬/모빌리티/블록 게이트 (stun=true).
    // HasStatus("stun") 문자열 조회의 콘텐츠 승격(효과 종류 데이터화 3보 3e). 미등록/미저작은 false.
    bool BlocksAction = false,
    // ─ 채널 membership 5종(3보 3f, 합산 규칙: family 간 가산·family 내 Max·클램프 코드 소유) ─
    // 받는 피해 배수 가산 채널 소속 (marked/exposed=true) — magnitude×MagnitudeScale이 가산량.
    bool AmplifiesIncomingDamage = false,
    // 받는 피해 delta 채널 소속 (guarded=true) — IncomingDamageDelta가 가산량(방어 boon).
    bool GrantsGuardedDefense = false,
    // 방어/저항 차감 채널 소속 (sunder=true) — magnitude×MagnitudeScale이 차감량.
    bool ShredsDefense = false,
    // 치유 감소 채널 소속 (wound=true) — magnitude×MagnitudeScale이 감소율.
    bool ReducesHealing = false,
    // 공속/이속 감쇠 채널 소속 (slow=true) — magnitude×MagnitudeScale이 감쇠율.
    bool DampensTempo = false,
    // 타게팅 표식 membership (marked=true) — MarkedEnemy 셀렉터/RequireMarked 필터/포커스 캡 상향/
    // 팀 블랙보드 포커스 점수. HasStatus("marked") 타게팅 조회의 콘텐츠 승격(3보 3g, 최종 sim 슬라이스).
    bool MarksTarget = false);

/// <summary>채널 membership 엔트리 — kind를 가진 family의 (상태 id, 채널 값) 쌍. CombatStatusRules가
/// 생성 시 id ordinal 정렬로 파생(가산 순서 고정 = IEEE 결정성)해 UnitSnapshot이 스냅샷한다(3보 3f).</summary>
public readonly record struct StatusChannelEntry(string StatusId, float Value)
{
    public override string ToString() => $"{StatusId}:{Value}";
}

// GrantedStatusId 기본값은 과거 ApplyCleanse의 "unstoppable" 리터럴과 동일 — 미저작/손조립 레인
// byte-identity 계약(1보 `?? -0.1f` 패턴의 문자열 판). 저지불가 kind 보유 파생 상태로 교체 저작 가능.
public sealed record CombatCleanseProfileRule(
    string Id,
    IReadOnlyList<string> RemovesStatusIds,
    bool RemovesOneHardControl,
    bool GrantsUnstoppable,
    float GrantedUnstoppableDurationSeconds,
    string GrantedStatusId = "unstoppable");

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
        UnstoppableStatusIds = DeriveKindStatusIds(StatusFamilies, static rule => rule.GrantsUnstoppable);
        BlocksActiveSkillsStatusIds = DeriveKindStatusIds(StatusFamilies, static rule => rule.BlocksActiveSkills);
        BlocksMovementStatusIds = DeriveKindStatusIds(StatusFamilies, static rule => rule.BlocksMovement);
        BlocksActionStatusIds = DeriveKindStatusIds(StatusFamilies, static rule => rule.BlocksAction);
        MarksTargetStatusIds = DeriveKindStatusIds(StatusFamilies, static rule => rule.MarksTarget);
        AmplifyIncomingDamageEntries = DeriveChannelEntries(StatusFamilies, static rule => rule.AmplifiesIncomingDamage, static rule => rule.MagnitudeScale);
        GuardedDefenseEntries = DeriveChannelEntries(StatusFamilies, static rule => rule.GrantsGuardedDefense, static rule => rule.IncomingDamageDelta);
        ShredsDefenseEntries = DeriveChannelEntries(StatusFamilies, static rule => rule.ShredsDefense, static rule => rule.MagnitudeScale);
        ReducesHealingEntries = DeriveChannelEntries(StatusFamilies, static rule => rule.ReducesHealing, static rule => rule.MagnitudeScale);
        DampensTempoEntries = DeriveChannelEntries(StatusFamilies, static rule => rule.DampensTempo, static rule => rule.MagnitudeScale);
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
        UnstoppableStatusIds = DeriveKindStatusIds(StatusFamilies, static rule => rule.GrantsUnstoppable);
        BlocksActiveSkillsStatusIds = DeriveKindStatusIds(StatusFamilies, static rule => rule.BlocksActiveSkills);
        BlocksMovementStatusIds = DeriveKindStatusIds(StatusFamilies, static rule => rule.BlocksMovement);
        BlocksActionStatusIds = DeriveKindStatusIds(StatusFamilies, static rule => rule.BlocksAction);
        MarksTargetStatusIds = DeriveKindStatusIds(StatusFamilies, static rule => rule.MarksTarget);
        AmplifyIncomingDamageEntries = DeriveChannelEntries(StatusFamilies, static rule => rule.AmplifiesIncomingDamage, static rule => rule.MagnitudeScale);
        GuardedDefenseEntries = DeriveChannelEntries(StatusFamilies, static rule => rule.GrantsGuardedDefense, static rule => rule.IncomingDamageDelta);
        ShredsDefenseEntries = DeriveChannelEntries(StatusFamilies, static rule => rule.ShredsDefense, static rule => rule.MagnitudeScale);
        ReducesHealingEntries = DeriveChannelEntries(StatusFamilies, static rule => rule.ReducesHealing, static rule => rule.MagnitudeScale);
        DampensTempoEntries = DeriveChannelEntries(StatusFamilies, static rule => rule.DampensTempo, static rule => rule.MagnitudeScale);
    }

    public IReadOnlyDictionary<string, CombatStatusFamilyRule> StatusFamilies { get; }
    public IReadOnlyDictionary<string, CombatCleanseProfileRule> CleanseProfiles { get; }
    public CombatControlDiminishingRule ControlDiminishing { get; }

    // kind별 상태 id set — 생성 시 1회 eager 파생, 이후 불변(변이 금지). UnitSnapshot이 생성자에서 참조를
    // 스냅샷해 핫패스 게터(IsUnstoppable/IsSilenced 등)가 사전 조회 없이 Contains만 소비한다(효과 종류 데이터화 3보).
    internal HashSet<string> UnstoppableStatusIds { get; }
    internal HashSet<string> BlocksActiveSkillsStatusIds { get; }
    internal HashSet<string> BlocksMovementStatusIds { get; }
    internal HashSet<string> BlocksActionStatusIds { get; }
    internal HashSet<string> MarksTargetStatusIds { get; }
    // 채널 membership 엔트리(3f) — id ordinal 정렬 배열(가산 순서 고정), 생성 시 1회 파생·불변(변이 금지).
    internal StatusChannelEntry[] AmplifyIncomingDamageEntries { get; }
    internal StatusChannelEntry[] GuardedDefenseEntries { get; }
    internal StatusChannelEntry[] ShredsDefenseEntries { get; }
    internal StatusChannelEntry[] ReducesHealingEntries { get; }
    internal StatusChannelEntry[] DampensTempoEntries { get; }

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

    /// <summary>적용 시 상태 잔존 대신 즉시 보호막으로 전환하는 family 인가 — 콘텐츠(StatusFamilyDefinition)
    /// 효과 종류 서술자(3보 1슬라이스). 미등록 family는 false(=일반 상태 잔존).</summary>
    public bool ResolveGrantsBarrierOnApply(string statusId)
        => TryGetStatusFamily(statusId, out var rule) && rule.GrantsBarrierOnApply;

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

    private static HashSet<string> DeriveKindStatusIds(
        IReadOnlyDictionary<string, CombatStatusFamilyRule> statusFamilies,
        Func<CombatStatusFamilyRule, bool> hasKind)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in statusFamilies.Values)
        {
            if (hasKind(rule))
            {
                ids.Add(rule.Id);
            }
        }

        return ids;
    }

    private static StatusChannelEntry[] DeriveChannelEntries(
        IReadOnlyDictionary<string, CombatStatusFamilyRule> statusFamilies,
        Func<CombatStatusFamilyRule, bool> hasKind,
        Func<CombatStatusFamilyRule, float> channelValue)
    {
        return statusFamilies.Values
            .Where(hasKind)
            .OrderBy(rule => rule.Id, StringComparer.Ordinal)
            .Select(rule => new StatusChannelEntry(rule.Id, channelValue(rule)))
            .ToArray();
    }

    private static CombatStatusRules CreateDefault()
    {
        var families = new[]
            {
                new CombatStatusFamilyRule("stun", StatusGroupValue.Control, true, true, true, 1f, false, false, new[] { "stun" }, "vfx.status_stun", BlocksAction: true),
                new CombatStatusFamilyRule("root", StatusGroupValue.Control, true, true, true, 1f, false, false, new[] { "root" }, "vfx.status_root", BlocksMovement: true),
                new CombatStatusFamilyRule("silence", StatusGroupValue.Control, true, true, true, 0.5f, false, false, new[] { "silence" }, "vfx.status_silence", BlocksActiveSkills: true),
                new CombatStatusFamilyRule("slow", StatusGroupValue.Control, false, false, false, 0f, false, false, new[] { "slow" }, "vfx.status_slow", DampensTempo: true),
                new CombatStatusFamilyRule("burn", StatusGroupValue.Attrition, false, false, false, 0f, true, false, new[] { "burn" }, "vfx.status_burn"),
                new CombatStatusFamilyRule("bleed", StatusGroupValue.Attrition, false, false, false, 0f, true, false, new[] { "bleed" }, "vfx.status_bleed"),
                new CombatStatusFamilyRule("wound", StatusGroupValue.Attrition, false, false, false, 0f, false, false, new[] { "wound" }, "vfx.status_wound", ReducesHealing: true),
                new CombatStatusFamilyRule("sunder", StatusGroupValue.Attrition, false, false, false, 0f, false, false, new[] { "sunder" }, "vfx.status_sunder", ShredsDefense: true),
                new CombatStatusFamilyRule("marked", StatusGroupValue.TacticalMark, false, false, false, 0f, false, false, new[] { "marked" }, "vfx.status_marked", AmplifiesIncomingDamage: true, MarksTarget: true),
                new CombatStatusFamilyRule("exposed", StatusGroupValue.TacticalMark, false, false, false, 0f, false, false, new[] { "exposed" }, "vfx.status_exposed", AmplifiesIncomingDamage: true),
                new CombatStatusFamilyRule("barrier", StatusGroupValue.DefensiveBoon, false, false, false, 0f, false, false, new[] { "barrier" }, "vfx.status_barrier", GrantsBarrierOnApply: true),
                new CombatStatusFamilyRule("guarded", StatusGroupValue.DefensiveBoon, false, false, false, 0f, false, false, new[] { "guarded" }, "vfx.status_guarded", IncomingDamageDelta: -0.1f, GrantsGuardedDefense: true),
                new CombatStatusFamilyRule("unstoppable", StatusGroupValue.DefensiveBoon, false, false, false, 0f, false, false, new[] { "unstoppable" }, "vfx.status_unstoppable", GrantsUnstoppable: true),
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
