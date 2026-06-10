# SFX hook id contract

- 상태: active
- 소유자: repository
- 최종수정일: 2026-06-10
- 소스오브트루스: `docs/03_architecture/sfx-hook-id-contract.md`
- 관련문서:
  - `docs/03_architecture/content-authoring-and-balance-data.md`
  - `docs/03_architecture/battle-actor-wrapper-and-asset-intake-seam.md`

## 목적

이 문서는 Unity authored content와 battle audio surface가 공유하는 SFX hook id 규칙의 단일 기준이다. 또한 생성 주문서에서 runtime hook과 소재별 generation variant를 분리하는 규칙을 정의한다. 실제 wav/mp3 생성, MOSS 호출, 볼륨 믹싱, story line-level cue 설계는 이 문서가 아니라 후속 asset generation pipeline과 MediaCueSheet 설계에서 다룬다.

## 공통 규칙

- id는 `^[a-z0-9]+([._][a-z0-9]+)*$` 패턴을 따른다.
- skill/status authored asset은 생성 파일이 없어도 hook id를 먼저 가진다.
- `SfxHookId`는 소리 파일 경로가 아니라 생성 주문서와 runtime audio surface가 공유하는 안정 id다.
- 실제 파일명은 hook id를 기반으로 만들 수 있지만, 경로와 확장자는 asset pipeline 단계에서 결정한다.
- runtime hook id는 게임 이벤트의 연결점이다. 예: `sfx.combat.impact_damage`.
- generation variant id는 특정 소재/강도/용도의 생성 후보다. 예: `sfx.combat.impact_damage.leather.light`.
- Unity authored asset과 `BattleActorAudioSurface`에는 runtime hook id만 저장한다. generation variant id는 생성 매니페스트, sidecar, Asset Studio review metadata에서만 사용한다.
- 생성 sidecar는 `hook_id` 또는 `variant_id`와 함께 `runtime_hook_id`, `variant_key`, `profile_key`를 함께 기록한다.

## SkillDefinitionAsset

`SkillDefinitionAsset.SfxHookId`는 단일 canonical id validator를 통과해야 하므로 active skill의 base hook만 저장한다.

| 대상 | asset field | 생성 clip hook |
| --- | --- | --- |
| active skill cast | `sfx.skill.<skill_id>` | `sfx.skill.<skill_id>.cast` |
| active skill impact | `sfx.skill.<skill_id>` | `sfx.skill.<skill_id>.impact` |

적용 범위는 `SlotKind = CoreActive` 또는 `UtilityActive`인 44개 skill이다. Passive, support modifier, external support asset은 이번 SFX manifest 범위가 아니다.

예시:

| skill id | `SfxHookId` | 생성 대상 |
| --- | --- | --- |
| `skill_ember_arrow` | `sfx.skill.skill_ember_arrow` | `sfx.skill.skill_ember_arrow.cast`, `sfx.skill.skill_ember_arrow.impact` |
| `skill_minor_heal` | `sfx.skill.skill_minor_heal` | `sfx.skill.skill_minor_heal.cast`, `sfx.skill.skill_minor_heal.impact` |

### 현재 채워진 active skill hook

| asset | `SfxHookId` | 생성 hook |
| --- | --- | --- |
| `skill_aegis_intercept.asset` | `sfx.skill.skill_aegis_intercept` | `sfx.skill.skill_aegis_intercept.cast`, `sfx.skill.skill_aegis_intercept.impact` |
| `skill_aegis_linebreaker.asset` | `sfx.skill.skill_aegis_linebreaker` | `sfx.skill.skill_aegis_linebreaker.cast`, `sfx.skill.skill_aegis_linebreaker.impact` |
| `skill_aegis_sentinel_oath.asset` | `sfx.skill.skill_aegis_sentinel_oath` | `sfx.skill.skill_aegis_sentinel_oath.cast`, `sfx.skill.skill_aegis_sentinel_oath.impact` |
| `skill_ash_step.asset` | `sfx.skill.skill_ash_step` | `sfx.skill.skill_ash_step.cast`, `sfx.skill.skill_ash_step.impact` |
| `skill_bulwark_core.asset` | `sfx.skill.skill_bulwark_core` | `sfx.skill.skill_bulwark_core.cast`, `sfx.skill.skill_bulwark_core.impact` |
| `skill_bulwark_utility.asset` | `sfx.skill.skill_bulwark_utility` | `sfx.skill.skill_bulwark_utility.cast`, `sfx.skill.skill_bulwark_utility.impact` |
| `skill_cinder_overrun.asset` | `sfx.skill.skill_cinder_overrun` | `sfx.skill.skill_cinder_overrun.cast`, `sfx.skill.skill_cinder_overrun.impact` |
| `skill_echo_resonance.asset` | `sfx.skill.skill_echo_resonance` | `sfx.skill.skill_echo_resonance.cast`, `sfx.skill.skill_echo_resonance.impact` |
| `skill_ember_arrow.asset` | `sfx.skill.skill_ember_arrow` | `sfx.skill.skill_ember_arrow.cast`, `sfx.skill.skill_ember_arrow.impact` |
| `skill_fracture_step.asset` | `sfx.skill.skill_fracture_step` | `sfx.skill.skill_fracture_step.cast`, `sfx.skill.skill_fracture_step.impact` |
| `skill_guardian_core.asset` | `sfx.skill.skill_guardian_core` | `sfx.skill.skill_guardian_core.cast`, `sfx.skill.skill_guardian_core.impact` |
| `skill_guardian_utility.asset` | `sfx.skill.skill_guardian_utility` | `sfx.skill.skill_guardian_utility.cast`, `sfx.skill.skill_guardian_utility.impact` |
| `skill_hexer_core.asset` | `sfx.skill.skill_hexer_core` | `sfx.skill.skill_hexer_core.cast`, `sfx.skill.skill_hexer_core.impact` |
| `skill_hexer_utility.asset` | `sfx.skill.skill_hexer_utility` | `sfx.skill.skill_hexer_utility.cast`, `sfx.skill.skill_hexer_utility.impact` |
| `skill_hunter_utility.asset` | `sfx.skill.skill_hunter_utility` | `sfx.skill.skill_hunter_utility.cast`, `sfx.skill.skill_hunter_utility.impact` |
| `skill_iron_pelt_maul.asset` | `sfx.skill.skill_iron_pelt_maul` | `sfx.skill.skill_iron_pelt_maul.cast`, `sfx.skill.skill_iron_pelt_maul.impact` |
| `skill_iron_pelt_roar.asset` | `sfx.skill.skill_iron_pelt_roar` | `sfx.skill.skill_iron_pelt_roar.cast`, `sfx.skill.skill_iron_pelt_roar.impact` |
| `skill_marksman_core.asset` | `sfx.skill.skill_marksman_core` | `sfx.skill.skill_marksman_core.cast`, `sfx.skill.skill_marksman_core.impact` |
| `skill_marksman_utility.asset` | `sfx.skill.skill_marksman_utility` | `sfx.skill.skill_marksman_utility.cast`, `sfx.skill.skill_marksman_utility.impact` |
| `skill_memory_tuning.asset` | `sfx.skill.skill_memory_tuning` | `sfx.skill.skill_memory_tuning.cast`, `sfx.skill.skill_memory_tuning.impact` |
| `skill_minor_heal.asset` | `sfx.skill.skill_minor_heal` | `sfx.skill.skill_minor_heal.cast`, `sfx.skill.skill_minor_heal.impact` |
| `skill_mirror_cut.asset` | `sfx.skill.skill_mirror_cut` | `sfx.skill.skill_mirror_cut.cast`, `sfx.skill.skill_mirror_cut.impact` |
| `skill_phase_tether.asset` | `sfx.skill.skill_phase_tether` | `sfx.skill.skill_phase_tether.cast`, `sfx.skill.skill_phase_tether.impact` |
| `skill_power_strike.asset` | `sfx.skill.skill_power_strike` | `sfx.skill.skill_power_strike.cast`, `sfx.skill.skill_power_strike.impact` |
| `skill_precision_shot.asset` | `sfx.skill.skill_precision_shot` | `sfx.skill.skill_precision_shot.cast`, `sfx.skill.skill_precision_shot.impact` |
| `skill_priest_core.asset` | `sfx.skill.skill_priest_core` | `sfx.skill.skill_priest_core.cast`, `sfx.skill.skill_priest_core.impact` |
| `skill_prism_lance.asset` | `sfx.skill.skill_prism_lance` | `sfx.skill.skill_prism_lance.cast`, `sfx.skill.skill_prism_lance.impact` |
| `skill_raider_core.asset` | `sfx.skill.skill_raider_core` | `sfx.skill.skill_raider_core.cast`, `sfx.skill.skill_raider_core.impact` |
| `skill_raider_utility.asset` | `sfx.skill.skill_raider_utility` | `sfx.skill.skill_raider_utility.cast`, `sfx.skill.skill_raider_utility.impact` |
| `skill_reaver_core.asset` | `sfx.skill.skill_reaver_core` | `sfx.skill.skill_reaver_core.cast`, `sfx.skill.skill_reaver_core.impact` |
| `skill_reaver_utility.asset` | `sfx.skill.skill_reaver_utility` | `sfx.skill.skill_reaver_utility.cast`, `sfx.skill.skill_reaver_utility.impact` |
| `skill_refracting_snare.asset` | `sfx.skill.skill_refracting_snare` | `sfx.skill.skill_refracting_snare.cast`, `sfx.skill.skill_refracting_snare.impact` |
| `skill_riposte_angle.asset` | `sfx.skill.skill_riposte_angle` | `sfx.skill.skill_riposte_angle.cast`, `sfx.skill.skill_riposte_angle.impact` |
| `skill_rusthide_charge.asset` | `sfx.skill.skill_rusthide_charge` | `sfx.skill.skill_rusthide_charge.cast`, `sfx.skill.skill_rusthide_charge.impact` |
| `skill_scout_core.asset` | `sfx.skill.skill_scout_core` | `sfx.skill.skill_scout_core.cast`, `sfx.skill.skill_scout_core.impact` |
| `skill_scout_utility.asset` | `sfx.skill.skill_scout_utility` | `sfx.skill.skill_scout_utility.cast`, `sfx.skill.skill_scout_utility.impact` |
| `skill_shaman_core.asset` | `sfx.skill.skill_shaman_core` | `sfx.skill.skill_shaman_core.cast`, `sfx.skill.skill_shaman_core.impact` |
| `skill_shaman_utility.asset` | `sfx.skill.skill_shaman_utility` | `sfx.skill.skill_shaman_utility.cast`, `sfx.skill.skill_shaman_utility.impact` |
| `skill_shardblade_sever.asset` | `sfx.skill.skill_shardblade_sever` | `sfx.skill.skill_shardblade_sever.cast`, `sfx.skill.skill_shardblade_sever.impact` |
| `skill_signal_flare.asset` | `sfx.skill.skill_signal_flare` | `sfx.skill.skill_signal_flare.cast`, `sfx.skill.skill_signal_flare.impact` |
| `skill_slayer_core.asset` | `sfx.skill.skill_slayer_core` | `sfx.skill.skill_slayer_core.cast`, `sfx.skill.skill_slayer_core.impact` |
| `skill_slayer_utility.asset` | `sfx.skill.skill_slayer_utility` | `sfx.skill.skill_slayer_utility.cast`, `sfx.skill.skill_slayer_utility.impact` |
| `skill_square_wall.asset` | `sfx.skill.skill_square_wall` | `sfx.skill.skill_square_wall.cast`, `sfx.skill.skill_square_wall.impact` |
| `skill_warden_utility.asset` | `sfx.skill.skill_warden_utility` | `sfx.skill.skill_warden_utility.cast`, `sfx.skill.skill_warden_utility.impact` |

## StatusFamilyDefinition

`StatusFamilyDefinition.SfxHookId`는 apply phase까지 포함한 full hook id를 저장한다.

| 대상 | asset field |
| --- | --- |
| status apply | `sfx.status.<status_id>.apply` |

현재 매니페스트 범위의 status는 `barrier`, `bleed`, `burn`, `exposed`, `guarded`, `marked`, `root`, `silence`, `slow`, `sunder`, `unstoppable`, `wound` 12개다. `stun` asset은 존재하지만 현재 active skill payload에서 직접 요구되는 12개 범위 밖이라 이번 hook fill 대상에서 제외한다.

### 현재 채워진 status hook

| asset | `SfxHookId` |
| --- | --- |
| `status_family_barrier.asset` | `sfx.status.barrier.apply` |
| `status_family_bleed.asset` | `sfx.status.bleed.apply` |
| `status_family_burn.asset` | `sfx.status.burn.apply` |
| `status_family_exposed.asset` | `sfx.status.exposed.apply` |
| `status_family_guarded.asset` | `sfx.status.guarded.apply` |
| `status_family_marked.asset` | `sfx.status.marked.apply` |
| `status_family_root.asset` | `sfx.status.root.apply` |
| `status_family_silence.asset` | `sfx.status.silence.apply` |
| `status_family_slow.asset` | `sfx.status.slow.apply` |
| `status_family_sunder.asset` | `sfx.status.sunder.apply` |
| `status_family_unstoppable.asset` | `sfx.status.unstoppable.apply` |
| `status_family_wound.asset` | `sfx.status.wound.apply` |

## Common Combat Cue

`BattleActorAudioSurface`는 common battle cue를 아래 runtime hook id로 매핑한다. 이 매핑은 skill-specific clip이 아직 runtime cue에 연결되지 않은 상태에서 기본 전투 audio surface가 받을 공용 cue id다. 이 id는 생성 프롬프트의 단일 소재를 의미하지 않는다.

| `BattlePresentationCueType` | hook id | socket |
| --- | --- | --- |
| `ActionCommitBasic` | `sfx.combat.action_commit_basic` | `ProjectileOrigin` |
| `ActionCommitSkill` | `sfx.combat.action_commit_skill` | `ProjectileOrigin` |
| `ActionCommitHeal` | `sfx.combat.action_commit_heal` | `Cast` |
| `ImpactDamage` | `sfx.combat.impact_damage` | `Hit` |
| `ImpactHeal` | `sfx.combat.impact_heal` | `Hit` |
| `GuardEnter` | `sfx.combat.guard_enter` | `Center` |
| `GuardExit` | `sfx.combat.guard_exit` | `Center` |
| `RepositionStart` | `sfx.combat.reposition_start` | `FeetRing` |
| `RepositionStop` | `sfx.combat.reposition_stop` | `FeetRing` |
| `DeathStart` | `sfx.combat.death_start` | `Center` |

### Common combat generation variant

common combat cue는 runtime event 단위라 넓다. 실제 생성과 검수는 아래처럼 소재/강도 variant로 쪼갠다.

| runtime hook id | generation variant id 예시 | `variant_key` | 검수 의미 |
| --- | --- | --- | --- |
| `sfx.combat.impact_damage` | `sfx.combat.impact_damage.flesh.light` | `combat.impact.flesh.light` | 살/천에 가까운 가벼운 피격 |
| `sfx.combat.impact_damage` | `sfx.combat.impact_damage.leather.light` | `combat.impact.leather.light` | 가죽 방어구에 닿는 가벼운 피격 |
| `sfx.combat.impact_damage` | `sfx.combat.impact_damage.metal.light` | `combat.impact.metal.light` | 작은 금속 방어구/장식의 짧은 접촉 |
| `sfx.combat.impact_damage` | `sfx.combat.impact_damage.wood_block.light` | `combat.impact.wood_block.light` | 목재 방패/봉쇄 표면에 닿는 block성 접촉 |
| `sfx.combat.guard_enter` | `sfx.combat.guard_enter.wood_shield` | `combat.guard_enter.wood_shield` | 나무 방패를 올리는 준비음 |
| `sfx.combat.guard_enter` | `sfx.combat.guard_enter.leather_bracer` | `combat.guard_enter.leather_bracer` | 가죽 bracer/strap을 당기는 준비음 |
| `sfx.combat.reposition_start` | `sfx.combat.reposition_start.boot_dirt` | `combat.reposition_start.boot_dirt` | 흙/돌 바닥의 짧은 발 긁힘 |
| `sfx.combat.reposition_stop` | `sfx.combat.reposition_stop.boot_dirt` | `combat.reposition_stop.boot_dirt` | 흙/돌 바닥의 짧은 정지/착지 |
| `sfx.combat.action_commit_basic` | `sfx.combat.action_commit_basic.blade_light` | `combat.action_commit.blade_light` | 기본 공격 시작의 가벼운 blade/몸동작 |
| `sfx.combat.action_commit_skill` | `sfx.combat.action_commit_skill.focus_release` | `combat.action_commit_skill.focus_release` | skill별 cast clip 미연결 시 공용 fallback 시전 시작음 |
| `sfx.combat.action_commit_heal` | `sfx.combat.action_commit_heal.soft_invoke` | `combat.action_commit_heal.soft_invoke` | 힐 시전 시작의 부드러운 상승 신호 |
| `sfx.combat.impact_heal` | `sfx.combat.impact_heal.warm_mend` | `combat.impact_heal.warm_mend` | 힐이 대상에게 닿는 완결 신호 |
| `sfx.combat.guard_exit` | `sfx.combat.guard_exit.wood_shield` | `combat.guard_exit.wood_shield` | 방패를 내리는 방어 해제음. guard_enter와 쌍 |
| `sfx.combat.death_start` | `sfx.combat.death_start.humanoid_light` | `combat.death_start.humanoid_light` | 보이스 없는 인간형 쓰러짐 시작 |

초기 생성 단계에서는 한 variant가 모든 소재를 커버한다고 가정하지 않는다. 승인된 variant가 쌓인 뒤에만 runtime resolver나 audio catalog에서 조건별 선택 규칙을 추가한다.

## Skill sound class

skill 생성 주문서는 44개 active skill을 두 sound class로 나눈다. 분류는 `SkillDefinitionAsset.DamageType`에서 결정론으로 파생한다.

| sound class | 파생 규칙 | 생성 방식 |
| --- | --- | --- |
| `physical` | `DamageType == Physical` (26개) | cast/impact 단발 생성. 무기/몸체 foley 중심 |
| `layered_magic` | `DamageType in (Magical, Healing)` (18개) | `work.sfx.skill.<skill_id>.{charge,cast,impact,tail}.raw` 4-layer 생성 후 합성 |

skill별 분류 행과 소재 메모(`foley_hint`)는 `art-pipeline/sfx/manifest/skills.json`이 단일 소스다. 단일 skill의 분류를 바꾸려면 manifest를 고치고, 파생 규칙 자체를 바꾸려면 이 문서를 먼저 고친다.

## 생성 주문서 행

SFX 생성 매니페스트는 최소 아래 필드를 가진다.

| field | 내용 |
| --- | --- |
| `runtime_hook_id` | Unity authored content 또는 `BattleActorAudioSurface`가 받는 안정 runtime hook id |
| `hook_id` | 생성할 full hook id. common combat에서는 generation variant id를 기록한다 |
| `variant_key` | 소재/강도/역할 profile key. 예: `combat.impact.leather.light` |
| `profile_key` | 자동 검증 profile key. 보통 `variant_key`와 같지만 batch별 보정 profile을 분리할 수 있다 |
| `source_asset_id` | skill/status/common cue id |
| `category` | `skill`, `status`, `combat_common` |
| `trigger` | `cast`, `impact`, `apply`, 또는 cue type |
| `estimated_length` | 생성 길이 목표 |
| `prompt` | MOSS-SoundEffect 생성 프롬프트 |
| `negative_prompt` | MOSS-SoundEffect 제외 프롬프트 |
| `expected_audio_profile` | 길이, attack, 대역 비율, 침묵 허용치 등 자동 검증 기준 |
| `review_context` | Asset Studio에 표시할 사용처, variant 의미, 검수 포커스 |

## 보류 및 모호점

- `BattlePresentationCue`는 현재 skill id를 직접 들고 있지 않다. 따라서 `ActionCommitSkill`은 공용 hook까지만 runtime audio surface에 매핑되고, `sfx.skill.<skill_id>.cast/impact`는 생성 주문서와 asset binding 기준으로 먼저 유지한다.
- status apply hook은 authored data에 존재하지만, status application 전용 presentation cue surface는 아직 없다.
- story SFX는 `DialogueLineDefinition`에 line-level field가 없으므로 MediaCueSheet 분리 설계 이후에 별도 계약으로 추가한다.
- UI SFX는 이번 매니페스트 범위 밖이다. 게임 UI 오디오 재생 surface가 아직 없으므로, UI 오디오 runtime 배선이 생길 때 `sfx.ui.*` hook 계약을 함께 추가한다. style bible의 UI 음색 규칙은 그 시점의 생성 기준으로 유지한다.
- common combat generation variant는 아직 runtime resolver에 연결되지 않는다. 현재 단계에서는 생성/검수/Asset Studio metadata 기준이며, 실제 선택 규칙은 weapon/surface tag와 audio catalog가 생긴 뒤 별도 task로 배선한다.
