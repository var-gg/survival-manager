using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessCensus;

/// <summary>정책 assembly가 참조할 수 없는 evaluator-only build 문법 그래프.</summary>
public sealed class BuildGrammarTruthGraph
{
    public BuildGrammarTruthGraph(IEnumerable<BuildGrammarTruthEdge> edges)
    {
        Edges = (edges ?? Array.Empty<BuildGrammarTruthEdge>())
            .OrderBy(edge => edge.EdgeId, StringComparer.Ordinal)
            .ToArray();
        if (Edges.Select(edge => edge.EdgeId).Distinct(StringComparer.Ordinal).Count() != Edges.Count)
        {
            throw new ArgumentException("Build grammar edge ids must be unique.", nameof(edges));
        }
    }

    public IReadOnlyList<BuildGrammarTruthEdge> Edges { get; }
}
