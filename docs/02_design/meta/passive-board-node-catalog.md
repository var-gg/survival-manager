# passive board node 카탈로그

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/meta/passive-board-node-catalog.md`
- 관련문서:
  - `docs/02_design/meta/item-passive-augment-budget.md`
  - `docs/02_design/systems/launch-floor-content-matrix.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## 목적

이 문서는 paid launch floor에서 실제 authoring할 `4 class boards / 72 nodes`를 explicit roster로 잠근다.
예산 문서가 아닌 실제 node 목록 문서이며, node id, kind, rule summary, tags, budget band를 함께 고정한다.

## board shape

### paid launch floor

- board 수: `4`
- board당 구조: `12 small / 5 notable / 1 keystone`
- 총 node 수: `72`

### paid launch safe target

- board 수: `4`
- board당 목표 구조: `14 small / 8 notable / 2 keystone`
- 총 node 수: `96`

## budget band 정의

- `small_floor_linear`: launch floor small node. 단일 stat tap 또는 작은 2축 보정.
- `notable_floor_bundle`: launch floor notable node. `3~6%` 체감 변화 기준의 2축 묶음.
- `keystone_floor_signature`: launch floor keystone. `0~8%` + 역할 정체성 고정.

## `board_vanguard`

- owner class: `vanguard`
- board tags: `frontline`, `sustain`

| node id | kind | rule summary | tags | power budget band |
| --- | --- | --- | --- | --- |
| `passive_vanguard_small_01` | `small` | `max_health +3` | `frontline`, `sustain` | `small_floor_linear` |
| `passive_vanguard_small_02` | `small` | `armor +0.8` | `frontline`, `sustain` | `small_floor_linear` |
| `passive_vanguard_small_03` | `small` | `tenacity +0.15` | `frontline`, `sustain` | `small_floor_linear` |
| `passive_vanguard_small_04` | `small` | `protect_radius +0.2` | `frontline`, `sustain` | `small_floor_linear` |
| `passive_vanguard_small_05` | `small` | `phys_power +0.6` | `frontline`, `sustain` | `small_floor_linear` |
| `passive_vanguard_small_06` | `small` | `target_switch_delay -0.03` | `frontline`, `sustain` | `small_floor_linear` |
| `passive_vanguard_small_07` | `small` | `max_health +3` | `frontline`, `sustain` | `small_floor_linear` |
| `passive_vanguard_small_08` | `small` | `armor +0.8` | `frontline`, `sustain` | `small_floor_linear` |
| `passive_vanguard_small_09` | `small` | `tenacity +0.15` | `frontline`, `sustain` | `small_floor_linear` |
| `passive_vanguard_small_10` | `small` | `protect_radius +0.2` | `frontline`, `sustain` | `small_floor_linear` |
| `passive_vanguard_small_11` | `small` | `phys_power +0.6` | `frontline`, `sustain` | `small_floor_linear` |
| `passive_vanguard_small_12` | `small` | `target_switch_delay -0.03` | `frontline`, `sustain` | `small_floor_linear` |
| `passive_vanguard_notable_01` | `notable` | `max_health +4`, `armor +0.9` | `frontline`, `sustain` | `notable_floor_bundle` |
| `passive_vanguard_notable_02` | `notable` | `tenacity +0.25`, `protect_radius +0.3` | `frontline`, `sustain` | `notable_floor_bundle` |
| `passive_vanguard_notable_03` | `notable` | `phys_power +0.8`, `attack_speed +0.08` | `frontline`, `sustain` | `notable_floor_bundle` |
| `passive_vanguard_notable_04` | `notable` | `armor +0.8`, `protect_radius +0.35` | `frontline`, `sustain` | `notable_floor_bundle` |
| `passive_vanguard_notable_05` | `notable` | `max_health +3`, `target_switch_delay -0.05` | `frontline`, `sustain` | `notable_floor_bundle` |
| `passive_vanguard_keystone_01` | `keystone` | `max_health +6`, `armor +1`, `protect_radius +0.5` | `frontline`, `sustain` | `keystone_floor_signature` |

## `board_duelist`

- owner class: `duelist`
- board tags: `frontline`, `burst`

| node id | kind | rule summary | tags | power budget band |
| --- | --- | --- | --- | --- |
| `passive_duelist_small_01` | `small` | `phys_power +0.8` | `frontline`, `burst` | `small_floor_linear` |
| `passive_duelist_small_02` | `small` | `attack_speed +0.1` | `frontline`, `burst` | `small_floor_linear` |
| `passive_duelist_small_03` | `small` | `crit_chance +0.02` | `frontline`, `burst` | `small_floor_linear` |
| `passive_duelist_small_04` | `small` | `lifesteal +0.02` | `frontline`, `burst` | `small_floor_linear` |
| `passive_duelist_small_05` | `small` | `phys_pen +0.7` | `frontline`, `burst` | `small_floor_linear` |
| `passive_duelist_small_06` | `small` | `move_speed +0.05` | `frontline`, `burst` | `small_floor_linear` |
| `passive_duelist_small_07` | `small` | `phys_power +0.8` | `frontline`, `burst` | `small_floor_linear` |
| `passive_duelist_small_08` | `small` | `attack_speed +0.1` | `frontline`, `burst` | `small_floor_linear` |
| `passive_duelist_small_09` | `small` | `crit_chance +0.02` | `frontline`, `burst` | `small_floor_linear` |
| `passive_duelist_small_10` | `small` | `lifesteal +0.02` | `frontline`, `burst` | `small_floor_linear` |
| `passive_duelist_small_11` | `small` | `phys_pen +0.7` | `frontline`, `burst` | `small_floor_linear` |
| `passive_duelist_small_12` | `small` | `move_speed +0.05` | `frontline`, `burst` | `small_floor_linear` |
| `passive_duelist_notable_01` | `notable` | `phys_power +1`, `attack_speed +0.08` | `frontline`, `burst` | `notable_floor_bundle` |
| `passive_duelist_notable_02` | `notable` | `crit_chance +0.025`, `lifesteal +0.02` | `frontline`, `burst` | `notable_floor_bundle` |
| `passive_duelist_notable_03` | `notable` | `phys_power +0.9`, `phys_pen +0.8` | `frontline`, `burst` | `notable_floor_bundle` |
| `passive_duelist_notable_04` | `notable` | `move_speed +0.06`, `target_switch_delay -0.05` | `frontline`, `burst` | `notable_floor_bundle` |
| `passive_duelist_notable_05` | `notable` | `phys_power +1`, `crit_chance +0.02` | `frontline`, `burst` | `notable_floor_bundle` |
| `passive_duelist_keystone_01` | `keystone` | `phys_power +1.5`, `attack_speed +0.12`, `crit_multiplier +0.15` | `frontline`, `burst` | `keystone_floor_signature` |

## `board_ranger`

- owner class: `ranger`
- board tags: `backline`, `burst`

| node id | kind | rule summary | tags | power budget band |
| --- | --- | --- | --- | --- |
| `passive_ranger_small_01` | `small` | `phys_power +0.8` | `backline`, `burst` | `small_floor_linear` |
| `passive_ranger_small_02` | `small` | `attack_range +0.18` | `backline`, `burst` | `small_floor_linear` |
| `passive_ranger_small_03` | `small` | `attack_speed +0.09` | `backline`, `burst` | `small_floor_linear` |
| `passive_ranger_small_04` | `small` | `projectile_speed +1` | `backline`, `burst` | `small_floor_linear` |
| `passive_ranger_small_05` | `small` | `crit_chance +0.02` | `backline`, `burst` | `small_floor_linear` |
| `passive_ranger_small_06` | `small` | `preferred_distance +0.15` | `backline`, `burst` | `small_floor_linear` |
| `passive_ranger_small_07` | `small` | `phys_power +0.8` | `backline`, `burst` | `small_floor_linear` |
| `passive_ranger_small_08` | `small` | `attack_range +0.18` | `backline`, `burst` | `small_floor_linear` |
| `passive_ranger_small_09` | `small` | `attack_speed +0.09` | `backline`, `burst` | `small_floor_linear` |
| `passive_ranger_small_10` | `small` | `projectile_speed +1` | `backline`, `burst` | `small_floor_linear` |
| `passive_ranger_small_11` | `small` | `crit_chance +0.02` | `backline`, `burst` | `small_floor_linear` |
| `passive_ranger_small_12` | `small` | `preferred_distance +0.15` | `backline`, `burst` | `small_floor_linear` |
| `passive_ranger_notable_01` | `notable` | `attack_range +0.25`, `preferred_distance +0.18` | `backline`, `burst` | `notable_floor_bundle` |
| `passive_ranger_notable_02` | `notable` | `phys_power +0.9`, `crit_chance +0.02` | `backline`, `burst` | `notable_floor_bundle` |
| `passive_ranger_notable_03` | `notable` | `attack_speed +0.08`, `projectile_speed +1.2` | `backline`, `burst` | `notable_floor_bundle` |
| `passive_ranger_notable_04` | `notable` | `move_speed +0.05`, `target_switch_delay -0.05` | `backline`, `burst` | `notable_floor_bundle` |
| `passive_ranger_notable_05` | `notable` | `phys_power +0.9`, `attack_range +0.2` | `backline`, `burst` | `notable_floor_bundle` |
| `passive_ranger_keystone_01` | `keystone` | `phys_power +1.2`, `attack_range +0.3`, `crit_multiplier +0.18` | `backline`, `burst` | `keystone_floor_signature` |

## `board_mystic`

- owner class: `mystic`
- board tags: `backline`, `support`

| node id | kind | rule summary | tags | power budget band |
| --- | --- | --- | --- | --- |
| `passive_mystic_small_01` | `small` | `mag_power +0.8` | `backline`, `support` | `small_floor_linear` |
| `passive_mystic_small_02` | `small` | `heal_power +0.8` | `backline`, `support` | `small_floor_linear` |
| `passive_mystic_small_03` | `small` | `mana_max +3` | `backline`, `support` | `small_floor_linear` |
| `passive_mystic_small_04` | `small` | `cooldown_recovery +0.04` | `backline`, `support` | `small_floor_linear` |
| `passive_mystic_small_05` | `small` | `resist +0.5` | `backline`, `support` | `small_floor_linear` |
| `passive_mystic_small_06` | `small` | `mana_gain_on_hit +0.4` | `backline`, `support` | `small_floor_linear` |
| `passive_mystic_small_07` | `small` | `mag_power +0.8` | `backline`, `support` | `small_floor_linear` |
| `passive_mystic_small_08` | `small` | `heal_power +0.8` | `backline`, `support` | `small_floor_linear` |
| `passive_mystic_small_09` | `small` | `mana_max +3` | `backline`, `support` | `small_floor_linear` |
| `passive_mystic_small_10` | `small` | `cooldown_recovery +0.04` | `backline`, `support` | `small_floor_linear` |
| `passive_mystic_small_11` | `small` | `resist +0.5` | `backline`, `support` | `small_floor_linear` |
| `passive_mystic_small_12` | `small` | `mana_gain_on_hit +0.4` | `backline`, `support` | `small_floor_linear` |
| `passive_mystic_notable_01` | `notable` | `mag_power +0.9`, `cooldown_recovery +0.05` | `backline`, `support` | `notable_floor_bundle` |
| `passive_mystic_notable_02` | `notable` | `heal_power +0.9`, `resist +0.6` | `backline`, `support` | `notable_floor_bundle` |
| `passive_mystic_notable_03` | `notable` | `mana_max +4`, `mana_gain_on_hit +0.4` | `backline`, `support` | `notable_floor_bundle` |
| `passive_mystic_notable_04` | `notable` | `mag_power +0.8`, `resist +0.6` | `backline`, `support` | `notable_floor_bundle` |
| `passive_mystic_notable_05` | `notable` | `heal_power +0.9`, `cooldown_recovery +0.05` | `backline`, `support` | `notable_floor_bundle` |
| `passive_mystic_keystone_01` | `keystone` | `mag_power +1`, `heal_power +1.2`, `cooldown_recovery +0.08` | `backline`, `support` | `keystone_floor_signature` |

## acceptance

- validator 기준 floor board shape는 반드시 `12 / 5 / 1`이다.
- node id, owner board, kind, tags가 이 문서와 다르면 drift로 본다.
- safe target 확장 시에는 이 문서를 수정해 `14 / 8 / 2`로 승격하기 전까지, floor board에 추가 node를 넣지 않는다.
