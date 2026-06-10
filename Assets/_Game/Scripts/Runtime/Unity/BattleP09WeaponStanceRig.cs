using System.Collections.Generic;
using SM.Combat.Model;
using UnityEngine;
using UnityEngine.Animations;

namespace SM.Unity;

/// <summary>
/// P09 모듈러 캐릭터의 원거리 무기(활/지팡이)를 전투 중 손에 들도록 거치를 바꾸는 presentation 헬퍼.
/// P09 팩은 활을 <c>Bow_Target_Back</c>, 지팡이를 <c>Staff_Target_Back</c>(등 홀스터)에 거치하므로
/// 전투 모션이 빈손으로 재생돼 "활을 등에 멘 채 공격"으로 보이던 문제의 수술 지점이다.
/// 무기 오브젝트가 ParentConstraint로 거치된 경우 손 타깃 소스로 가중치를 옮기고(팩 authored 손 자세 우선),
/// 손 소스가 없으면 제약을 끄고 손 타깃 아래로 reparent한다. 시각 전용 — sim truth와 무관.
/// </summary>
public static class BattleP09WeaponStanceRig
{
    private const string BowHandTargetName = "Weapon_Target_Hand_L";
    private const string StaffHandTargetName = "Weapon_Target_Hand_R";

    /// <summary>원거리 분류 actor의 활/지팡이를 손 거치로 전환한다. 근접 분류면 아무것도 하지 않는다.</summary>
    public static void ApplyRangedHandStance(Transform modelRoot, BattleUnitReadModel actor)
    {
        if (modelRoot == null || actor == null)
        {
            return;
        }

        var kind = BattleBasicAttackPresentationClassifier.Resolve(actor);
        if (kind == BattleBasicAttackPresentationKind.Melee)
        {
            return;
        }

        var weaponToken = kind == BattleBasicAttackPresentationKind.Bow ? "Bow" : "Staff";
        var handTargetName = kind == BattleBasicAttackPresentationKind.Bow ? BowHandTargetName : StaffHandTargetName;
        var handTarget = FindByExactName(modelRoot, handTargetName);
        var weapons = CollectWeaponTransforms(modelRoot, weaponToken);
        foreach (var weapon in weapons)
        {
            MoveToHand(weapon, handTarget);
        }
    }

    // 무기 오브젝트를 찾는다. P09 실제 rig는 "Weapon/Bow"(ParentConstraint 보유 그룹) 아래에
    // "Bow_001" 같은 메시 자식들이 있고 외형 프리셋이 weaponId로 하나만 켠다. 거치 타깃
    // ("Bow_Target_Back")은 제외, 렌더러가 보이는(=현재 무기가 켜진) 가지만 수집하고,
    // 조상이 이미 수집된 자식은 버린다 — 그룹 제약을 플립한 뒤 자식 메시를 또 옮기면 안 된다.
    private static List<Transform> CollectWeaponTransforms(Transform modelRoot, string weaponToken)
    {
        var candidates = new List<Transform>();
        foreach (var transform in modelRoot.GetComponentsInChildren<Transform>(false))
        {
            var name = transform.name;
            if (!name.Contains(weaponToken) || name.Contains("Target"))
            {
                continue;
            }

            if (transform.GetComponentInChildren<Renderer>(false) == null)
            {
                continue;
            }

            candidates.Add(transform);
        }

        var topmost = new List<Transform>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (!HasAncestorIn(candidate, candidates, modelRoot))
            {
                topmost.Add(candidate);
            }
        }

        return topmost;
    }

    private static bool HasAncestorIn(Transform candidate, List<Transform> pool, Transform stopAt)
    {
        for (var parent = candidate.parent; parent != null && parent != stopAt; parent = parent.parent)
        {
            if (pool.Contains(parent))
            {
                return true;
            }
        }

        return false;
    }

    private static void MoveToHand(Transform weapon, Transform? handTarget)
    {
        var constraint = weapon.GetComponent<ParentConstraint>();
        if (constraint != null && TryFlipConstraintToHandSource(constraint))
        {
            return; // 팩이 authored한 손 소스/오프셋을 그대로 사용 — 가장 정확한 손 자세.
        }

        if (handTarget == null)
        {
            return; // 손 타깃이 없는 rig — 건드리지 않는 게 안전.
        }

        if (constraint != null)
        {
            constraint.constraintActive = false; // 등 타깃 추적을 끊어야 reparent가 보인다.
        }

        // 거치 타깃 transform이 팩 authored 그립 자세(위치+회전)를 들고 있으므로 identity local로 앉힌다.
        weapon.SetParent(handTarget, worldPositionStays: false);
        weapon.localPosition = Vector3.zero;
        weapon.localRotation = Quaternion.identity;
    }

    // ParentConstraint 소스 중 손 타깃("...Hand...")이 있으면 그 소스만 1, 나머지 0으로 전환.
    private static bool TryFlipConstraintToHandSource(ParentConstraint constraint)
    {
        var handIndex = -1;
        for (var i = 0; i < constraint.sourceCount; i++)
        {
            var source = constraint.GetSource(i);
            if (source.sourceTransform != null && source.sourceTransform.name.Contains("Hand"))
            {
                handIndex = i;
                break;
            }
        }

        if (handIndex < 0)
        {
            return false;
        }

        for (var i = 0; i < constraint.sourceCount; i++)
        {
            var source = constraint.GetSource(i);
            source.weight = i == handIndex ? 1f : 0f;
            constraint.SetSource(i, source);
        }

        constraint.constraintActive = true;
        return true;
    }

    private static Transform? FindByExactName(Transform root, string name)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(transform.name, name, System.StringComparison.Ordinal))
            {
                return transform;
            }
        }

        return null;
    }
}
