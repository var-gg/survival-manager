# 드롭 테이블, rarity bracket, source matrix

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `docs/02_design/meta/drop-table-rarity-bracket-and-source-matrix.md`
- 관련문서:
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/02_design/combat/hero-traits.md`
  - `docs/03_architecture/unit-economy-schema.md`

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
| `reward_source_skirmish` | 예 | 예 | gold, `ember_dust`, low-rarity item/manual |
| `reward_source_elite` | 예 | 예 | gold 증가, guaranteed material, better drop |
| `reward_source_boss` | 예 | 예 | `boss_sigil`, `echo_crystal`, named/high-value drop |
| `reward_source_shrine_event` | 예 | 예 | event/shrine 보상 소스 |
| `reward_source_extract` | 예 | 예 | end-run 정산과 bonus chest |
| `reward_source_salvage` | 예 | 아니오 | dismantle / salvage settlement |

## rarity bracket

| bracket id | 의미 | launch floor 예시 |
| --- | --- | --- |
| `common` | 기초 재화와 소재 | gold, `ember_dust`, base material |
| `advanced` | 일반 파밍의 상위층 | rolled item, skill shard |
| `elite` | elite 전용 확정 가치 | better manual, trait token, rare pack |
| `boss` | 보스 전용 고가치 | `boss_sigil`, named item, permanent candidate |

launch floor에서는 ARPG식 5~6단계 rarity ladder를 열지 않는다.

## source matrix 운영 규칙

- skirmish는 `common`과 `advanced`까지만 연다.
- elite는 `advanced`와 `elite`를 연다.
- boss는 `elite`와 `boss`를 연다.
- extract는 base drop table과 bonus bundle을 함께 계산할 수 있다.
- salvage는 material recovery 소스로만 사용한다.
- skirmish / elite / boss drop table은 `RequiredContextTags = SiteId + answer_lane_*`로
  site별 routed entry를 가진다.
- generic source matrix는 유지하되, live subset에서는 `무슨 source인가`와 함께
  `무슨 질문의 답인가`를 같이 기록한다.

## live answer-lane routing

| site id | skirmish routed reward | elite routed reward | boss routed reward |
| --- | --- | --- | --- |
| `site_ashen_gate` | `support_guarded` | `support_anchored` | `support_guarded` |
| `site_cinder_watch` | `support_longshot` | `support_hunter_mark` | `support_piercing` |
| `site_forgotten_warren` | `support_echo` | `support_lingering` | `support_siphon` |
| `site_twisted_den` | `support_purifying` | `support_swift` | `support_purifying` |
| `site_ruined_crypt` | `support_executioner` | `support_piercing` | `support_brutal` |
| `site_grave_sanctum` | `support_siphon` | `support_echo` | `support_hunter_mark` |

## automatic loot 예시

`reward_source_boss`, seed `12345` 기준 예시 bundle:

- `boss_sigil_drop x1`
- `echo_crystal_boss x2`
- `item_prayer_bead x1`

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
