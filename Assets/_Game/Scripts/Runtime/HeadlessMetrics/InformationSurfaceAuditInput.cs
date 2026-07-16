using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>truth/visible/token/feedback vocabulary를 auditor에 전달하는 순수 입력.</summary>
public sealed record InformationSurfaceAuditInput(
    IReadOnlyList<BuildGrammarAuditEdge> TruthEdges,
    IReadOnlyList<PlayerVisibleBuildGrammarSemantic> VisibleSemantics,
    IReadOnlyList<PlayerVisibleTokenUse> VisibleTokens,
    IReadOnlyList<string> AvailableFeedbackWitnesses)
{
    public static InformationSurfaceAuditInput Empty { get; } = new(
        Array.Empty<BuildGrammarAuditEdge>(),
        Array.Empty<PlayerVisibleBuildGrammarSemantic>(),
        Array.Empty<PlayerVisibleTokenUse>(),
        Array.Empty<string>());
}
