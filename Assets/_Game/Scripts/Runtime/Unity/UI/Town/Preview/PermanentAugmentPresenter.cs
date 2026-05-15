using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Permanent Augment V1 Presenter — `Profile.UnlockedPermanentAugmentIds` +
/// `PermanentAugmentLoadouts.EquippedAugmentIds` → ViewState (audit §4.1 P0-4 다운스코프).
///
/// 모델: "장착한 영구 augment 1개 + 해금 후보 풀". MaxPermanentAugmentSlots = 1.
/// unlock 상태 read + EquipPermanentAugment edit는 wire 가능. KoLabel/Family/Effect/Flavor는
/// scaffold catalog (Sprint 2에 AugmentDefinition fetch로 교체). posture 매핑 / progress 축 폐기.
///
/// 워크플로우: 사용자가 해금된 augment 선택 + Equip → SessionState.EquipPermanentAugment(augmentId) → BattleTest에 즉시 반영.
/// </summary>
public sealed class PermanentAugmentPresenter : IPermanentAugmentActions
{
    public delegate Texture2D? SpriteLoader(string spriteKey);

    private readonly GameSessionRoot _root;
    private readonly PermanentAugmentView _view;
    private readonly SpriteLoader _augmentSprite;
    private string _selectedAugmentId = string.Empty;

    public PermanentAugmentPresenter(
        GameSessionRoot root,
        PermanentAugmentView view,
        SpriteLoader? augmentSprite = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _augmentSprite = augmentSprite ?? (_ => null);
    }

    public void Initialize()
    {
        _view.Bind(this);
        _view.BindClose(Close);
        Refresh();
    }

    public void Open()
    {
        _view.Open();
        Refresh();
    }

    public void Close()
    {
        _view.Close();
    }

    public void Refresh()
    {
        _view.Render(BuildState());
    }

    void IPermanentAugmentActions.OnAugmentSelected(string augmentId)
    {
        _selectedAugmentId = augmentId;
        Refresh();
    }

    void IPermanentAugmentActions.OnEquipAugment(string augmentId)
    {
        _root.SessionState.EquipPermanentAugment(augmentId);
        Refresh();
    }

    private PermanentAugmentViewState BuildState()
    {
        var session = _root.SessionState;
        var unlockedIds = new HashSet<string>(session.Profile.UnlockedPermanentAugmentIds, StringComparer.Ordinal);

        // 단일 슬롯 — active blueprint의 EquippedAugmentIds 첫 번째.
        var equippedId = session.Profile.PermanentAugmentLoadouts
            .FirstOrDefault(record => string.Equals(record.BlueprintId, session.Profile.ActiveBlueprintId, StringComparison.Ordinal))
            ?.EquippedAugmentIds.FirstOrDefault() ?? string.Empty;

        // selected 기본값 = 현재 equipped, 없으면 catalog 첫 번째.
        if (string.IsNullOrEmpty(_selectedAugmentId))
        {
            _selectedAugmentId = !string.IsNullOrEmpty(equippedId)
                ? equippedId
                : PermanentCatalog[0].AugmentId;
        }

        var cells = PermanentCatalog
            .Select(p => new PermanentAugmentCellViewState(
                AugmentId: p.AugmentId,
                KoLabel: p.KoLabel,
                FamilyBucket: p.FamilyBucket,
                Unlocked: unlockedIds.Contains(p.AugmentId),
                IsEquipped: string.Equals(p.AugmentId, equippedId, StringComparison.Ordinal),
                IsSelected: string.Equals(p.AugmentId, _selectedAugmentId, StringComparison.Ordinal),
                IconSprite: _augmentSprite(p.Motif)))
            .ToList();

        var selectedSpec = PermanentCatalog.FirstOrDefault(p => p.AugmentId == _selectedAugmentId)
            ?? PermanentCatalog[0];
        var detail = new PermanentAugmentDetailViewState(
            SelectedAugmentId: selectedSpec.AugmentId,
            KoLabel: selectedSpec.KoLabel,
            EnLabel: selectedSpec.EnLabel,
            FamilyBucket: selectedSpec.FamilyBucket,
            SignatureEffect: selectedSpec.SignatureEffect,  // TODO Sprint 2: AugmentDefinition Effects/Modifiers fetch
            FlavorText: selectedSpec.Flavor,
            IsUnlocked: unlockedIds.Contains(selectedSpec.AugmentId),
            IsEquipped: string.Equals(selectedSpec.AugmentId, equippedId, StringComparison.Ordinal),
            IconSprite: _augmentSprite(selectedSpec.Motif),
            MetaRows: new[]
            {
                new PermanentAugmentMetaRowViewState("FAMILY", selectedSpec.FamilyBucket),
                new PermanentAugmentMetaRowViewState("강화 효과", selectedSpec.SignatureEffect),
            });

        return new PermanentAugmentViewState(cells, detail);
    }

    private sealed record PermanentCatalogEntry(
        string AugmentId, string Motif, string KoLabel, string EnLabel,
        string FamilyBucket, string SignatureEffect, string Flavor);

    /// <summary>
    /// pindoc V1 wiki-combat-augment-v1: permanent augment 4개. P0-4 다운스코프 — posture 매핑은
    /// 구조 축에서 폐기 (Flavor 산문에만 암시). SignatureEffect는 scaffold placeholder — 실 값은
    /// AugmentDefinition.Effects/Modifiers/DescriptionKey에서 read (Sprint 2).
    /// </summary>
    private static readonly PermanentCatalogEntry[] PermanentCatalog =
    {
        new("augment_perm_citadel_doctrine",    "shield", "성채 신조",      "Citadel Doctrine",    "TacticalRewrite", "전열 유지 +30%, 후열 추격 -20%",          "한 발도 물러서지 않는다. 전열이 무너지면 그 뒤의 모든 것이 함께 무너진다."),
        new("augment_perm_guardian_detail",     "wing",   "수호 분대",      "Guardian Detail",     "HeroRewrite",     "캐리 보호 반경 +25%, 후열 피해 흡수 -15%", "칼끝은 앞에서 빛나지만, 그 칼을 끝까지 쥐게 하는 손은 늘 뒤에 있다."),
        new("augment_perm_breakthrough_orders", "blade",  "돌파 명령",      "Breakthrough Orders", "ScalingEngine",   "약측 적 처리 시 phys_power +5% 누적",      "약한 고리를 찾아 끊어라. 전선은 언제나 가장 얇은 곳에서 먼저 찢어진다."),
        new("augment_perm_night_hunt_mandate",  "moon",   "야간 추적 위임", "Night Hunt Mandate",  "SynergyPact",     "후열 처치 시 squad 전체 lifesteal +0.03",  "어둠을 틈타 지휘선을 노린다. 머리를 치면 몸은 스스로 멈춘다."),
    };

    public static IReadOnlyList<(string AugmentId, string Motif, string KoLabel, string EnLabel, string FamilyBucket, string SignatureEffect, string Flavor)> Catalog
        => PermanentCatalog
            .Select(p => (p.AugmentId, p.Motif, p.KoLabel, p.EnLabel, p.FamilyBucket, p.SignatureEffect, p.Flavor))
            .ToList();
}
