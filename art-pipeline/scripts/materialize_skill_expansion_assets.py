#!/usr/bin/env python3
"""Materialize expansion skill catalog rows into Unity authored assets."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Any

import yaml


REPO_ROOT = Path(__file__).resolve().parents[2]
CATALOG_PATH = REPO_ROOT / "art-pipeline" / "config" / "skill_expansion_design_catalog.yaml"
CONTENT_ROOT = REPO_ROOT / "Assets" / "Resources" / "_Game" / "Content" / "Definitions"
SKILL_DIR = CONTENT_ROOT / "Skills"
TAG_DIR = CONTENT_ROOT / "StableTags"
LOCALIZATION_DIR = REPO_ROOT / "Assets" / "Localization" / "StringTables"
SKILL_SHARED_TABLE = LOCALIZATION_DIR / "Content_Skills Shared Data.asset"
SKILL_EN_TABLE = LOCALIZATION_DIR / "Content_Skills_en.asset"
SKILL_KO_TABLE = LOCALIZATION_DIR / "Content_Skills_ko.asset"

SKILL_SCRIPT_GUID = "317f0135c04991a4e91ddd8d0479dbe4"

KIND = {"Strike": 0, "Heal": 1, "Shield": 2, "Buff": 3, "Debuff": 4, "Utility": 5}
DAMAGE = {"Physical": 0, "Magical": 1, "Healing": 2}
DELIVERY = {"Melee": 0, "Ranged": 1, "Projectile": 2, "Nova": 3, "Aura": 4, "Trap": 5, "Zone": 6}
TARGET_RULE = {
    "NearestEnemy": 0,
    "LowestHpEnemy": 1,
    "MostExposedEnemy": 2,
    "LowestHpAlly": 3,
    "ProtectedAlly": 4,
    "Self": 5,
    "MarkedTarget": 6,
}
SLOT = {"CoreActive": 0, "UtilityActive": 1, "Passive": 2, "Support": 3}

TEMPLATE_TYPE = {
    "Melee": 1,
    "Ranged": 4,
    "Projectile": 4,
    "Nova": 9,
    "Aura": 11,
    "Trap": 8,
    "Zone": 8,
}
ROLE_PROFILE = {"vanguard": 0, "duelist": 2, "ranger": 3, "mystic": 4}


def load_yaml(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected YAML mapping")
    return data


def deterministic_guid(path: Path) -> str:
    rel = path.relative_to(REPO_ROOT).as_posix()
    return hashlib.md5(f"survival-manager:{rel}".encode("utf-8")).hexdigest()


def format_number(value: Any) -> str:
    number = float(value)
    if number.is_integer():
        return str(int(number))
    return f"{number:.3f}".rstrip("0").rstrip(".")


def load_tag_guids() -> dict[str, str]:
    result: dict[str, str] = {}
    for path in TAG_DIR.glob("*.asset"):
        text = path.read_text(encoding="utf-8")
        id_match = re.search(r"^  Id:\s*(\S+)\s*$", text, re.MULTILINE)
        if id_match is None:
            continue
        meta = path.with_name(f"{path.name}.meta")
        meta_text = meta.read_text(encoding="utf-8")
        guid_match = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", meta_text, re.MULTILINE)
        if guid_match is None:
            raise ValueError(f"{meta}: missing guid")
        result[id_match.group(1)] = guid_match.group(1)
    return result


def tag_ref(tag_id: str, tags: dict[str, str]) -> str:
    if tag_id not in tags:
        raise ValueError(f"missing StableTagDefinition for '{tag_id}'")
    return f"  - {{fileID: 11400000, guid: {tags[tag_id]}, type: 2}}"


def tag_list(tag_ids: list[str], tags: dict[str, str]) -> str:
    if not tag_ids:
        return "[]"
    return "\n".join(tag_ref(tag_id, tags) for tag_id in tag_ids)


def range_for(skill: dict[str, Any]) -> tuple[float, float]:
    delivery = str(skill["delivery"])
    target_rule = str(skill["target_rule"])
    if target_rule == "Self":
        base = 0.0
    elif delivery == "Melee":
        base = 1.5
    elif delivery in {"Ranged", "Projectile"}:
        base = 7.0
    elif delivery in {"Zone", "Trap"}:
        base = 5.0
    elif delivery == "Nova":
        base = 3.0
    else:
        base = 4.0

    radius = 0.0
    if delivery == "Nova":
        radius = 3.0
    elif delivery == "Zone":
        radius = 2.75
    elif delivery == "Trap":
        radius = 2.25
    elif delivery == "Aura":
        radius = 2.5
    return base, radius


def target_rule_data(target_rule: str) -> dict[str, Any]:
    table = {
        "Self": (1, 9, 4, 0),
        "LowestHpAlly": (3, 10, 4, 21),
        "ProtectedAlly": (3, 12, 4, 21),
        "MarkedTarget": (2, 6, 2, 37),
        "MostExposedEnemy": (2, 8, 2, 69),
        "LowestHpEnemy": (2, 3, 2, 5),
        "NearestEnemy": (2, 1, 2, 5),
    }
    domain, selector, fallback, filters = table[target_rule]
    return {
        "Domain": domain,
        "PrimarySelector": selector,
        "FallbackPolicy": fallback,
        "Filters": filters,
    }


def power_band(skill: dict[str, Any]) -> tuple[int, int]:
    slot = str(skill["slot"])
    if slot in {"Passive", "Support"}:
        return 1, 8
    return 2, 12


def budget_vector(skill: dict[str, Any], target: int) -> dict[str, int]:
    vector = {
        "SustainedDamage": 0,
        "BurstDamage": 0,
        "Durability": 0,
        "Control": 0,
        "Mobility": 0,
        "Support": 0,
        "CounterCoverage": 0,
        "Reliability": 0,
        "Economy": 0,
        "DrawbackCredit": 0,
    }
    kind = str(skill["kind"])
    damage = str(skill["damage"])
    status_count = len(skill.get("statuses") or [])
    if kind == "Shield":
        vector["Durability"] = min(5, target)
        vector["Support"] = min(3, max(0, target - vector["Durability"]))
    elif kind == "Heal" or damage == "Healing":
        vector["Support"] = min(5, target)
        vector["Durability"] = min(2, max(0, target - vector["Support"]))
    elif kind == "Debuff":
        vector["Control"] = min(4, target)
        vector["Reliability"] = min(2, max(0, target - vector["Control"]))
        if float(skill.get("power") or 0) > 0:
            vector["BurstDamage"] = min(3, max(0, target - vector["Control"] - vector["Reliability"]))
    elif kind in {"Buff", "Utility"}:
        vector["Support"] = min(4, target)
        vector["Mobility"] = 1 if str(skill["delivery"]) in {"Melee", "Ranged", "Projectile"} else 0
        vector["Durability"] = min(2, max(0, target - vector["Support"] - vector["Mobility"]))
    else:
        vector["SustainedDamage"] = 3 if target >= 12 else 2
        vector["BurstDamage"] = 4 if target >= 12 else 2
        vector["Mobility"] = 1 if str(skill["delivery"]) in {"Melee", "Ranged", "Projectile"} else 0
        vector["Control"] = min(2, status_count)

    spent = sum(value for key, value in vector.items() if key != "DrawbackCredit")
    vector["Reliability"] += max(0, target - spent)
    return vector


def coeffs(skill: dict[str, Any]) -> tuple[float, float, float]:
    power = float(skill.get("power") or 0)
    if power <= 0:
        return 0.0, 0.0, 0.0
    damage = str(skill["damage"])
    if damage == "Physical":
        return 1.0, 0.0, 0.0
    if damage == "Magical":
        return 0.0, 1.0, 0.0
    if damage == "Healing":
        return 0.0, 0.0, 1.0
    raise ValueError(f"{skill['id']}: unsupported damage '{damage}'")


def yaml_scalar(value: str) -> str:
    if value == "":
        return ""
    if re.fullmatch(r"[A-Za-z0-9_.:-]+", value):
        return value
    return json.dumps(value, ensure_ascii=False)


def status_block(skill: dict[str, Any]) -> str:
    statuses = skill.get("statuses") or []
    if not statuses:
        return "[]"
    lines: list[str] = []
    for status in statuses:
        status_id = str(status["id"])
        lines.extend(
            [
                f"  - Id: status_{status_id}",
                f"    StatusId: {status_id}",
                f"    DurationSeconds: {format_number(status.get('duration', 0))}",
                f"    Magnitude: {format_number(status.get('magnitude', 0))}",
                "    MaxStacks: 1",
                "    RefreshDurationOnReapply: 1",
                "    StackCap: 0",
                "    StackPolicy: 0",
                "    RefreshPolicy: 0",
                "    ProcAttributionPolicy: 0",
                "    OwnershipPolicy: 0",
                "    Effects: []",
            ]
        )
    return "\n".join(lines)


def render_skill_asset(skill: dict[str, Any], tags: dict[str, str]) -> str:
    skill_id = str(skill["id"])
    slot = str(skill["slot"])
    kind = str(skill["kind"])
    damage = str(skill["damage"])
    delivery = str(skill["delivery"])
    target = str(skill["target_rule"])
    class_id = str(skill["target_class"])
    if damage not in DAMAGE:
        raise ValueError(f"{skill_id}: unsupported damage '{damage}'")
    if class_id not in ROLE_PROFILE:
        raise ValueError(f"{skill_id}: unsupported target_class '{class_id}'")

    range_value, radius = range_for(skill)
    band, target_score = power_band(skill)
    vector = budget_vector(skill, target_score)
    phys, mag, heal = coeffs(skill)
    template = 15 if slot in {"Passive", "Support"} else TEMPLATE_TYPE[delivery]
    if kind in {"Shield", "Heal"}:
        template = 12
    rule = target_rule_data(target)
    required_class_tags = [class_id]
    support_allowed = [class_id] if skill_id.startswith("support_") else []
    compile_tags = [class_id]
    support_allowed_block = (
        "SupportAllowedTags:\n"
        f"{tag_list(support_allowed, tags)}"
        if support_allowed
        else "SupportAllowedTags: []"
    )
    applied_status_block = (
        "AppliedStatuses:\n"
        f"{status_block(skill)}"
        if skill.get("statuses")
        else "AppliedStatuses: []"
    )

    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SKILL_SCRIPT_GUID}, type: 3}}
  m_Name: {skill_id}
  m_EditorClassIdentifier: SM.Content:SM.Content.Definitions:SkillDefinitionAsset
  Id: {skill_id}
  NameKey: content.skill.{skill_id}.name
  DescriptionKey: content.skill.{skill_id}.desc
  TemplateType: {template}
  Kind: {KIND[kind]}
  SlotKind: {SLOT[slot]}
  DamageType: {DAMAGE[damage]}
  Delivery: {DELIVERY[delivery]}
  TargetRule: {TARGET_RULE[target]}
  Power: {format_number(skill.get("power", 0))}
  Range: {format_number(range_value)}
  RangeMin: 0
  RangeMax: -1
  Radius: {format_number(radius)}
  Width: 0
  ArcDegrees: 0
  PowerFlat: {format_number(skill.get("power", 0))}
  PhysCoeff: {format_number(phys)}
  MagCoeff: {format_number(mag)}
  HealCoeff: {format_number(heal)}
  HealthCoeff: 0
  CanCrit: {1 if float(skill.get("power") or 0) > 0 and damage in {"Physical", "Magical"} and kind == "Strike" else 0}
  ActivationModel: 2
  Lane: 0
  LockRule: 2
  AuthorityLayer: 1
  BudgetCard:
    Domain: 1
    Rarity: 0
    PowerBand: {band}
    RoleProfile: {ROLE_PROFILE[class_id]}
    Vector:
      SustainedDamage: {vector["SustainedDamage"]}
      BurstDamage: {vector["BurstDamage"]}
      Durability: {vector["Durability"]}
      Control: {vector["Control"]}
      Mobility: {vector["Mobility"]}
      Support: {vector["Support"]}
      CounterCoverage: 0
      Reliability: {vector["Reliability"]}
      Economy: 0
      DrawbackCredit: 0
    KeywordCount: {min(2, max(1, len(skill.get("statuses") or []) + (1 if kind in {"Shield", "Heal", "Buff", "Debuff"} else 0)))}
    ConditionClauseCount: {1 if skill.get("statuses") else 0}
    RuleExceptionCount: 0
    DeclaredThreatPatterns:
    DeclaredCounterTools: []
    DeclaredFeatureFlags: 0
  ManaCost: 0
  ResourceCost: -1
  BaseCooldownSeconds: {format_number(skill.get("cooldown", 0))}
  CooldownSeconds: -1
  CastWindupSeconds: 0
  RecoverySeconds: -1
  PowerBudget: 0
  InterruptRefundScalar: 0.5
  AiIntents:
  AiScoreHints:
    BurstBias: {1 if kind == "Strike" else 0}
    ProtectBias: {1 if kind in {"Shield", "Heal", "Buff"} else 0}
    MaintainRangeBias: {1 if delivery in {"Ranged", "Projectile"} else 0}
    ExecuteBias: {1 if target in {"LowestHpEnemy", "MostExposedEnemy"} else 0}
    ControlBias: {1 if kind == "Debuff" or skill.get("statuses") else 0}
    MinimumTargetHealthRatio: 0
    MaximumTargetHealthRatio: 1
    MinimumDistance: 0
    MaximumDistance: {format_number(range_value)}
  AnimationHookId:
  IconId: {skill["icon_id"]}
  VfxHookId: vfx.{skill_id}
  SfxHookId:
  LearnSource: 2
  EffectFamilyId: expansion_v1_{class_id}
  MutuallyExclusiveGroupId:
  RecruitNativeTags: []
  RecruitPlanTags: []
  RecruitScoutTags: []
  TargetRuleData:
    Domain: {rule["Domain"]}
    PrimarySelector: {rule["PrimarySelector"]}
    FallbackPolicy: {rule["FallbackPolicy"]}
    Filters: {rule["Filters"]}
    ReevaluateIntervalSeconds: 0.25
    MinimumCommitSeconds: 0.75
    MaxAcquireRange: 0
    PreferredMinTargets: 1
    ClusterRadius: 2.5
    LockTargetAtCastStart: 1
    RetargetLockMode: 3
  SummonProfile:
    EntityKind: 1
    BehaviorKind: 1
    Eligibility: 24
    CreditPolicy: 15
    MaxConcurrentPerSource: 2
    MaxConcurrentPerOwner: 4
    DespawnOnOwnerDeath: 1
    OwnerDeathDespawnDelaySeconds: 1
    InheritOwnerTarget: 1
    IsPersistent: 1
    Inheritance:
      OffenseBonusScalar: 0.5
      DefenseBonusScalar: 0.35
      UtilityBonusScalar: 0.25
      InheritCritChance: 0
      InheritDodge: 0
      InheritBlock: 0
  Effects: []
  CompileTags:
{tag_list(compile_tags, tags)}
  RuleModifierTags: []
  {support_allowed_block}
  SupportBlockedTags: []
  RequiredWeaponTags: []
  RequiredClassTags:
{tag_list(required_class_tags, tags)}
  {applied_status_block}
  CleanseProfileId:
  legacyDisplayName:
"""


def write_skill_assets(catalog: dict[str, Any], dry_run: bool) -> int:
    tags = load_tag_guids()
    skills = catalog.get("skills")
    if not isinstance(skills, list):
        raise ValueError("catalog.skills must be a list")
    written = 0
    for skill in skills:
        path = SKILL_DIR / f"{skill['id']}.asset"
        meta = path.with_name(f"{path.name}.meta")
        text = render_skill_asset(skill, tags)
        meta_text = (
            "fileFormatVersion: 2\n"
            f"guid: {deterministic_guid(path)}\n"
            "NativeFormatImporter:\n"
            "  externalObjects: {}\n"
            "  mainObjectFileID: 11400000\n"
            "  userData:\n"
            "  assetBundleName:\n"
            "  assetBundleVariant:\n"
        )
        if dry_run:
            print(f"[materialize] {path.relative_to(REPO_ROOT)}")
            written += 1
            continue
        path.write_text(text, encoding="utf-8", newline="\n")
        meta.write_text(meta_text, encoding="utf-8", newline="\n")
        written += 1
    return written


def parse_shared_entries(text: str) -> dict[str, int]:
    result: dict[str, int] = {}
    current_id: int | None = None
    for line in text.splitlines():
        id_match = re.match(r"\s*-\s*m_Id:\s*(\d+)\s*$", line)
        if id_match:
            current_id = int(id_match.group(1))
            continue
        key_match = re.match(r"\s*m_Key:\s*(\S+)\s*$", line)
        if key_match and current_id is not None:
            result[key_match.group(1)] = current_id
            current_id = None
    return result


def append_before_table_end(path: Path, entries: list[str]) -> None:
    text = path.read_text(encoding="utf-8")
    if not text.endswith("\n"):
        text += "\n"
    text += "".join(entries)
    path.write_text(text, encoding="utf-8", newline="\n")


def update_localization(catalog: dict[str, Any], dry_run: bool) -> int:
    shared_text = SKILL_SHARED_TABLE.read_text(encoding="utf-8")
    shared = parse_shared_entries(shared_text)
    next_id = max(shared.values(), default=290000000000000) + 1
    shared_entries: list[str] = []
    en_entries: list[str] = []
    ko_entries: list[str] = []
    added = 0

    for skill in catalog.get("skills", []):
        skill_id = str(skill["id"])
        rows = [
            (f"content.skill.{skill_id}.name", str(skill["name_en"]), str(skill["name_ko"])),
            (f"content.skill.{skill_id}.desc", str(skill["effect_en"]), str(skill["effect_ko"])),
        ]
        for key, en, ko in rows:
            if key in shared:
                continue
            entry_id = next_id
            next_id += 1
            shared_entries.append(
                f"  - m_Id: {entry_id}\n"
                f"    m_Key: {key}\n"
                "    m_Metadata:\n"
                "      m_Items: []\n"
            )
            en_entries.append(
                f"  - m_Id: {entry_id}\n"
                f"    m_Localized: {json.dumps(en, ensure_ascii=False)}\n"
                "    m_Metadata:\n"
                "      m_Items: []\n"
            )
            ko_entries.append(
                f"  - m_Id: {entry_id}\n"
                f"    m_Localized: {json.dumps(ko, ensure_ascii=True)}\n"
                "    m_Metadata:\n"
                "      m_Items: []\n"
            )
            added += 1

    if dry_run:
        print(f"[materialize] would add {added} localization entries")
        return added
    if shared_entries:
        append_before_table_end(SKILL_SHARED_TABLE, shared_entries)
        append_before_table_end(SKILL_EN_TABLE, en_entries)
        append_before_table_end(SKILL_KO_TABLE, ko_entries)
    return added


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=CATALOG_PATH)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    catalog = load_yaml(args.catalog)
    skills_written = write_skill_assets(catalog, args.dry_run)
    localization_added = update_localization(catalog, args.dry_run)
    verb = "would write" if args.dry_run else "wrote"
    print(
        f"[materialize_skill_expansion_assets] {verb} "
        f"{skills_written} skill assets and {localization_added} localization entries"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
