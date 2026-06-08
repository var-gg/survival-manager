import type { AssetItem } from "../types";

export interface AssetReviewContext {
  hookId: string;
  runtimeHookId: string | null;
  variantKey: string | null;
  profileKey: string | null;
  category: string;
  trigger: string;
  whereUsed: string;
  variantRole: string | null;
  reviewFocus: string;
  source: string;
  generationSummary: string | null;
}

interface RuntimeCueInfo {
  category: string;
  trigger: string;
  socket: string;
  whereUsed: string;
  reviewFocus: string;
}

interface VariantProfileInfo {
  category: string;
  trigger: string;
  whereUsed: string;
  variantRole: string;
  reviewFocus: string;
}

interface InferredSfxIdentity {
  hookId: string;
  runtimeHookId: string | null;
  variantKey: string | null;
  profileKey: string | null;
}

const runtimeCueInfo: Record<string, RuntimeCueInfo> = {
  "sfx.combat.action_commit_basic": {
    category: "Combat Runtime Cue",
    trigger: "Basic attack commit",
    socket: "ProjectileOrigin",
    whereUsed:
      "BattleActorAudioSurface가 ActionCommitBasic cue를 받을 때 기본 공격 시작점에서 받는 runtime hook입니다.",
    reviewFocus:
      "이 hook 자체는 단일 소재 사운드가 아닙니다. blade, bow, body motion 같은 variant metadata가 있는 후보를 우선 검수합니다.",
  },
  "sfx.combat.action_commit_skill": {
    category: "Combat Runtime Cue",
    trigger: "Skill commit",
    socket: "ProjectileOrigin",
    whereUsed:
      "BattleActorAudioSurface가 ActionCommitSkill cue를 받을 때 스킬 시전 시작점에서 받는 runtime hook입니다.",
    reviewFocus:
      "공용 fallback cue이며 개별 스킬 cast 후보와 역할이 겹치지 않는지 확인합니다.",
  },
  "sfx.combat.action_commit_heal": {
    category: "Combat Runtime Cue",
    trigger: "Heal commit",
    socket: "Cast",
    whereUsed:
      "BattleActorAudioSurface가 ActionCommitHeal cue를 받을 때 회복 계열 시전 시작점에서 받는 runtime hook입니다.",
    reviewFocus:
      "회복 onset 후보로 검수하되 공격 impact나 긴 melody처럼 들리는 파일은 제외합니다.",
  },
  "sfx.combat.impact_damage": {
    category: "Combat Runtime Cue",
    trigger: "Damage impact",
    socket: "Hit",
    whereUsed:
      "BattleActorAudioSurface가 ImpactDamage cue를 받을 때 받는 runtime hook입니다. 실제 생성/검수는 소재별 variant로 나눕니다.",
    reviewFocus:
      "runtime hook만 있는 후보는 검수 단위가 너무 넓습니다. flesh/leather/metal/wood_block 같은 variant가 명시됐는지 먼저 확인합니다.",
  },
  "sfx.combat.impact_heal": {
    category: "Combat Runtime Cue",
    trigger: "Heal impact",
    socket: "Hit",
    whereUsed:
      "BattleActorAudioSurface가 ImpactHeal cue를 받을 때 회복이 대상에게 들어가는 지점에서 받는 runtime hook입니다.",
    reviewFocus:
      "피격음으로 오해되지 않는 짧은 회복 도착감인지 확인합니다.",
  },
  "sfx.combat.guard_enter": {
    category: "Combat Runtime Cue",
    trigger: "Guard enter",
    socket: "Center",
    whereUsed:
      "BattleActorAudioSurface가 GuardEnter cue를 받을 때 방어 태세 진입 순간에 받는 runtime hook입니다.",
    reviewFocus:
      "wood_shield, leather_bracer, metal_shield 같은 variant가 명시됐는지 확인합니다.",
  },
  "sfx.combat.guard_exit": {
    category: "Combat Runtime Cue",
    trigger: "Guard exit",
    socket: "Center",
    whereUsed:
      "BattleActorAudioSurface가 GuardExit cue를 받을 때 방어 태세가 풀리는 순간에 받는 runtime hook입니다.",
    reviewFocus:
      "해제감은 있으나 실패/피격음처럼 들리지 않는지 확인합니다.",
  },
  "sfx.combat.reposition_start": {
    category: "Combat Runtime Cue",
    trigger: "Reposition start",
    socket: "FeetRing",
    whereUsed:
      "BattleActorAudioSurface가 RepositionStart cue를 받을 때 이동/재배치 시작 발밑에서 받는 runtime hook입니다.",
    reviewFocus:
      "boot_dirt, boot_stone 같은 지면/동작 variant가 명시됐는지 확인합니다.",
  },
  "sfx.combat.reposition_stop": {
    category: "Combat Runtime Cue",
    trigger: "Reposition stop",
    socket: "FeetRing",
    whereUsed:
      "BattleActorAudioSurface가 RepositionStop cue를 받을 때 이동/재배치 종료 발밑에서 받는 runtime hook입니다.",
    reviewFocus:
      "착지/정지감이 짧고 피격음처럼 무겁지 않은지 확인합니다.",
  },
  "sfx.combat.death_start": {
    category: "Combat Runtime Cue",
    trigger: "Death start",
    socket: "Center",
    whereUsed:
      "BattleActorAudioSurface가 DeathStart cue를 받을 때 사망 연출 시작점에서 받는 runtime hook입니다.",
    reviewFocus:
      "humanoid_light, armored_light 같은 variant가 명시됐는지 확인하고 보이스/울음은 제외합니다.",
  },
};

const variantProfileInfo: Record<string, VariantProfileInfo> = {
  "combat.impact.flesh.light": {
    category: "Combat Impact Variant",
    trigger: "ImpactDamage / flesh light",
    whereUsed:
      "ImpactDamage runtime hook에서 살/천에 가까운 가벼운 피격을 표현할 때 선택할 후보입니다.",
    variantRole:
      "살/천 body thump용입니다. 가죽 방어구, 금속 갑주, 목재 block을 대신하지 않습니다.",
    reviewFocus:
      "짧은 body/cloth 접촉으로 들리는지, 금속 clink나 대형 충돌 저역이 섞이지 않았는지 확인합니다.",
  },
  "combat.impact.leather.light": {
    category: "Combat Impact Variant",
    trigger: "ImpactDamage / leather light",
    whereUsed:
      "ImpactDamage runtime hook에서 가죽 방어구에 닿는 가벼운 피격을 표현할 때 선택할 후보입니다.",
    variantRole:
      "가죽과 천의 짧은 접촉용입니다. 금속 갑주 충돌, 목재 방패 block, 살 타격을 모두 커버하지 않습니다.",
    reviewFocus:
      "leather creak와 cloth thump가 짧게 읽히는지, 차량 충돌/오함마/대형 금속음처럼 과장되지 않았는지 확인합니다.",
  },
  "combat.impact.metal.light": {
    category: "Combat Impact Variant",
    trigger: "ImpactDamage / metal light",
    whereUsed:
      "ImpactDamage runtime hook에서 작은 금속 방어구나 장식에 닿는 가벼운 접촉을 표현할 때 선택할 후보입니다.",
    variantRole:
      "짧은 금속 clink용입니다. 대형 갑옷 충돌이나 heavy hammer hit가 아닙니다.",
    reviewFocus:
      "밝고 짧은 clink가 있고 body thump가 과하지 않은지, trailer boom으로 커지지 않았는지 확인합니다.",
  },
  "combat.impact.wood_block.light": {
    category: "Combat Impact Variant",
    trigger: "ImpactDamage / wood block light",
    whereUsed:
      "ImpactDamage runtime hook에서 목재 방패나 막는 표면에 닿는 가벼운 block성 접촉을 표현할 때 선택할 후보입니다.",
    variantRole:
      "wood shield/block용입니다. 일반 살 피격이나 금속 갑주 피격을 대신하지 않습니다.",
    reviewFocus:
      "짧은 hollow wood knock으로 들리는지, 폭발/큰 충돌/피격 body thump로 흐려지지 않았는지 확인합니다.",
  },
  "combat.guard_enter.wood_shield": {
    category: "Guard Variant",
    trigger: "GuardEnter / wood shield",
    whereUsed:
      "GuardEnter runtime hook에서 목재 방패를 들어 올리는 방어 태세 진입음 후보입니다.",
    variantRole:
      "방패 준비 동작용입니다. 맞는 소리나 block impact가 아닙니다.",
    reviewFocus:
      "작은 wood/strap movement로 들리는지, 피격이나 방패 충돌처럼 강하지 않은지 확인합니다.",
  },
  "combat.guard_enter.leather_bracer": {
    category: "Guard Variant",
    trigger: "GuardEnter / leather bracer",
    whereUsed:
      "GuardEnter runtime hook에서 가죽 bracer나 strap을 당겨 방어 자세를 잡는 후보입니다.",
    variantRole:
      "방어 준비의 가까운 장비 foley용입니다. 공격 시작음이나 피격음이 아닙니다.",
    reviewFocus:
      "leather strap tension과 cloth shift가 짧게 읽히는지 확인합니다.",
  },
  "combat.reposition_start.boot_dirt": {
    category: "Reposition Variant",
    trigger: "RepositionStart / boot dirt",
    whereUsed:
      "RepositionStart runtime hook에서 흙/돌 바닥 위 짧은 전술 이동 시작을 표현할 후보입니다.",
    variantRole:
      "발 긁힘과 작은 먼지 scuff용입니다. 공격 dash, 무기 swing, 착지 impact가 아닙니다.",
    reviewFocus:
      "짧은 boot scrape와 cloth movement로 읽히는지, 대시 폭발이나 hit처럼 들리지 않는지 확인합니다.",
  },
  "combat.reposition_stop.boot_dirt": {
    category: "Reposition Variant",
    trigger: "RepositionStop / boot dirt",
    whereUsed:
      "RepositionStop runtime hook에서 흙/돌 바닥 위 짧은 이동 종료를 표현할 후보입니다.",
    variantRole:
      "정지/착지의 작은 발 foley용입니다. damage impact가 아닙니다.",
    reviewFocus:
      "짧고 가벼운 stop으로 들리는지, 피격음처럼 무겁지 않은지 확인합니다.",
  },
};

const combatRuntimePrefixes = [
  "sfx.combat.action_commit_basic",
  "sfx.combat.action_commit_skill",
  "sfx.combat.action_commit_heal",
  "sfx.combat.impact_damage",
  "sfx.combat.impact_heal",
  "sfx.combat.guard_enter",
  "sfx.combat.guard_exit",
  "sfx.combat.reposition_start",
  "sfx.combat.reposition_stop",
  "sfx.combat.death_start",
];

export function buildAssetReviewContext(
  asset: AssetItem,
  sidecar: unknown | null,
): AssetReviewContext | null {
  const sidecarRecord = asRecord(sidecar);
  const explicit = sidecarRecord ? explicitReviewContext(sidecarRecord) : null;
  if (explicit) {
    return explicit;
  }

  if (asset.assetType !== "sfx" && asset.assetType !== "voice" && asset.assetType !== "bgm") {
    return null;
  }

  const prompt = sidecarRecord ? stringValue(sidecarRecord.prompt) ?? settingsPrompt(sidecarRecord) : null;
  const settings = sidecarRecord ? asRecord(sidecarRecord.settings) : null;
  const outputName = settings ? stringValue(settings.output_name) : null;
  const inferred = inferSfxIdentity(prompt, outputName, asset.name);

  if (!inferred) {
    return null;
  }

  return buildContext(inferred, "derived from sidecar prompt/settings", sidecarRecord);
}

function explicitReviewContext(record: Record<string, unknown>): AssetReviewContext | null {
  const source = asRecord(record.review_context) ?? asRecord(record.reviewContext);
  if (!source) {
    return null;
  }

  const hookId =
    stringValue(source.hook_id) ??
    stringValue(source.hookId) ??
    stringValue(record.hook_id) ??
    stringValue(record.hookId) ??
    stringValue(record.variant_id) ??
    stringValue(record.variantId);
  const promptText = [
    stringValue(record.prompt),
    settingsPrompt(record),
    stringValue(asRecord(record.settings)?.output_name),
    stringValue(asRecord(record.settings)?.outputName),
  ]
    .filter((value): value is string => value !== null)
    .join(" ")
    .toLowerCase();
  const runtimeHookId =
    stringValue(source.runtime_hook_id) ??
    stringValue(source.runtimeHookId) ??
    stringValue(record.runtime_hook_id) ??
    stringValue(record.runtimeHookId) ??
    (hookId ? runtimeHookFromHookId(hookId) : null);
  const inferredVariant = inferVariantForRuntime(runtimeHookId, promptText);
  const variantKey =
    stringValue(source.variant_key) ??
    stringValue(source.variantKey) ??
    stringValue(record.variant_key) ??
    stringValue(record.variantKey) ??
    (hookId ? variantKeyFromHookId(hookId) : null) ??
    inferredVariant;
  const profileKey =
    stringValue(source.profile_key) ??
    stringValue(source.profileKey) ??
    stringValue(record.profile_key) ??
    stringValue(record.profileKey) ??
    variantKey;
  const effectiveHookId = hookId ?? (variantKey ? hookIdFromVariantKey(variantKey) : null) ?? runtimeHookId;

  if (!effectiveHookId) {
    return null;
  }

  const inferred: InferredSfxIdentity = {
    hookId: effectiveHookId,
    runtimeHookId,
    variantKey,
    profileKey,
  };
  const base = describeIdentity(inferred);

  return {
    ...base,
    hookId: effectiveHookId,
    runtimeHookId,
    variantKey,
    profileKey,
    category: stringValue(source.category) ?? base.category,
    trigger: stringValue(source.trigger) ?? base.trigger,
    whereUsed: stringValue(source.where_used) ?? stringValue(source.whereUsed) ?? base.whereUsed,
    variantRole:
      stringValue(source.variant_role) ??
      stringValue(source.variantRole) ??
      stringValue(source.role) ??
      base.variantRole,
    reviewFocus:
      stringValue(source.review_focus) ??
      stringValue(source.reviewFocus) ??
      "검수 기준이 sidecar에 명시되지 않았습니다.",
    source: "sidecar review_context",
    generationSummary: generationSummary(record),
  };
}

function buildContext(
  identity: InferredSfxIdentity,
  source: string,
  record: Record<string, unknown> | null,
): AssetReviewContext {
  return {
    ...describeIdentity(identity),
    ...identity,
    source,
    generationSummary: generationSummary(record),
  };
}

function describeIdentity(
  identity: InferredSfxIdentity,
): Omit<AssetReviewContext, "hookId" | "runtimeHookId" | "variantKey" | "profileKey" | "source" | "generationSummary"> {
  const profile = identity.profileKey ? variantProfileInfo[identity.profileKey] : null;
  const variant = identity.variantKey ? variantProfileInfo[identity.variantKey] : null;
  const variantInfo = profile ?? variant;
  if (variantInfo) {
    return {
      category: variantInfo.category,
      trigger: variantInfo.trigger,
      whereUsed: variantInfo.whereUsed,
      variantRole: variantInfo.variantRole,
      reviewFocus: variantInfo.reviewFocus,
    };
  }

  const runtimeHookId = identity.runtimeHookId ?? runtimeHookFromHookId(identity.hookId);
  const runtime = runtimeHookId ? runtimeCueInfo[runtimeHookId] : null;
  if (runtime) {
    return {
      category: runtime.category,
      trigger: `${runtime.trigger} / ${runtime.socket}`,
      whereUsed: runtime.whereUsed,
      variantRole: "소재/강도 variant가 아직 명시되지 않았습니다.",
      reviewFocus: runtime.reviewFocus,
    };
  }

  const skill = /^sfx\.skill\.([a-z0-9_]+)\.(cast|impact)$/.exec(identity.hookId);
  if (skill) {
    const skillId = skill[1];
    const phase = skill[2];
    const readable = readableId(skillId.replace(/^skill_/, ""));
    const baseHook = `sfx.skill.${skillId}`;
    return {
      category: "Skill",
      trigger: phase === "cast" ? "Skill cast" : "Skill impact",
      whereUsed:
        phase === "cast"
          ? `${readable} 스킬의 cast 후보입니다. Unity authored asset에는 base hook ${baseHook}가 저장되고, 이 파일은 시전/windup release 쪽에 붙일 생성 후보로 검수합니다.`
          : `${readable} 스킬의 impact 후보입니다. Unity authored asset에는 base hook ${baseHook}가 저장되고, 이 파일은 contact/명중 순간에 붙일 생성 후보로 검수합니다.`,
      variantRole: null,
      reviewFocus:
        phase === "cast"
          ? "시전 시작과 release가 분명한지, impact hit처럼 들리거나 tail이 과하게 길지 않은지 확인합니다."
          : "명중 순간의 body/transient가 읽히고 cast 사운드와 역할이 겹치지 않는지 확인합니다.",
    };
  }

  const status = /^sfx\.status\.([a-z0-9_]+)\.apply$/.exec(identity.hookId);
  if (status) {
    const statusId = status[1];
    return {
      category: "Status",
      trigger: "Status apply",
      whereUsed: `${readableId(statusId)} 상태가 대상에게 적용되는 순간에 붙일 StatusFamilyDefinition apply cue 후보입니다. 현재 status 전용 presentation surface는 보류 상태라, 후속 배선 전 검수용으로 봅니다.`,
      variantRole: null,
      reviewFocus: "상태 식별 질감이 짧게 읽히고 전투 impact나 UI click처럼 들리지 않는지 확인합니다.",
    };
  }

  return {
    category: "SFX",
    trigger: "Unknown hook",
    whereUsed: "hook id 규칙에는 맞지만 앱이 아직 구체적인 사용처를 분류하지 못한 후보입니다.",
    variantRole: null,
    reviewFocus: "sidecar prompt와 hook id contract를 함께 확인해야 합니다.",
  };
}

function inferSfxIdentity(
  prompt: string | null,
  outputName: string | null,
  fileName: string,
): InferredSfxIdentity | null {
  const text = [prompt, outputName, fileName].filter(Boolean).join(" ");
  const explicitHook = /\bsfx\.[a-z0-9_]+(?:[._][a-z0-9_]+)*\b/.exec(text);
  if (explicitHook) {
    const hookId = explicitHook[0];
    const runtimeHookId = runtimeHookFromHookId(hookId);
    const variantKey = variantKeyFromHookId(hookId);
    return {
      hookId,
      runtimeHookId,
      variantKey,
      profileKey: variantKey,
    };
  }

  const lowered = text.toLowerCase();
  if (lowered.includes("impact-damage") || lowered.includes("impact damage")) {
    return combatIdentity("sfx.combat.impact_damage", inferImpactVariant(lowered));
  }
  if (lowered.includes("guard-enter") || lowered.includes("guard enter")) {
    return combatIdentity("sfx.combat.guard_enter", inferGuardVariant(lowered));
  }
  if (lowered.includes("reposition-start") || lowered.includes("reposition start")) {
    return combatIdentity("sfx.combat.reposition_start", inferRepositionStartVariant(lowered));
  }
  if (lowered.includes("reposition-stop") || lowered.includes("reposition stop")) {
    return combatIdentity("sfx.combat.reposition_stop", inferRepositionStopVariant(lowered));
  }
  if (lowered.includes("prism-lance-cast")) {
    return skillIdentity("skill_prism_lance", "cast");
  }
  if (lowered.includes("burn-apply")) {
    return statusIdentity("burn");
  }
  return null;
}

function combatIdentity(runtimeHookId: string, variantKey: string | null): InferredSfxIdentity {
  return {
    hookId: variantKey ? hookIdFromVariantKey(variantKey) ?? runtimeHookId : runtimeHookId,
    runtimeHookId,
    variantKey,
    profileKey: variantKey,
  };
}

function skillIdentity(skillId: string, phase: "cast" | "impact"): InferredSfxIdentity {
  return {
    hookId: `sfx.skill.${skillId}.${phase}`,
    runtimeHookId: `sfx.skill.${skillId}`,
    variantKey: null,
    profileKey: null,
  };
}

function statusIdentity(statusId: string): InferredSfxIdentity {
  const hookId = `sfx.status.${statusId}.apply`;
  return {
    hookId,
    runtimeHookId: hookId,
    variantKey: null,
    profileKey: null,
  };
}

function inferImpactVariant(text: string): string | null {
  if (text.includes("wood") || text.includes("shield") || text.includes("block")) {
    return "combat.impact.wood_block.light";
  }
  if (text.includes("leather")) {
    return "combat.impact.leather.light";
  }
  if (text.includes("metal") || text.includes("clink") || text.includes("buckle")) {
    return "combat.impact.metal.light";
  }
  if (text.includes("flesh") || text.includes("body") || text.includes("cloth")) {
    return "combat.impact.flesh.light";
  }
  return null;
}

function inferGuardVariant(text: string): string | null {
  if (text.includes("wood") || text.includes("shield")) {
    return "combat.guard_enter.wood_shield";
  }
  if (text.includes("leather") || text.includes("bracer") || text.includes("strap")) {
    return "combat.guard_enter.leather_bracer";
  }
  return null;
}

function inferRepositionStartVariant(text: string): string | null {
  if (text.includes("boot") || text.includes("dirt") || text.includes("stone") || text.includes("dust")) {
    return "combat.reposition_start.boot_dirt";
  }
  return null;
}

function inferRepositionStopVariant(text: string): string | null {
  if (text.includes("boot") || text.includes("dirt") || text.includes("stone") || text.includes("dust")) {
    return "combat.reposition_stop.boot_dirt";
  }
  return null;
}

function inferVariantForRuntime(runtimeHookId: string | null, text: string): string | null {
  switch (runtimeHookId) {
    case "sfx.combat.impact_damage":
      return inferImpactVariant(text);
    case "sfx.combat.guard_enter":
      return inferGuardVariant(text);
    case "sfx.combat.reposition_start":
      return inferRepositionStartVariant(text);
    case "sfx.combat.reposition_stop":
      return inferRepositionStopVariant(text);
    default:
      return null;
  }
}

function runtimeHookFromHookId(hookId: string): string | null {
  const skill = /^sfx\.skill\.([a-z0-9_]+)\.(cast|impact)$/.exec(hookId);
  if (skill) {
    return `sfx.skill.${skill[1]}`;
  }

  if (/^sfx\.status\.[a-z0-9_]+\.apply$/.test(hookId)) {
    return hookId;
  }

  return combatRuntimePrefixes.find((prefix) => hookId === prefix || hookId.startsWith(`${prefix}.`)) ?? null;
}

function variantKeyFromHookId(hookId: string): string | null {
  if (hookId.startsWith("sfx.combat.impact_damage.")) {
    return `combat.impact.${hookId.slice("sfx.combat.impact_damage.".length)}`;
  }
  if (hookId.startsWith("sfx.combat.guard_enter.")) {
    return `combat.guard_enter.${hookId.slice("sfx.combat.guard_enter.".length)}`;
  }
  if (hookId.startsWith("sfx.combat.guard_exit.")) {
    return `combat.guard_exit.${hookId.slice("sfx.combat.guard_exit.".length)}`;
  }
  if (hookId.startsWith("sfx.combat.reposition_start.")) {
    return `combat.reposition_start.${hookId.slice("sfx.combat.reposition_start.".length)}`;
  }
  if (hookId.startsWith("sfx.combat.reposition_stop.")) {
    return `combat.reposition_stop.${hookId.slice("sfx.combat.reposition_stop.".length)}`;
  }
  if (hookId.startsWith("sfx.combat.action_commit_basic.")) {
    return `combat.action_commit.${hookId.slice("sfx.combat.action_commit_basic.".length)}`;
  }
  if (hookId.startsWith("sfx.combat.death_start.")) {
    return `combat.death_start.${hookId.slice("sfx.combat.death_start.".length)}`;
  }
  return null;
}

function hookIdFromVariantKey(variantKey: string): string | null {
  if (variantKey.startsWith("combat.impact.")) {
    return `sfx.combat.impact_damage.${variantKey.slice("combat.impact.".length)}`;
  }
  if (variantKey.startsWith("combat.guard_enter.")) {
    return `sfx.combat.guard_enter.${variantKey.slice("combat.guard_enter.".length)}`;
  }
  if (variantKey.startsWith("combat.guard_exit.")) {
    return `sfx.combat.guard_exit.${variantKey.slice("combat.guard_exit.".length)}`;
  }
  if (variantKey.startsWith("combat.reposition_start.")) {
    return `sfx.combat.reposition_start.${variantKey.slice("combat.reposition_start.".length)}`;
  }
  if (variantKey.startsWith("combat.reposition_stop.")) {
    return `sfx.combat.reposition_stop.${variantKey.slice("combat.reposition_stop.".length)}`;
  }
  if (variantKey.startsWith("combat.action_commit.")) {
    return `sfx.combat.action_commit_basic.${variantKey.slice("combat.action_commit.".length)}`;
  }
  if (variantKey.startsWith("combat.death_start.")) {
    return `sfx.combat.death_start.${variantKey.slice("combat.death_start.".length)}`;
  }
  return null;
}

function generationSummary(record: Record<string, unknown> | null): string | null {
  if (!record) {
    return null;
  }

  const settings = asRecord(record.settings);
  const seconds = numberValue(record.seconds) ?? numberValue(settings?.seconds);
  const steps = numberValue(settings?.num_inference_steps);
  const cfg = numberValue(settings?.cfg_scale);
  const seed = numberValue(settings?.seed);
  const elapsed = numberValue(record.elapsed_seconds);

  const parts = [
    seconds !== null ? `${seconds}s` : null,
    steps !== null ? `${steps} steps` : null,
    cfg !== null ? `cfg ${cfg}` : null,
    seed !== null ? `seed ${seed}` : null,
    elapsed !== null ? `elapsed ${elapsed}s` : null,
  ].filter((value): value is string => value !== null);

  return parts.length > 0 ? parts.join(" / ") : null;
}

function settingsPrompt(record: Record<string, unknown>): string | null {
  const settings = asRecord(record.settings);
  return settings ? stringValue(settings.prompt) : null;
}

function readableId(id: string): string {
  return id
    .split("_")
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return value !== null && typeof value === "object" && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null;
}

function stringValue(value: unknown): string | null {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : null;
}

function numberValue(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}
