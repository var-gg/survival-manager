using System;
using System.Collections.Generic;

namespace SM.HeadlessPolicies;

/// <summary>플레이어가 이미 소유한 장비와 현재 장착 대상을 담는 prep 전용 공개 관측.</summary>
public sealed class HeadlessOwnedItemObservation
{
    public HeadlessOwnedItemObservation(HeadlessItemMechanicsObservation mechanics, string equippedHeroId)
    {
        Mechanics = mechanics ?? throw new ArgumentNullException(nameof(mechanics));
        EquippedHeroId = equippedHeroId ?? string.Empty;
    }

    public HeadlessItemMechanicsObservation Mechanics { get; }
    public string EquippedHeroId { get; }
}

/// <summary>보유 장비 한 개를 현재 출전 영웅에게 옮기는 무료 prep action.</summary>
public sealed class HeadlessEquipmentAssignment
{
    public HeadlessEquipmentAssignment(string itemInstanceId, string heroId)
    {
        ItemInstanceId = itemInstanceId;
        HeroId = heroId;
    }

    public string ItemInstanceId { get; }
    public string HeroId { get; }
}

/// <summary>elite/boss 직전의 제한된 formation, bench, re-equip 결정을 한 transaction으로 표현한다.</summary>
public sealed class HeadlessPrepDecision
{
    public HeadlessPrepDecision(
        IReadOnlyList<HeadlessPlacement> placements,
        IReadOnlyList<HeadlessEquipmentAssignment> equipmentAssignments,
        string rationale,
        double estimatedValue,
        IReadOnlyList<string> evidenceFactIds = null)
    {
        Placements = placements ?? Array.Empty<HeadlessPlacement>();
        EquipmentAssignments = equipmentAssignments ?? Array.Empty<HeadlessEquipmentAssignment>();
        Rationale = rationale;
        EstimatedValue = estimatedValue;
        EvidenceFactIds = evidenceFactIds ?? Array.Empty<string>();
    }

    public IReadOnlyList<HeadlessPlacement> Placements { get; }
    public IReadOnlyList<HeadlessEquipmentAssignment> EquipmentAssignments { get; }
    public string Rationale { get; }
    public double EstimatedValue { get; }
    public IReadOnlyList<string> EvidenceFactIds { get; }
}
