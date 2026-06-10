using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Unity;
using UnityEngine;
using UnityEngine.Animations;

namespace SM.Tests.EditMode;

/// <summary>
/// 활/지팡이 손 거치 전환(BattleP09WeaponStanceRig) 계약: P09 rig의 등 홀스터 거치를
/// 전투용 손 거치로 옮긴다. 제약에 손 소스가 있으면 가중치 플립(팩 authored 자세 우선),
/// 없으면 제약 비활성 + 손 타깃 reparent. 근접 유닛은 건드리지 않는다.
/// </summary>
[Category("BatchOnly")]
public sealed class BattleP09WeaponStanceRigTests
{
    [Test]
    public void ApplyRangedHandStance_FlipsConstraintWeights_WhenHandSourceExists()
    {
        var root = new GameObject("ModelRoot");
        try
        {
            var hand = CreateChild(root.transform, "Weapon_Target_Hand_L");
            var back = CreateChild(root.transform, "Bow_Target_Back");
            var bow = CreateWeapon(root.transform, "Bow_001");
            var constraint = bow.gameObject.AddComponent<ParentConstraint>();
            constraint.AddSource(new ConstraintSource { sourceTransform = hand, weight = 0f });
            constraint.AddSource(new ConstraintSource { sourceTransform = back, weight = 1f });
            constraint.constraintActive = false;

            BattleP09WeaponStanceRig.ApplyRangedHandStance(root.transform, CreateUnit(classId: "ranger"));

            Assert.That(constraint.GetSource(0).weight, Is.EqualTo(1f), "hand source takes full weight");
            Assert.That(constraint.GetSource(1).weight, Is.EqualTo(0f), "back source is released");
            Assert.That(constraint.constraintActive, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ApplyRangedHandStance_FlipsGroupConstraint_AndLeavesChildMeshUnderGroup()
    {
        // 실제 P09 rig 모양: "Weapon/Bow" 그룹이 ParentConstraint(소스=[손 w=0, 등 w=1])를 들고
        // 메시 자식("Bow_001")은 그룹 아래에서 외형 프리셋으로 켜진다. 그룹만 플립되고
        // 자식 메시는 그룹 아래 그대로 남아야 한다(이중 reparent 금지).
        var root = new GameObject("ModelRoot");
        try
        {
            var hand = CreateChild(root.transform, "Weapon_Target_Hand_L");
            var back = CreateChild(root.transform, "Bow_Target_Back");
            var weaponGroup = CreateChild(root.transform, "Weapon");
            var bowGroup = CreateChild(weaponGroup, "Bow");
            var bowMesh = CreateWeapon(bowGroup, "Bow_001");
            var constraint = bowGroup.gameObject.AddComponent<ParentConstraint>();
            constraint.AddSource(new ConstraintSource { sourceTransform = hand, weight = 0f });
            constraint.AddSource(new ConstraintSource { sourceTransform = back, weight = 1f });
            constraint.constraintActive = true;

            BattleP09WeaponStanceRig.ApplyRangedHandStance(root.transform, CreateUnit(classId: "ranger"));

            Assert.That(constraint.GetSource(0).weight, Is.EqualTo(1f), "pack-authored hand source takes over");
            Assert.That(constraint.GetSource(1).weight, Is.EqualTo(0f), "back holster source is released");
            Assert.That(constraint.constraintActive, Is.True);
            Assert.That(bowMesh.parent, Is.SameAs(bowGroup), "child mesh stays under the constrained group");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ApplyRangedHandStance_ReparentsToHandTarget_WhenConstraintHasNoHandSource()
    {
        var root = new GameObject("ModelRoot");
        try
        {
            var hand = CreateChild(root.transform, "Weapon_Target_Hand_L");
            var back = CreateChild(root.transform, "Bow_Target_Back");
            var bow = CreateWeapon(back, "Bow_001");
            var constraint = bow.gameObject.AddComponent<ParentConstraint>();
            constraint.AddSource(new ConstraintSource { sourceTransform = back, weight = 1f });
            constraint.constraintActive = true;

            BattleP09WeaponStanceRig.ApplyRangedHandStance(root.transform, CreateUnit(classId: "ranger"));

            Assert.That(bow.parent, Is.SameAs(hand), "bow moves under the hand target");
            Assert.That(bow.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(constraint.constraintActive, Is.False, "back-tracking constraint is released");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ApplyRangedHandStance_MovesStaffToRightHand_ForProjectileCasters()
    {
        var root = new GameObject("ModelRoot");
        try
        {
            var hand = CreateChild(root.transform, "Weapon_Target_Hand_R");
            var back = CreateChild(root.transform, "Staff_Target_Back");
            var staff = CreateWeapon(back, "Staff_002");

            BattleP09WeaponStanceRig.ApplyRangedHandStance(
                root.transform,
                CreateUnit(classId: "mystic", preferredRangeMin: 2.35f, preferredRangeMax: 3.15f));

            Assert.That(staff.parent, Is.SameAs(hand), "staff moves under the right-hand target");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ApplyRangedHandStance_LeavesMeleeUnitsUntouched()
    {
        var root = new GameObject("ModelRoot");
        try
        {
            CreateChild(root.transform, "Weapon_Target_Hand_L");
            var back = CreateChild(root.transform, "Bow_Target_Back");
            var bow = CreateWeapon(back, "Bow_001");

            BattleP09WeaponStanceRig.ApplyRangedHandStance(root.transform, CreateUnit(classId: "vanguard"));

            Assert.That(bow.parent, Is.SameAs(back), "melee classification leaves the rig as authored");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static Transform CreateWeapon(Transform parent, string name)
    {
        var weapon = CreateChild(parent, name);
        weapon.gameObject.AddComponent<MeshRenderer>();
        return weapon;
    }

    private static BattleUnitReadModel CreateUnit(
        string classId,
        float preferredRangeMin = 0f,
        float preferredRangeMax = 0f)
    {
        return new BattleUnitReadModel(
            "ally",
            "Ally",
            TeamSide.Ally,
            DeploymentAnchorId.FrontCenter,
            "human",
            classId,
            new CombatVector2(0f, 0f),
            10f,
            10f,
            true,
            CombatActionState.AcquireTarget,
            null,
            "enemy",
            "Enemy",
            0f,
            0f,
            0f,
            100f,
            false,
            PreferredRangeMin: preferredRangeMin,
            PreferredRangeMax: preferredRangeMax);
    }
}
