using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Meta.Model;

[Serializable]
public sealed class FirstPlayableSliceDefinition
{
    public int UnitBlueprintCap = 12;
    public int SignatureActiveCap = 12;
    public int SignaturePassiveCap = 12;
    // 에디터 contract(FirstPlayableAuthoringContract.LiveFlexActiveCap)와 수동 동기 — authored asset이 없는
    // fallback 경로에서 승격 콘텐츠가 잘리지 않도록 기본값도 live cap을 따른다.
    public int FlexActiveCap = 15;
    public int FlexPassiveCap = 20;
    // 에디터 contract(EquipmentContentV1Contract.LiveAffixCount)와 수동 동기 — authored asset이 없는
    // fallback 경로에서도 ratified live affix pool 전체를 유지한다.
    public int AffixCap = 44;
    public int SynergyFamilyCap = 7;
    public int TemporaryAugmentCap = 24;
    public int PermanentAugmentCap = 1;
    public int PassiveBoardCap = 4;

    public bool RequireAllThreatPatternsCovered = true;
    public bool RequireAllCounterToolsCovered = true;

    public IReadOnlyList<SliceCoverageQuota> CoverageQuotas = Array.Empty<SliceCoverageQuota>();
    public IReadOnlyList<string> UnitBlueprintIds = Array.Empty<string>();
    public IReadOnlyList<string> SignatureActiveIds = Array.Empty<string>();
    public IReadOnlyList<string> SignaturePassiveIds = Array.Empty<string>();
    public IReadOnlyList<string> FlexActiveIds = Array.Empty<string>();
    public IReadOnlyList<string> FlexPassiveIds = Array.Empty<string>();
    public IReadOnlyList<string> AffixIds = Array.Empty<string>();
    public IReadOnlyList<string> SynergyFamilyIds = Array.Empty<string>();
    public IReadOnlyList<string> TemporaryAugmentIds = Array.Empty<string>();
    public IReadOnlyList<string> PermanentAugmentIds = Array.Empty<string>();
    public IReadOnlyList<string> PassiveBoardIds = Array.Empty<string>();
    public IReadOnlyList<string> ParkingLotContentIds = Array.Empty<string>();

    // Migration convenience — combines both augment lists for existing callers
    public int AugmentCap => TemporaryAugmentCap + PermanentAugmentCap;
    public IReadOnlyList<string> AugmentIds =>
        TemporaryAugmentIds.Concat(PermanentAugmentIds).ToList();

    // V1 synergy grammar: race 2/4, class 2/3
    public IReadOnlyList<SynergyGrammarEntry> SynergyGrammar = Array.Empty<SynergyGrammarEntry>();

    // V1 class label mapping: canonical id -> player-facing label
    public IReadOnlyList<ClassLabelMapping> ClassLabelMappings = Array.Empty<ClassLabelMapping>();

    public bool Contains(ContentKind kind, string contentId)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return false;
        }

        var ids = kind switch
        {
            ContentKind.UnitBlueprint => UnitBlueprintIds,
            ContentKind.SignatureActive => SignatureActiveIds,
            ContentKind.SignaturePassive => SignaturePassiveIds,
            ContentKind.FlexActive => FlexActiveIds,
            ContentKind.FlexPassive => FlexPassiveIds,
            ContentKind.Affix => AffixIds,
            ContentKind.SynergyFamily => SynergyFamilyIds,
            ContentKind.TemporaryAugment => TemporaryAugmentIds,
            ContentKind.PermanentAugment => PermanentAugmentIds,
            ContentKind.PassiveBoard => PassiveBoardIds,
            _ => Array.Empty<string>(),
        };

        return ids.Contains(contentId, StringComparer.Ordinal);
    }
}

[Serializable]
public sealed class SynergyGrammarEntry
{
    public string FamilyId = string.Empty;
    public SynergyFamilyType FamilyType = SynergyFamilyType.Race;
    public int MinorThreshold = 2;
    public int MajorThreshold = 4;
}

public enum SynergyFamilyType
{
    Race = 0,
    Class = 1,
}

[Serializable]
public sealed class ClassLabelMapping
{
    public string CanonicalId = string.Empty;
    public string PlayerFacingLabel = string.Empty;
}
