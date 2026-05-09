using System;
using System.Collections.Generic;
using SM.Core.Contracts;
using SM.Meta.Model;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class HeroInstanceRecord
{
    public string HeroId = string.Empty;
    public string Name = string.Empty;
    public string CharacterId = string.Empty;
    public string ArchetypeId = string.Empty;
    public string RaceId = string.Empty;
    public string ClassId = string.Empty;
    public string PositiveTraitId = string.Empty;
    public string NegativeTraitId = string.Empty;
    public string FlexActiveId = string.Empty;
    public string FlexPassiveId = string.Empty;
    public RecruitTier RecruitTier = RecruitTier.Common;
    public RecruitOfferSource RecruitSource = RecruitOfferSource.RecruitPhase;
    public DominantHand DominantHand = DominantHand.Right;
    public UnitRetrainState RetrainState = new();
    public UnitEconomyFootprint EconomyFootprint = new();
    public List<string> EquippedItemIds = new();
}
