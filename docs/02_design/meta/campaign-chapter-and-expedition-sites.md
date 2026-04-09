# 캠페인 chapter와 expedition site

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`
- 관련문서:
  - `docs/02_design/meta/town-and-expedition-loop.md`
  - `docs/02_design/combat/encounter-catalog-and-scaling.md`
  - `docs/03_architecture/encounter-authoring-and-runtime-resolution.md`

## 목적

이 문서는 story progression을 `chapter -> site -> encounter track` 구조로 고정한다.
launch floor에서 expedition은 branch graph가 아니라 authored site track을 따른다.

## progression 규칙

- story chapter는 `5`
- chapter당 expedition site는 `2`
- site당 battle node는 `4`
- extract node는 site당 `1`
- endless mode는 story clear 이후에만 열린다.

## chapter / site 카탈로그

| story order | chapter id | site ids | endless unlock |
| --- | --- | --- | --- |
| 1 | `chapter_ashen_gate` | `site_ashen_gate`, `site_wolfpine_trail` | 아니오 |
| 2 | `chapter_sunken_bastion` | `site_sunken_bastion`, `site_tithe_road` | 아니오 |
| 3 | `chapter_ruined_crypts` | `site_ruined_crypts`, `site_bone_orchard` | 아니오 |
| 4 | `chapter_glass_forest` | `site_glass_forest`, `site_starved_menagerie` | 아니오 |
| 5 | `chapter_heartforge_descent` | `site_heartforge_gate`, `site_worldscar_depths` | 예 |

## site track 규칙

각 site는 아래 선형 track을 사용한다.

1. `skirmish`
2. `skirmish`
3. `elite`
4. `boss`
5. `extract`

- Town에서는 해금된 chapter와 site를 선택한다.
- active run 중 Town에서는 chapter/site 선택을 잠그고 같은 authored site track만 resume한다.
- Expedition에서는 현재 site의 선형 progress만 보여 준다.
- node context는 `ChapterId`, `SiteId`, `SiteNodeIndex`, `EncounterId`, `BattleSeed`, `BattleContextHash`로 고정한다.
- extract는 site clear를 확정하기 전 마지막 Reward settlement source로 취급한다.

## site pressure sequence와 primary answer lane

### chapter 1 — chapter_ashen_gate

| site id | skirmish 1 | skirmish 2 | elite | boss | primary answer lane |
| --- | --- | --- | --- | --- | --- |
| `site_ashen_gate` | `bastion_front` | `mark_execute` | `protect_carry` | `bastion_front + defensive_boon` | `answer_lane_guard_anchor` |
| `site_wolfpine_trail` | `weakside_dive` | `tempo_swarm` | `weakside_dive` | `weakside_dive + pack_instinct` | `answer_lane_peel_anti_dive` |

### chapter 2 — chapter_sunken_bastion

| site id | skirmish 1 | skirmish 2 | elite | boss | primary answer lane |
| --- | --- | --- | --- | --- | --- |
| `site_sunken_bastion` | `bastion_front` | `protect_carry` | `bastion_front` | `bastion_front + defensive_boon` | `answer_lane_break_formation` |
| `site_tithe_road` | `mark_execute` | `control_cleanse` | `mark_execute` | `mark_execute + purification` | `answer_lane_anti_mark_cleanse` |

### chapter 3 — chapter_ruined_crypts

| site id | skirmish 1 | skirmish 2 | elite | boss | primary answer lane |
| --- | --- | --- | --- | --- | --- |
| `site_ruined_crypts` | `sustain_grind` | `bastion_front` | `mark_execute` | `sustain_grind + defensive_boon` | `answer_lane_anti_sustain_finish` |
| `site_bone_orchard` | `summon_pressure` | `sustain_grind` | `summon_pressure` | `summon_pressure + awakening` | `answer_lane_anti_summon_burst` |

### chapter 4 — chapter_glass_forest

| site id | skirmish 1 | skirmish 2 | elite | boss | primary answer lane |
| --- | --- | --- | --- | --- | --- |
| `site_glass_forest` | `control_cleanse` | `weakside_dive` | `control_cleanse` | `control_cleanse + crystalline` | `answer_lane_cleanse_mobility` |
| `site_starved_menagerie` | `tempo_swarm` | `sustain_grind` | `tempo_swarm` | `tempo_swarm + mutation` | `answer_lane_anti_swarm_persistence` |

### chapter 5 — chapter_heartforge_descent

| site id | skirmish 1 | skirmish 2 | elite | boss | primary answer lane |
| --- | --- | --- | --- | --- | --- |
| `site_heartforge_gate` | `bastion_front` | `mark_execute` | `bastion_front + mark_execute` | `bastion_front + mark_execute + forge_aegis` | `answer_lane_hybrid_break` |
| `site_worldscar_depths` | `tempo_swarm + control_cleanse` | `weakside_dive + summon_pressure` | `sustain_grind + bastion_front + mark_execute` | `final_cycle` | `answer_lane_adaptive_mastery` |

### 규칙 요약

- 같은 site의 두 skirmish는 같은 질문을 반복하지 않는다.
- 같은 chapter의 두 site는 primary answer lane을 공유하지 않는다.
- chapter 5의 `site_worldscar_depths`는 전 family를 혼합하여 최종 시험으로 기능한다.
- source-of-truth는 `Encounter.SiteId`, `Encounter.RewardDropTags`, `BossOverlay.RewardDropTags`다.

## clear / unlock 규칙

- site clear는 해당 site의 boss 전투와 extract를 모두 지난 뒤 확정한다.
- chapter clear는 chapter에 속한 두 site가 모두 clear된 뒤 확정한다.
- `StoryCleared`는 모든 chapter clear 시점에 true가 된다.
- `EndlessUnlocked`는 `StoryCleared`와 함께 true가 되며, `site_worldscar_depths` clear가 전제 조건이다.

## launch floor UX 원칙

- story 사용자는 chapter/site를 따라 hand-authored progression을 밟는다.
- farm 사용자는 story clear 뒤 endless로 진입한다.
- 무작위 graph 생성은 이번 패스에 넣지 않는다.
