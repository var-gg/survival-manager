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
    // wave-33-progression: mutable HP record — combat resolution 직후 HeroInstanceRecord에 반영해
    // Town/Atlas/Reward UI가 "이번 전투를 거친 hero의 실제 HP"를 표시할 수 있게 한다.
    // 0이면 "아직 데이터 없음" (fresh hero) → 표시 측에서 fallback ("체력 만전" 같은 표현).
    // 진짜 battle result wire는 별도 sprint — 본 turn은 schema + 표면 wire만.
    public int MaxHp = 0;
    public int CurrentHp = 0;
}
