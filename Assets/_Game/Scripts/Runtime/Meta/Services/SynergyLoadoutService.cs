using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class SynergyLoadoutService
{
    public static IReadOnlyList<CombatModifierPackage> BuildTeamPackages(
        IReadOnlyList<BattleUnitLoadout> units,
        CombatContentSnapshot content)
    {
        var rules = content.SynergyCatalog.Values.Select(template => template.Rule).ToList();
        return rules.Count == 0
            ? SynergyService.BuildForTeam(units)
            : SynergyService.BuildForTeam(units, rules);
    }
}
