# 영웅풀 확장 로드맵

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/deck/hero-expansion-roadmap.md`
- 관련문서:
  - `docs/02_design/deck/character-lore-registry.md`
  - `docs/02_design/narrative/campaign-story-arc.md`
  - `docs/02_design/meta/story-gating-and-unlock-rules.md`
  - `docs/02_design/narrative/faction-conflict-matrix.md`

## 목적

MVP 12영웅에서 launch/DLC/sequel roster까지의 확장 규칙을 정의한다. 이 문서는 영웅 수, wave, specialist 정책의 source of truth다.

## launch target

| 축 | 목표 |
| --- | ---: |
| 총 영웅 수 | `20` |
| 종족 수 | `4` (Human, Beastkin, Undead, Relicborn) |
| 클래스 수 | `4` (Vanguard, Duelist, Ranger, Mystic) |
| core matrix | `16` (4종족 x 4클래스) |
| specialists | `4` (새 클래스 아님, tag만 추가) |
| 종족별 수 | 각 `5` |
| 클래스별 수 | 각 `5` |

20명은 행렬 완결성 + enemy-to-hero 전환의 재미 + 인디 규모 구현 가능성의 절충선이다.

## 4번째 종족 정책

| 항목 | 결정 |
| --- | --- |
| 종족명 | `Relicborn` |
| 서사 역할 | 침략자가 아니라 봉인의 수문장. midpoint의 진실 보유 집단 |
| 시너지 니치 | barrier, precision, cleanse, control |
| 기존 종족과의 차이 | Human=질서/지속, Beastkin=템포/기동, Undead=소모전/저항, Relicborn=보호막/정화/명중/제어 |
| 브레이크포인트 정합성 | 종족 2/4 체계를 그대로 유지 |

| race | 2-breakpoint fantasy | 4-breakpoint fantasy | roster count |
| --- | --- | --- | ---: |
| `Human` | formation sustain | fortified frontline | 5 |
| `Beastkin` | tempo spike | hunt/crit acceleration | 5 |
| `Undead` | attrition resistance | corpse-long fight dominance | 5 |
| `Relicborn` | ward and clarity | barrier lattice and cleanse chain | 5 |

5번째 종족 금지: sequel 규모가 `24~30+ 영웅 / 15h+`가 되기 전에는 도입하지 않는다. 5번째 종족은 행렬을 다시 뒤흔들고 narrative scope를 급증시킨다.

## 확장 wave 표

| wave_id | hero_id | source | intro_gate | roster_role |
| --- | --- | --- | --- | --- |
| `mvp-core` | `hero_iron_warden` | — | `start_roster` | Human Vanguard |
| `mvp-core` | `hero_crypt_guardian` | — | `start_roster` | Undead Vanguard |
| `mvp-core` | `hero_fang_bulwark` | — | `start_roster` | Beastkin Vanguard |
| `mvp-core` | `hero_oath_slayer` | — | `start_roster` | Human Duelist |
| `mvp-core` | `hero_pack_raider` | — | `start_roster` | Beastkin Duelist |
| `mvp-core` | `hero_grave_reaver` | — | `start_roster` | Undead Duelist |
| `mvp-core` | `hero_longshot_hunter` | — | `start_roster` | Human Ranger |
| `mvp-core` | `hero_trail_scout` | — | `start_roster` | Beastkin Ranger |
| `mvp-core` | `hero_dread_marksman` | — | `start_roster` | Undead Ranger |
| `mvp-core` | `hero_dawn_priest` | — | `start_roster` | Human Mystic |
| `mvp-core` | `hero_grave_hexer` | — | `start_roster` | Undead Mystic |
| `mvp-core` | `hero_storm_shaman` | — | `start_roster` | Beastkin Mystic |
| `launch-specialist` | `hero_rift_stalker` | `encounter_family_weakside_dive` | `chapter_1_site_2` | early specialist |
| `launch-specialist` | `hero_bastion_penitent` | `encounter_family_bastion_front` | `chapter_2_site_1` | human fortress specialist |
| `launch-specialist` | `hero_pale_executor` | `encounter_family_mark_execute` | `chapter_2_site_2` | undead execution specialist |
| `launch-core` | `hero_aegis_sentinel` | `faction_relicborn_guardian` | `chapter_3_site_1` | Relicborn Vanguard |
| `launch-core` | `hero_echo_savant` | `faction_relicborn_scholar` | `chapter_3_site_2` | Relicborn Mystic (lead) |
| `launch-core` | `hero_shardblade` | `faction_relicborn_warrior` | `chapter_4_site_1` | Relicborn Duelist |
| `launch-core` | `hero_prism_seeker` | `faction_relicborn_tracker` | `chapter_4_site_2` | Relicborn Ranger |
| `launch-specialist` | `hero_mirror_cantor` | `encounter_family_control_cleanse` | `chapter_5_site_1` | Relicborn cleanse specialist |

## specialist 정책

- **specialist는 새 클래스가 아니다.** 기존 4클래스 중 하나에 속하면서 특정 encounter family fantasy를 농축한 tag다.
- 런치에서는 4명만 구현해 "잡몹도 우리 덱 영웅이 된다"는 감각을 준다.

| encounter_family | 전환 hero_id | specialist_tag | 런치 여부 |
| --- | --- | --- | --- |
| `encounter_family_weakside_dive` | `hero_rift_stalker` | `weakside_dive` | 런치 |
| `encounter_family_bastion_front` | `hero_bastion_penitent` | `bastion_front` | 런치 |
| `encounter_family_mark_execute` | `hero_pale_executor` | `mark_execute` | 런치 |
| `encounter_family_control_cleanse` | `hero_mirror_cantor` | `control_cleanse` | 런치 |
| `encounter_family_protect_carry` | 추후 specialist | `protect_carry` | DLC 후보 |
| `encounter_family_tempo_swarm` | 추후 specialist | `tempo_swarm` | DLC 후보 |
| `encounter_family_sustain_grind` | 추후 specialist | `sustain_grind` | DLC 후보 |
| `encounter_family_summon_pressure` | 추후 specialist | `summon_pressure` | DLC 후보 |

## DLC / 후속작 규칙

| expansion_phase | allowed_addition | forbidden_addition | rationale |
| --- | --- | --- | --- |
| `dlc_1` | 남은 encounter family 4종을 specialist 4명으로 전환 (총 24명) | 5th race | matrix 안정성 유지 |
| `dlc_2` | `heroes/hero-*.md` 장문 열전과 companion story cards | core matrix 추가 | 시스템보다 authored lore 보강 |
| `sequel_only` | 5th race reconsideration (`24~30+ 영웅 / 15h+` 규모일 때만) | live-service scale hero flood | scope 급증 방지 |

## 작성 지침

- 한 wave는 한 줄로 끝나야 한다.
- hero unlock gate는 `story-gating-and-unlock-rules.md`와 일치해야 한다.
- lore 요약은 registry를 참조하고 여기서는 배치 이유만 적는다.
