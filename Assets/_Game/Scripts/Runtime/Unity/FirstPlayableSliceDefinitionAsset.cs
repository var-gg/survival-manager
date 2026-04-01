using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;
using UnityEngine;

namespace SM.Unity;

[CreateAssetMenu(menuName = "SM/Definitions/First Playable Slice", fileName = "first_playable_slice")]
public sealed class FirstPlayableSliceDefinitionAsset : ScriptableObject
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

    public System.Collections.Generic.List<SliceCoverageQuota> CoverageQuotas = new();
    public System.Collections.Generic.List<string> UnitBlueprintIds = new();
    public System.Collections.Generic.List<string> SignatureActiveIds = new();
    public System.Collections.Generic.List<string> SignaturePassiveIds = new();
    public System.Collections.Generic.List<string> FlexActiveIds = new();
    public System.Collections.Generic.List<string> FlexPassiveIds = new();
    public System.Collections.Generic.List<string> AffixIds = new();
    public System.Collections.Generic.List<string> SynergyFamilyIds = new();
    public System.Collections.Generic.List<string> AugmentIds = new();
    public System.Collections.Generic.List<string> ParkingLotContentIds = new();

    public FirstPlayableSliceDefinition ToRuntime()
    {
        return new FirstPlayableSliceDefinition
        {
            UnitBlueprintCap = UnitBlueprintCap,
            SignatureActiveCap = SignatureActiveCap,
            SignaturePassiveCap = SignaturePassiveCap,
            FlexActiveCap = FlexActiveCap,
            FlexPassiveCap = FlexPassiveCap,
            AffixCap = AffixCap,
            SynergyFamilyCap = SynergyFamilyCap,
            AugmentCap = AugmentCap,
            RequireAllThreatPatternsCovered = RequireAllThreatPatternsCovered,
            RequireAllCounterToolsCovered = RequireAllCounterToolsCovered,
            CoverageQuotas = CoverageQuotas
                .Select(quota => new SliceCoverageQuota
                {
                    Kind = quota.Kind,
                    MinimumCount = quota.MinimumCount,
                })
                .ToList(),
            UnitBlueprintIds = UnitBlueprintIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(System.StringComparer.Ordinal).ToList(),
            SignatureActiveIds = SignatureActiveIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(System.StringComparer.Ordinal).ToList(),
            SignaturePassiveIds = SignaturePassiveIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(System.StringComparer.Ordinal).ToList(),
            FlexActiveIds = FlexActiveIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(System.StringComparer.Ordinal).ToList(),
            FlexPassiveIds = FlexPassiveIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(System.StringComparer.Ordinal).ToList(),
            AffixIds = AffixIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(System.StringComparer.Ordinal).ToList(),
            SynergyFamilyIds = SynergyFamilyIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(System.StringComparer.Ordinal).ToList(),
            AugmentIds = AugmentIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(System.StringComparer.Ordinal).ToList(),
            ParkingLotContentIds = ParkingLotContentIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(System.StringComparer.Ordinal).ToList(),
        };
    }
}
