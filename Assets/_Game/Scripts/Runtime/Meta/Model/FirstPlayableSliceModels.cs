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
    public int FlexActiveCap = 8;
    public int FlexPassiveCap = 8;
    public int AffixCap = 24;
    public int SynergyFamilyCap = 8;
    public int AugmentCap = 16;

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
    public IReadOnlyList<string> AugmentIds = Array.Empty<string>();
    public IReadOnlyList<string> ParkingLotContentIds = Array.Empty<string>();

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
            ContentKind.Augment => AugmentIds,
            _ => Array.Empty<string>(),
        };

        return ids.Contains(contentId, StringComparer.Ordinal);
    }
}
