import type { AssetItem, SfxQcStatus } from "../types";

export type SfxNeededCategory = "combat_common" | "skill" | "status";
export type SfxCoverageStatus = "made" | "missing";

export interface SfxNeededItem {
  id: string;
  hookId: string;
  runtimeHookId: string;
  variantKey: string | null;
  profileKey: string | null;
  variantLabel: string | null;
  materialLabel: string | null;
  category: SfxNeededCategory;
  phase: string;
  label: string;
  source: string;
  description: string;
}

export interface SfxCoverageRow extends SfxNeededItem {
  assets: AssetItem[];
  madeCount: number;
  coverageStatus: SfxCoverageStatus;
  qcStatus: SfxQcStatus;
  qcLabel: string;
  primaryAsset: AssetItem | null;
}

const skillIds = [
  "skill_aegis_intercept",
  "skill_aegis_linebreaker",
  "skill_aegis_sentinel_oath",
  "skill_ash_step",
  "skill_bulwark_core",
  "skill_bulwark_utility",
  "skill_cinder_overrun",
  "skill_echo_resonance",
  "skill_ember_arrow",
  "skill_fracture_step",
  "skill_guardian_core",
  "skill_guardian_utility",
  "skill_hexer_core",
  "skill_hexer_utility",
  "skill_hunter_utility",
  "skill_iron_pelt_maul",
  "skill_iron_pelt_roar",
  "skill_marksman_core",
  "skill_marksman_utility",
  "skill_memory_tuning",
  "skill_minor_heal",
  "skill_mirror_cut",
  "skill_phase_tether",
  "skill_power_strike",
  "skill_precision_shot",
  "skill_priest_core",
  "skill_prism_lance",
  "skill_raider_core",
  "skill_raider_utility",
  "skill_reaver_core",
  "skill_reaver_utility",
  "skill_refracting_snare",
  "skill_riposte_angle",
  "skill_rusthide_charge",
  "skill_scout_core",
  "skill_scout_utility",
  "skill_shaman_core",
  "skill_shaman_utility",
  "skill_shardblade_sever",
  "skill_signal_flare",
  "skill_slayer_core",
  "skill_slayer_utility",
  "skill_square_wall",
  "skill_warden_utility",
] as const;

const statusIds = [
  "barrier",
  "bleed",
  "burn",
  "exposed",
  "guarded",
  "marked",
  "root",
  "silence",
  "slow",
  "sunder",
  "unstoppable",
  "wound",
] as const;

const combatVariants: Array<{
  runtimeHookId: string;
  hookId: string;
  variantKey: string;
  variantLabel: string;
  materialLabel: string;
  phase: string;
  label: string;
  source: string;
  description: string;
}> = [
  {
    runtimeHookId: "sfx.combat.impact_damage",
    hookId: "sfx.combat.impact_damage.flesh.light",
    variantKey: "combat.impact.flesh.light",
    variantLabel: "Flesh light",
    materialLabel: "Flesh / Cloth",
    phase: "ImpactDamage",
    label: "Impact flesh light",
    source: "BattleActorAudioSurface",
    description: "살/천에 가까운 가벼운 피격 후보입니다.",
  },
  {
    runtimeHookId: "sfx.combat.impact_damage",
    hookId: "sfx.combat.impact_damage.leather.light",
    variantKey: "combat.impact.leather.light",
    variantLabel: "Leather light",
    materialLabel: "Leather",
    phase: "ImpactDamage",
    label: "Impact leather light",
    source: "BattleActorAudioSurface",
    description: "가죽 방어구에 닿는 가벼운 피격 후보입니다.",
  },
  {
    runtimeHookId: "sfx.combat.impact_damage",
    hookId: "sfx.combat.impact_damage.metal.light",
    variantKey: "combat.impact.metal.light",
    variantLabel: "Metal light",
    materialLabel: "Small Metal",
    phase: "ImpactDamage",
    label: "Impact metal light",
    source: "BattleActorAudioSurface",
    description: "작은 금속 방어구나 장식의 짧은 접촉 후보입니다.",
  },
  {
    runtimeHookId: "sfx.combat.impact_damage",
    hookId: "sfx.combat.impact_damage.wood_block.light",
    variantKey: "combat.impact.wood_block.light",
    variantLabel: "Wood block light",
    materialLabel: "Wood Block",
    phase: "ImpactDamage",
    label: "Impact wood block light",
    source: "BattleActorAudioSurface",
    description: "목재 방패나 막는 표면에 닿는 block성 접촉 후보입니다.",
  },
  {
    runtimeHookId: "sfx.combat.guard_enter",
    hookId: "sfx.combat.guard_enter.wood_shield",
    variantKey: "combat.guard_enter.wood_shield",
    variantLabel: "Wood shield",
    materialLabel: "Wood / Strap",
    phase: "GuardEnter",
    label: "Guard wood shield",
    source: "BattleActorAudioSurface",
    description: "나무 방패를 들어 올리는 방어 준비음 후보입니다.",
  },
  {
    runtimeHookId: "sfx.combat.guard_enter",
    hookId: "sfx.combat.guard_enter.leather_bracer",
    variantKey: "combat.guard_enter.leather_bracer",
    variantLabel: "Leather bracer",
    materialLabel: "Leather Strap",
    phase: "GuardEnter",
    label: "Guard leather bracer",
    source: "BattleActorAudioSurface",
    description: "가죽 bracer나 strap을 당기는 방어 준비음 후보입니다.",
  },
  {
    runtimeHookId: "sfx.combat.reposition_start",
    hookId: "sfx.combat.reposition_start.boot_dirt",
    variantKey: "combat.reposition_start.boot_dirt",
    variantLabel: "Boot dirt start",
    materialLabel: "Boot / Dirt",
    phase: "RepositionStart",
    label: "Reposition start boot dirt",
    source: "BattleActorAudioSurface",
    description: "흙/돌 바닥에서 전술 이동을 시작하는 짧은 발 긁힘 후보입니다.",
  },
  {
    runtimeHookId: "sfx.combat.reposition_stop",
    hookId: "sfx.combat.reposition_stop.boot_dirt",
    variantKey: "combat.reposition_stop.boot_dirt",
    variantLabel: "Boot dirt stop",
    materialLabel: "Boot / Dirt",
    phase: "RepositionStop",
    label: "Reposition stop boot dirt",
    source: "BattleActorAudioSurface",
    description: "흙/돌 바닥에서 이동을 멈추는 짧은 정지/착지 후보입니다.",
  },
  {
    runtimeHookId: "sfx.combat.action_commit_basic",
    hookId: "sfx.combat.action_commit_basic.blade_light",
    variantKey: "combat.action_commit.blade_light",
    variantLabel: "Blade light",
    materialLabel: "Blade / Body Motion",
    phase: "ActionCommitBasic",
    label: "Basic commit blade light",
    source: "BattleActorAudioSurface",
    description: "기본 공격 시작의 가벼운 blade와 몸동작 후보입니다.",
  },
  {
    runtimeHookId: "sfx.combat.death_start",
    hookId: "sfx.combat.death_start.humanoid_light",
    variantKey: "combat.death_start.humanoid_light",
    variantLabel: "Humanoid light",
    materialLabel: "Humanoid Body",
    phase: "DeathStart",
    label: "Death humanoid light",
    source: "BattleActorAudioSurface",
    description: "보이스 없는 인간형 쓰러짐 시작 후보입니다.",
  },
];

export const SFX_NEEDED_ITEMS: SfxNeededItem[] = [
  ...combatVariants.map((variant) => ({
    id: variant.hookId,
    hookId: variant.hookId,
    runtimeHookId: variant.runtimeHookId,
    variantKey: variant.variantKey,
    profileKey: variant.variantKey,
    variantLabel: variant.variantLabel,
    materialLabel: variant.materialLabel,
    category: "combat_common" as const,
    phase: variant.phase,
    label: variant.label,
    source: variant.source,
    description: variant.description,
  })),
  ...skillIds.flatMap((skillId) =>
    (["cast", "impact"] as const).map((phase) => ({
      id: `sfx.skill.${skillId}.${phase}`,
      hookId: `sfx.skill.${skillId}.${phase}`,
      runtimeHookId: `sfx.skill.${skillId}`,
      variantKey: null,
      profileKey: null,
      variantLabel: `${phase} phase`,
      materialLabel: null,
      category: "skill" as const,
      phase,
      label: `${readableId(skillId)} ${phase}`,
      source: `${skillId}.asset`,
      description:
        phase === "cast"
          ? `${readableId(skillId)} 스킬의 시전 시작 또는 release 후보입니다.`
          : `${readableId(skillId)} 스킬의 contact 또는 명중 순간 후보입니다.`,
    })),
  ),
  ...statusIds.map((statusId) => ({
    id: `sfx.status.${statusId}.apply`,
    hookId: `sfx.status.${statusId}.apply`,
    runtimeHookId: `sfx.status.${statusId}.apply`,
    variantKey: null,
    profileKey: null,
    variantLabel: "apply phase",
    materialLabel: null,
    category: "status" as const,
    phase: "apply",
    label: `${readableId(statusId)} apply`,
    source: `status_family_${statusId}.asset`,
    description: `${readableId(statusId)} 상태가 대상에게 적용되는 순간의 후보입니다.`,
  })),
];

export function buildSfxCoverage(assets: AssetItem[]): SfxCoverageRow[] {
  const sfxAssets = assets.filter((asset) => asset.assetType === "sfx");
  return SFX_NEEDED_ITEMS.map((item) => {
    const matches = sfxAssets.filter((asset) => matchesNeededItem(asset, item));
    const primaryAsset = selectPrimaryAsset(matches);
    const qcStatus = primaryAsset?.sfxQcStatus ?? "missing";
    return {
      ...item,
      assets: matches,
      madeCount: matches.length,
      coverageStatus: matches.length > 0 ? "made" : "missing",
      qcStatus,
      qcLabel: primaryAsset?.sfxQcLabel ?? qcLabel(qcStatus),
      primaryAsset,
    };
  });
}

export function unmatchedSfxAssets(assets: AssetItem[], coverage: SfxCoverageRow[]): AssetItem[] {
  const matchedIds = new Set(coverage.flatMap((row) => row.assets.map((asset) => asset.id)));
  return assets.filter((asset) => asset.assetType === "sfx" && !matchedIds.has(asset.id));
}

export function qcLabel(status: SfxQcStatus): string {
  switch (status) {
    case "red":
      return "QC Red";
    case "yellow":
      return "QC Yellow";
    case "green":
      return "QC Green";
    case "missing":
      return "QC Missing";
    case "unknown":
      return "QC Unknown";
  }
}

export function materialLabelForVariantKey(variantKey: string | null): string | null {
  if (!variantKey) {
    return null;
  }

  const match = SFX_NEEDED_ITEMS.find((item) => item.variantKey === variantKey);
  return match?.materialLabel ?? match?.variantLabel ?? null;
}

function matchesNeededItem(asset: AssetItem, item: SfxNeededItem): boolean {
  if (asset.sfxHookId === item.hookId) {
    return true;
  }
  if (item.variantKey && asset.sfxVariantKey === item.variantKey) {
    return true;
  }
  if (!item.variantKey && asset.sfxRuntimeHookId === item.runtimeHookId) {
    return true;
  }
  return false;
}

function selectPrimaryAsset(assets: AssetItem[]): AssetItem | null {
  return [...assets].sort((left, right) => {
    const qcDelta = qcRank(right.sfxQcStatus) - qcRank(left.sfxQcStatus);
    if (qcDelta !== 0) {
      return qcDelta;
    }
    return right.modifiedMs - left.modifiedMs;
  })[0] ?? null;
}

function qcRank(status: SfxQcStatus | null): number {
  switch (status) {
    case "green":
      return 5;
    case "yellow":
      return 4;
    case "unknown":
      return 3;
    case "missing":
      return 2;
    case "red":
      return 1;
    default:
      return 0;
  }
}

function readableId(id: string): string {
  return id
    .replace(/^skill_/, "")
    .split("_")
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}
