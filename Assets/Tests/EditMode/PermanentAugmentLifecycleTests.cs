using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class PermanentAugmentLifecycleTests
{
    // FakeCombatContentLookup은 Resources.LoadAll을 호출하지 않으므로
    // GUI 모드에서도 에디터 freeze 없이 빠르게 실행된다.
    private static readonly FakeCombatContentLookup SharedLookup = new();

    [Test]
    public void EquipPermanentAugment_UnlocksAndEquips()
    {
        var session = CreateBoundSession();

        var result = session.EquipPermanentAugment("perm_aug_001");

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(session.Profile.UnlockedPermanentAugmentIds, Does.Contain("perm_aug_001"));

        var loadout = session.Profile.PermanentAugmentLoadouts
            .FirstOrDefault(r => r.BlueprintId == session.Profile.ActiveBlueprintId);
        Assert.That(loadout, Is.Not.Null, "PermanentAugmentLoadoutRecord가 생성되어야 함");
        Assert.That(loadout!.EquippedAugmentIds, Does.Contain("perm_aug_001"));
    }

    [Test]
    public void EquipPermanentAugment_DuplicateEquip_Fails()
    {
        var session = CreateBoundSession();
        session.EquipPermanentAugment("perm_aug_001");

        var result = session.EquipPermanentAugment("perm_aug_001");

        Assert.That(result.IsSuccess, Is.False, "이미 장착된 영구 증강 재장착 불가");
    }

    [Test]
    public void EquipPermanentAugment_MaxSlotsCap_Fails()
    {
        var session = CreateBoundSession();
        var maxSlots = MetaBalanceDefaults.MaxPermanentAugmentSlots;

        // Fill all slots
        for (int i = 0; i < maxSlots; i++)
        {
            var fillResult = session.EquipPermanentAugment($"perm_aug_{i:D3}");
            Assert.That(fillResult.IsSuccess, Is.True, fillResult.Error);
        }

        // Try one more
        var result = session.EquipPermanentAugment("perm_aug_overflow");

        Assert.That(result.IsSuccess, Is.False, $"최대 {maxSlots}개 초과 장착 불가");
    }

    [Test]
    public void UnequipPermanentAugment_RemovesFromLoadout()
    {
        var session = CreateBoundSession();
        session.EquipPermanentAugment("perm_aug_001");
        session.EquipPermanentAugment("perm_aug_002");

        var result = session.UnequipPermanentAugment("perm_aug_001");

        Assert.That(result.IsSuccess, Is.True, result.Error);

        var loadout = session.Profile.PermanentAugmentLoadouts
            .First(r => r.BlueprintId == session.Profile.ActiveBlueprintId);
        Assert.That(loadout.EquippedAugmentIds, Does.Not.Contain("perm_aug_001"));
        Assert.That(loadout.EquippedAugmentIds, Does.Contain("perm_aug_002"));
    }

    [Test]
    public void UnequipPermanentAugment_NotEquipped_Fails()
    {
        var session = CreateBoundSession();
        session.EquipPermanentAugment("perm_aug_001");

        var result = session.UnequipPermanentAugment("perm_aug_not_equipped");

        Assert.That(result.IsSuccess, Is.False, "장착되지 않은 증강 해제 불가");
    }

    [Test]
    public void EquipPermanentAugment_EmptyId_Fails()
    {
        var session = CreateBoundSession();

        var result = session.EquipPermanentAugment("");

        Assert.That(result.IsSuccess, Is.False, "빈 ID로 장착 불가");
    }

    [Test]
    public void EquipAndUnequip_Roundtrip_LeavesCleanState()
    {
        var session = CreateBoundSession();
        session.EquipPermanentAugment("perm_aug_001");
        session.UnequipPermanentAugment("perm_aug_001");

        var loadout = session.Profile.PermanentAugmentLoadouts
            .First(r => r.BlueprintId == session.Profile.ActiveBlueprintId);
        Assert.That(loadout.EquippedAugmentIds, Is.Empty, "장착 → 해제 후 빈 상태여야 함");

        // Can re-equip after unequip
        var result = session.EquipPermanentAugment("perm_aug_001");
        Assert.That(result.IsSuccess, Is.True, "해제 후 재장착 가능");
    }

    [Test]
    public void UnequipPermanentAugment_NoLoadoutRecord_Fails()
    {
        var session = CreateBoundSession();
        // No augment equipped yet → no loadout record exists

        var result = session.UnequipPermanentAugment("perm_aug_001");

        Assert.That(result.IsSuccess, Is.False, "로드아웃 레코드가 없으면 해제 불가");
    }

    // ── helpers ──

    private static GameSessionState CreateBoundSession()
    {
        var session = new GameSessionState(SharedLookup);
        session.BindProfile(new SaveProfile());
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }
}
