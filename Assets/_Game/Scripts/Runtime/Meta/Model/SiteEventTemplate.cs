using System.Collections.Generic;
using SM.Core.Content;

namespace SM.Meta.Model;

public sealed record SiteEventTemplate(
    string Id,
    string SiteId,
    string SetupKey,
    IReadOnlyList<SiteEventChoiceTemplate> Choices);

public sealed record SiteEventChoiceTemplate(
    string Id,
    string LabelKey,
    IReadOnlyList<SiteEventOutcomeTemplate> Outcomes);

public sealed record SiteEventOutcomeTemplate(
    OutcomeKind Kind,
    string PayloadId,
    string AuxiliaryId,
    int Amount,
    OutcomeTargetRule TargetRule);

public sealed record SiteEventItemGrant(
    string ItemBaseId,
    string AffixId);

public sealed record SiteEventRecruitOffer(
    string ArchetypeId,
    string NegativeTraitId);

public sealed record SiteEventLegalAction(
    string EventId,
    string ChoiceId);
