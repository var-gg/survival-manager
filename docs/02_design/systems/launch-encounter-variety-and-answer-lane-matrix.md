# launch encounter variety와 answer lane 매트릭스

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `docs/02_design/systems/launch-encounter-variety-and-answer-lane-matrix.md`
- 관련문서:
  - `docs/02_design/combat/encounter-catalog-and-scaling.md`
  - `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`
  - `docs/02_design/meta/drop-table-rarity-bracket-and-source-matrix.md`
  - `docs/02_design/deck/roster-archetype-launch-scope.md`
  - `docs/02_design/systems/first-playable-slice.md`

## 목적

이 문서는 current pre-art floor에서 `24 encounter / 6 site / 8 authored family`를
어떻게 닫는지 고정한다.
카운트를 늘리는 대신, 각 site가 서로 다른 질문을 던지고 reward가 다음 답으로 이어지게
하는 authored matrix를 소유한다.

## encounter family taxonomy

| family id | 플레이어 질문 | 주 레버 |
| --- | --- | --- |
| `encounter_family_bastion_front` | anchored frontline을 뚫거나 우회해야 한다 | vanguard anchor, undead wall, `defensive_boon` |
| `encounter_family_protect_carry` | 보호받는 backline carry에 닿아야 한다 | marksman/hunter/hexer carry + escort |
| `encounter_family_weakside_dive` | flank collapse를 막거나 punish해야 한다 | raider/slayer/reaver dive |
| `encounter_family_tempo_swarm` | 다수의 중간 위협을 정리해야 한다 | beastkin spread, tempo pressure |
| `encounter_family_sustain_grind` | heal/drain/attrition을 넘겨야 한다 | priest/shaman/undead sustain |
| `encounter_family_mark_execute` | marked burst와 focus fire를 버텨야 한다 | marksman/hunter burst window |
| `encounter_family_control_cleanse` | control 저항/cleanse가 필요하다 | mystic pressure, `control` family |
| `encounter_family_summon_pressure` | 지속 압박과 소환 가치에 대응해야 한다 | hexer/shaman persistent pressure |

## site sequence와 primary answer lane

| site id | skirmish 1 | skirmish 2 | elite | boss | primary answer lane |
| --- | --- | --- | --- | --- | --- |
| `site_ashen_gate` | `bastion_front` | `mark_execute` | `protect_carry` | `bastion_front + defensive_boon` | `answer_lane_guard_anchor` |
| `site_cinder_watch` | `protect_carry` | `weakside_dive` | `mark_execute` | `weakside_dive + tactical_mark` | `answer_lane_reach_anti_carry` |
| `site_forgotten_warren` | `tempo_swarm` | `summon_pressure` | `control_cleanse` | `summon_pressure + attrition-vulnerability` | `answer_lane_anti_swarm_persistence` |
| `site_twisted_den` | `weakside_dive` | `control_cleanse` | `tempo_swarm` | `weakside_dive + control` | `answer_lane_peel_cleanse` |
| `site_ruined_crypt` | `sustain_grind` | `bastion_front` | `mark_execute` | `sustain_grind + defensive_boon` | `answer_lane_anti_sustain_finish` |
| `site_grave_sanctum` | `control_cleanse` | `protect_carry` | `summon_pressure` | `control_cleanse + mark_execute` | `answer_lane_hybrid_boss_prep` |

## reward routing

drop routing은 `RewardSource` 자체를 늘리지 않고
`DropTable.Entries.RequiredContextTags = SiteId + answer_lane_*`로 site bias를 준다.

| site id | skirmish | elite | boss |
| --- | --- | --- | --- |
| `site_ashen_gate` | `support_guarded` | `support_anchored` | `support_guarded` |
| `site_cinder_watch` | `support_longshot` | `support_hunter_mark` | `support_piercing` |
| `site_forgotten_warren` | `support_echo` | `support_lingering` | `support_siphon` |
| `site_twisted_den` | `support_purifying` | `support_swift` | `support_purifying` |
| `site_ruined_crypt` | `support_executioner` | `support_piercing` | `support_brutal` |
| `site_grave_sanctum` | `support_siphon` | `support_echo` | `support_hunter_mark` |

## build lane floor

### class lane

| class | lane A | lane B | lane C |
| --- | --- | --- | --- |
| `vanguard` | `hold_guard` | `anti_dive_peel` | `attrition_hold` |
| `duelist` | `burst_execute` | `sticky_flank` | `sustain_break` |
| `ranger` | `longshot_focus` | `mobile_kite` | `armor_break_focus` |
| `mystic` | `sustain_cleanse` | `control_attrition` | `persistent_pressure` |

### archetype rule

- 각 archetype은 baseline lane 1개와 alt lane 1개를 가진다.
- lane variance는 새 class/race/site 추가가 아니라 support modifier, affix, augment, reward routing으로 만든다.
- 12 core archetype은 enemy squad에도 최소 2회, elite/boss composition에는 최소 1회 등장한다.

## validator contract

- encounter asset은 `encounter_family_*` 1개와 `answer_lane_*` 1개를 exact set으로 가진다.
- boss overlay는 `overlay_ask_*` 1개만 가진다.
- 같은 site의 두 skirmish는 같은 family를 공유하지 않는다.
- 6 site의 4-beat sequence는 서로 모두 달라야 한다.
- 각 family는 2~4회 사용한다.
- boss는 `primary family + overlay ask`의 최대 2 ask만 허용한다.
