# affix pool v1

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/meta/affix-pool-v1.md`
- 관련문서:
  - `docs/02_design/meta/affix-authoring-schema.md`
  - `docs/02_design/meta/item-and-affix-system.md`
  - `docs/03_architecture/content-seed-assets.md`

## 목적

이 문서는 full affix catalog 후보와 current live subset의 관계를 기록한다.

## capacity vs live subset

- schema capacity: `100~120`
- v1 catalog target: `54`
- current live subset target: `16~24`

## core scalar 24

- `affix.core.attack_power_flat` — flat `phys_power`
- `affix.core.attack_power_percent` — percent `phys_power`
- `affix.core.spell_power_flat` — flat `mag_power`
- `affix.core.spell_power_percent` — percent `mag_power`
- `affix.core.attack_speed` — `attack_speed`
- `affix.core.skill_haste` — `cooldown_recovery`
- `affix.core.crit_chance` — `crit_chance`
- `affix.core.crit_damage` — `crit_multiplier`
- `affix.core.physical_penetration` — `phys_pen`
- `affix.core.elemental_penetration` — `mag_pen`
- `affix.core.max_life_flat` — flat `max_health`
- `affix.core.max_life_percent` — percent `max_health`
- `affix.core.armor_flat` — `armor`
- `affix.core.resistance_flat` — `resist`
- `affix.core.block_chance_stub` — block identity placeholder, live subset 미승격
- `affix.core.block_strength_stub` — block mitigation placeholder, live subset 미승격
- `affix.core.dodge_stub` — dodge placeholder, live subset 미승격
- `affix.core.tenacity` — `tenacity`
- `affix.core.healing_received` — incoming heal multiplier
- `affix.core.nearby_damage_reduction` — nearby DR conditional scalar
- `affix.core.move_speed` — `move_speed`
- `affix.core.starting_resource` — opening mana / energy
- `affix.core.resource_on_hit` — hit resource gain
- `affix.core.cooldown_recovery_on_kill` — kill reset scalar

## conditional tagged 18

- `affix.cond.melee_bonus_damage` — melee tag damage bonus
- `affix.cond.projectile_speed_and_pierce` — projectile speed / pierce chance
- `affix.cond.mobility_after_shield` — dash/roll/blink 후 shield
- `affix.cond.guard_nearby_armor` — guard skill 사용 시 nearby armor
- `affix.cond.crit_applies_mark` — crit on mark
- `affix.cond.mark_bonus_damage` — marked target bonus
- `affix.cond.shield_shatter_bonus` — shield target punish
- `affix.cond.bleed_ticks_faster` — bleed tick rate
- `affix.cond.poison_duration_up` — poison duration
- `affix.cond.burn_spread_on_kill` — burn spread
- `affix.cond.summon_max_life` — summon EHP
- `affix.cond.summon_attack_speed` — summon tempo
- `affix.cond.channeling_damage_reduction` — channel DR
- `affix.cond.first_hit_after_reposition_crit` — reposition follow-up
- `affix.cond.basic_attack_reduces_skill_cd` — attack -> skill cadence
- `affix.cond.long_range_skill_bonus` — long-range cast bonus
- `affix.cond.execute_low_life_bonus` — low-life execute
- `affix.cond.on_block_minor_retaliate` — block pulse

## build-shaping 12

- `affix.shape.first_projectile_split` — 첫 projectile split
- `affix.shape.melee_basic_cleave` — melee cleave
- `affix.shape.dash_damage_trail` — dash trail
- `affix.shape.blink_decoy` — blink decoy
- `affix.shape.guard_tenacity_aura` — guard tenacity aura
- `affix.shape.crit_exposed_instead_of_multi` — crit identity swap
- `affix.shape.first_cast_echo` — round first cast echo
- `affix.shape.summon_limit_plus_one` — summon cap +1
- `affix.shape.non_ultimate_splash` — splash on non-ultimate
- `affix.shape.kill_resets_mobility` — mobility reset on kill
- `affix.shape.barrier_to_damage` — barrier consume burst
- `affix.shape.extra_owned_dot_stack` — owned DoT stack cap push

## 운영 메모

- current committed asset은 subset만 runtime에 연결한다.
- broad public stat 과잉 노출을 막기 위해 `block`, `dodge`, `summon_power`, `status_potency`는 catalog에만 두고 live subset 기본선에서는 좁게 쓴다.
