using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Editor.Validation;

internal sealed record H100BattleScreeningMember(
    string ArchetypeId,
    DeploymentAnchorId Anchor);

internal sealed record H100BattleScreeningCase(
    string CaseId,
    string BuildId,
    string MedoidSignature,
    int Seed,
    IReadOnlyList<H100BattleScreeningMember> Members);
