using System.Collections.Generic;
using System.Linq;

namespace SM.Meta.Model;

public sealed class RosterState
{
    private readonly List<HeroRecord> _heroes;

    public RosterState(IEnumerable<HeroRecord>? heroes = null)
    {
        _heroes = heroes?.ToList() ?? new List<HeroRecord>();
    }

    public IReadOnlyList<HeroRecord> Heroes => _heroes;

    public void Add(HeroRecord hero) => _heroes.Add(hero);
}
