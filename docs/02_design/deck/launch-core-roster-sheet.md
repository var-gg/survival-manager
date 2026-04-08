# launch-core roster sheet

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `docs/02_design/deck/launch-core-roster-sheet.md`
- 관련문서:
  - `docs/02_design/systems/launch-floor-content-matrix.md`
  - `docs/02_design/deck/roster-archetype-launch-scope.md`
  - `docs/02_design/meta/passive-board-node-catalog.md`
  - `docs/02_design/meta/permanent-augment-progression.md`
  - `docs/03_architecture/town-character-sheet-contract.md`

## 목적

이 문서는 운영/QA/Town UI/Sandbox가 같은 표를 보도록 `12 core archetype`의 launch-core 운영 truth를 한 장에 고정한다.

## 운영 메모

- 이 문서는 사람용 operator truth다.
- current runtime authored synthetic baseline은 기본적으로 item/passive/augment empty package로 시작하고, build override가 들어갈 때만 실제 drift가 생긴다.
- starter equipment / passive path / permanent thesis는 launch-core freeze를 위한 운영 기준이며, runtime default bake-in은 별도 task로 다룬다.

## launch-core roster truth

| player-facing name | character / archetype id | race / class / role family | 6-slot package | starter equipment | passive board + initial path | posture / tactic | expected synergy | permanent thesis | counter / weakness |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `Iron Warden` | `character_warden` / `warden` | `human` / `vanguard` / `vanguard` | `basic_attack`, `skill_power_strike`, `skill_warden_utility`, `skill_vanguard_passive_1`, `skill_vanguard_support_1`, `mobility_reaction` | `item_guardian_shield`, `item_warden_armor`, `item_warden_trinket` | `board_vanguard`: `passive_vanguard_small_01 -> passive_vanguard_small_02 -> passive_vanguard_notable_01` | `HoldLine` / `team_tactic_hold_line` | `synergy_human`, `synergy_vanguard` | `augment_perm_legacy_oath`: frontline thesis | guard / anti-dive, weakness=`armor shred / dot / ignore-front` |
| `Crypt Guardian` | `character_guardian` / `guardian` | `undead` / `vanguard` / `vanguard` | `basic_attack`, `skill_guardian_core`, `skill_guardian_utility`, `skill_vanguard_passive_2`, `skill_vanguard_support_2`, `mobility_reaction` | `item_guardian_shield`, `item_bone_plate`, `item_guardian_trinket` | `board_vanguard`: `passive_vanguard_small_01 -> passive_vanguard_small_03 -> passive_vanguard_notable_02` | `ProtectCarry` / `team_tactic_protect_carry` | `synergy_undead`, `synergy_vanguard` | `augment_perm_legacy_bone`: attrition bunker thesis | sustain peel, weakness=`armor shred / dot / ignore-front` |
| `Fang Bulwark` | `character_bulwark` / `bulwark` | `beastkin` / `vanguard` / `vanguard` | `basic_attack`, `skill_bulwark_core`, `skill_bulwark_utility`, `skill_vanguard_passive_1`, `skill_vanguard_support_2`, `mobility_reaction` | `item_bulwark_shield`, `item_bulwark_armor`, `item_bulwark_trinket` | `board_vanguard`: `passive_vanguard_small_02 -> passive_vanguard_small_04 -> passive_vanguard_notable_04` | `CollapseWeakSide` / `team_tactic_collapse_weak_side` | `synergy_beastkin`, `synergy_vanguard` | `augment_perm_legacy_fang`: tempo frontline thesis | collapse anchor, weakness=`armor shred / dot / ignore-front` |
| `Oath Slayer` | `character_slayer` / `slayer` | `human` / `duelist` / `striker` | `basic_attack`, `skill_slayer_core`, `skill_slayer_utility`, `skill_duelist_passive_1`, `skill_duelist_support_1`, `mobility_reaction` | `item_slayer_blade`, `item_slayer_armor`, `item_slayer_trinket` | `board_duelist`: `passive_duelist_small_01 -> passive_duelist_small_02 -> passive_duelist_notable_01` | `StandardAdvance` / `team_tactic_standard_advance` | `synergy_human`, `synergy_duelist` | `augment_perm_legacy_blade`: execution thesis | exposed-target finish, weakness=`peel / redirect / hard CC` |
| `Pack Raider` | `character_raider` / `raider` | `beastkin` / `duelist` / `striker` | `basic_attack`, `skill_raider_core`, `skill_raider_utility`, `skill_duelist_passive_2`, `skill_duelist_support_2`, `mobility_reaction` | `item_iron_sword`, `item_raider_armor`, `item_raider_trinket` | `board_duelist`: `passive_duelist_small_01 -> passive_duelist_small_06 -> passive_duelist_notable_04` | `CollapseWeakSide` / `team_tactic_collapse_weak_side` | `synergy_beastkin`, `synergy_duelist` | `augment_perm_legacy_hide`: flank thesis | weak-side collapse, weakness=`peel / redirect / hard CC` |
| `Grave Reaver` | `character_reaver` / `reaver` | `undead` / `duelist` / `striker` | `basic_attack`, `skill_reaver_core`, `skill_reaver_utility`, `skill_duelist_passive_1`, `skill_duelist_support_2`, `mobility_reaction` | `item_reaver_blade`, `item_reaver_armor`, `item_reaver_trinket` | `board_duelist`: `passive_duelist_small_01 -> passive_duelist_small_04 -> passive_duelist_notable_02` | `AllInBackline` / `team_tactic_all_in_backline` | `synergy_undead`, `synergy_duelist` | `augment_perm_legacy_bone`: sustain dive thesis | long fight punish, weakness=`peel / redirect / hard CC` |
| `Longshot Hunter` | `character_hunter` / `hunter` | `human` / `ranger` / `ranger` | `basic_attack`, `skill_precision_shot`, `skill_hunter_utility`, `skill_ranger_passive_1`, `skill_ranger_support_1`, `mobility_reaction` | `item_hunter_bow`, `item_leather_armor`, `item_hunter_trinket` | `board_ranger`: `passive_ranger_small_01 -> passive_ranger_small_02 -> passive_ranger_notable_01` | `StandardAdvance` / `team_tactic_standard_advance` | `synergy_human`, `synergy_ranger` | `augment_perm_legacy_scope`: carry thesis | exposed-target pick, weakness=`dive / backline pressure` |
| `Trail Scout` | `character_scout` / `scout` | `beastkin` / `ranger` / `ranger` | `basic_attack`, `skill_scout_core`, `skill_scout_utility`, `skill_ranger_passive_2`, `skill_ranger_support_2`, `mobility_reaction` | `item_scout_bow`, `item_scout_armor`, `item_lucky_charm` | `board_ranger`: `passive_ranger_small_03 -> passive_ranger_small_06 -> passive_ranger_notable_04` | `CollapseWeakSide` / `team_tactic_collapse_weak_side` | `synergy_beastkin`, `synergy_ranger` | `augment_perm_legacy_signal`: relocation thesis | tempo relocation, weakness=`dive / backline pressure` |
| `Dread Marksman` | `character_marksman` / `marksman` | `undead` / `ranger` / `ranger` | `basic_attack`, `skill_marksman_core`, `skill_marksman_utility`, `skill_ranger_passive_1`, `skill_ranger_support_1`, `mobility_reaction` | `item_marksman_bow`, `item_marksman_armor`, `item_marksman_trinket` | `board_ranger`: `passive_ranger_small_01 -> passive_ranger_small_05 -> passive_ranger_notable_02` | `ProtectCarry` / `team_tactic_protect_carry` | `synergy_undead`, `synergy_ranger` | `augment_perm_legacy_scope`: protected carry thesis | long-lane carry, weakness=`dive / backline pressure` |
| `Dawn Priest` | `character_priest` / `priest` | `human` / `mystic` / `mystic` | `basic_attack`, `skill_priest_core`, `skill_minor_heal`, `skill_mystic_passive_1`, `skill_mystic_support_1`, `mobility_reaction` | `item_priest_focus`, `item_priest_armor`, `item_prayer_bead` | `board_mystic`: `passive_mystic_small_01 -> passive_mystic_small_02 -> passive_mystic_notable_02` | `ProtectCarry` / `team_tactic_protect_carry` | `synergy_human`, `synergy_mystic` | `augment_perm_legacy_grace`: sustain thesis | cleanse/sustain, weakness=`silence / fast dive` |
| `Grave Hexer` | `character_hexer` / `hexer` | `undead` / `mystic` / `mystic` | `basic_attack`, `skill_hexer_core`, `skill_hexer_utility`, `skill_mystic_passive_2`, `skill_mystic_support_2`, `mobility_reaction` | `item_hexer_focus`, `item_hexer_armor`, `item_hexer_trinket` | `board_mystic`: `passive_mystic_small_01 -> passive_mystic_small_04 -> passive_mystic_notable_01` | `AllInBackline` / `team_tactic_all_in_backline` | `synergy_undead`, `synergy_mystic` | `augment_perm_legacy_chalice`: curse/control thesis | attrition control, weakness=`silence / fast dive` |
| `Storm Shaman` | `character_shaman` / `shaman` | `beastkin` / `mystic` / `mystic` | `basic_attack`, `skill_shaman_core`, `skill_shaman_utility`, `skill_mystic_passive_1`, `skill_mystic_support_2`, `mobility_reaction` | `item_shaman_focus`, `item_shaman_armor`, `item_shaman_trinket` | `board_mystic`: `passive_mystic_small_02 -> passive_mystic_small_03 -> passive_mystic_notable_03` | `StandardAdvance` / `team_tactic_standard_advance` | `synergy_beastkin`, `synergy_mystic` | `augment_perm_legacy_signal`: tempo-support thesis | heal/control bridge, weakness=`silence / fast dive` |

## 운영 acceptance

- Town character sheet, sandbox preset naming, QA roster review는 위 표의 `character / archetype id`를 그대로 쓴다.
- exact live skill/loadout truth가 바뀌면 먼저 이 문서와 `docs/02_design/systems/launch-floor-content-matrix.md`를 같이 갱신한다.
- starter equipment / passive path / permanent thesis가 실제 runtime compiled default로 bake-in 되기 전까지는, sandbox drift에서 `equipment / passive-board / augment`는 override 발생 시 drift로 보는 current executable baseline을 유지한다.
