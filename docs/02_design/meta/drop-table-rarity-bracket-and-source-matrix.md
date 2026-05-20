# 드롭 테이블, rarity bracket, source matrix

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-20
- 소스오브트루스: `docs/02_design/meta/drop-table-rarity-bracket-and-source-matrix.md`
- 관련문서:
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/02_design/meta/item-and-affix-system.md`
  - `docs/02_design/combat/hero-traits.md`
  - `docs/03_architecture/unit-economy-schema.md`
  - `pindoc://decision-equipment-content-v1-assetization-contract`
  - `pindoc://decision-launch-encounter-matrix-support-gate-anchor`

## 목적

이 문서는 전투 후 보상을 `automatic battle drops`와 `operator-choice reward cards` 두 채널로 분리하고, launch floor source matrix를 고정한다.

## dual reward channel

### channel A: automatic battle drops

- 전투 결과 직후 source-tagged loot bundle을 계산한다.
- 결과는 ledger와 stash/run bag에 먼저 기록된다.
- 플레이어가 pick 여부를 고르지 않는다.

### channel B: operator-choice reward cards

- 기존 Reward scene의 3지선다 카드를 유지한다.
- node clear 직후 전략 선택을 제공한다.
- automatic drop을 대체하지 않고 그 위에 추가된다.

## launch floor reward source

| source id | 자동 드롭 | reward card | 주 용도 |
| --- | --- | --- | --- |
| `reward_source_skirmish` | 예 | 예 | gold, low-rarity item/manual |
| `reward_source_elite` | 예 | 예 | gold 증가, better item/manual |
| `reward_source_boss` | 예 | 예 | named/high-value item/manual |
| `reward_source_shrine_event` | 예 | 예 | event/shrine 보상 소스 |
| `reward_source_extract` | 예 | 예 | end-run 정산과 bonus chest |
| `reward_source_salvage` | 예 | 아니오 | dismantle / salvage settlement |

## rarity bracket

| bracket id | 의미 | launch floor 예시 |
| --- | --- | --- |
| `common` | 기초 재화와 낮은 risk 보상 | gold, low-rarity item |
| `advanced` | 일반 파밍의 상위층 | rolled item, skill shard |
| `elite` | elite 전용 확정 가치 | better manual, trait token, rare pack |
| `boss` | 보스 전용 고가치 | named item, permanent candidate |

launch floor에서는 ARPG식 5~6단계 rarity ladder를 열지 않는다.

## source matrix 운영 규칙

- skirmish는 `common`과 `advanced`까지만 연다.
- elite는 `advanced`와 `elite`를 연다.
- boss는 `elite`와 `boss`를 연다.
- extract는 base drop table과 bonus bundle을 함께 계산할 수 있다.
- salvage는 material recovery 소스로만 사용한다.
- V1 live lane에서 skirmish / elite / boss의 automatic item reward는 `RewardType.Item`이어야 한다.
- `RewardType.Item` entry의 `ContentId`는 실제 `ItemBaseDefinition`을 가리켜야 한다.
- skirmish / elite / boss drop table은 `RequiredContextTags = SiteId + answer_lane_*`로
  site별 routed entry를 가진다.
- generic source matrix는 유지하되, live subset에서는 `무슨 source인가`와 함께
  `무슨 질문의 답인가`를 같이 기록한다.

## committed V1 automatic item drops

장비 콘텐츠 V1은 item reward가 skill/manual placeholder inventory item으로 변환되지 않도록 drop table에 실제 item entry를 둔다.
아래 entry는 `EquipmentContentV1CatalogValidator`가 필수 조건으로 검증한다.

| source id | required item ids |
| --- | --- |
| `reward_source_skirmish` | `item_iron_sword`, `item_leather_armor`, `item_lucky_charm` |
| `reward_source_elite` | `item_bone_blade`, `item_guardian_shield`, `item_priest_focus` |
| `reward_source_boss` | `item_prayer_bead`, `item_bulwark_armor`, `item_rift_bow` |

## live answer-lane routing coverage

skirmish / elite / boss drop table은 아래 10개 site lane에 대해 routed entry를 각각 가진다.
route entry id는 `{source_prefix}_{site_id}_{answer_lane_id}` 형태이며, `RequiredContextTags`에 같은 `site_id`와 `answer_lane_id`를 기록한다.

| site id | primary answer lane |
| --- | --- |
| `site_ashen_gate` | `answer_lane_guard_anchor` |
| `site_wolfpine_trail` | `answer_lane_peel_anti_dive` |
| `site_sunken_bastion` | `answer_lane_break_formation` |
| `site_tithe_road` | `answer_lane_anti_mark_cleanse` |
| `site_ruined_crypts` | `answer_lane_anti_sustain_finish` |
| `site_bone_orchard` | `answer_lane_anti_summon_burst` |
| `site_glass_forest` | `answer_lane_cleanse_mobility` |
| `site_starved_menagerie` | `answer_lane_anti_swarm_persistence` |
| `site_heartforge_gate` | `answer_lane_hybrid_break` |
| `site_worldscar_depths` | `answer_lane_adaptive_mastery` |

## automatic loot 예시

`reward_source_boss`, seed `12345` 기준 예시 bundle:

- `gold_boss_cache`
- `item_prayer_bead x1`
- `item_bulwark_armor x1`
- `item_rift_bow x1`

## trait token 정책

launch floor trait token은 아래 세 종류만 연다.

- `trait_reroll_token`
- `trait_lock_token`
- `trait_purge_token`

regular battle drop에서 무작위 추가 trait를 빈번하게 지급하지 않는다.

## deferred

- advanced rarity ladder
- market / trade
- long-form salvage economy
- post-battle inventory capacity puzzle
- material crafting currency as live sink
