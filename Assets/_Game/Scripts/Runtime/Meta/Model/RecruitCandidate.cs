namespace SM.Meta.Model;

public sealed record RecruitCandidate(
    string Id,
    string ArchetypeId,
    string RaceId,
    string ClassId,
    string PositiveTraitId,
    string NegativeTraitId);
