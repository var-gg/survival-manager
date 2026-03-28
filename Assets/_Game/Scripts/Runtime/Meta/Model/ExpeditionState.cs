using System.Collections.Generic;
using System.Linq;

namespace SM.Meta.Model;

public sealed class ExpeditionState
{
    public ExpeditionState(int currentNodeIndex = 0, IEnumerable<string>? temporaryAugmentIds = null)
    {
        CurrentNodeIndex = currentNodeIndex;
        TemporaryAugmentIds = temporaryAugmentIds?.ToList() ?? new List<string>();
    }

    public int CurrentNodeIndex { get; private set; }
    public List<string> TemporaryAugmentIds { get; }

    public void AdvanceNode() => CurrentNodeIndex++;
    public void AddTemporaryAugment(string id) => TemporaryAugmentIds.Add(id);
}
