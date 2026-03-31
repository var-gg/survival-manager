# 조우 카탈로그와 스케일링

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/combat/encounter-catalog-and-scaling.md`
- 관련문서:
  - `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`
  - `docs/02_design/combat/status-effects-cc-and-cleanse-taxonomy.md`
  - `docs/03_architecture/encounter-authoring-and-runtime-resolution.md`
  - `docs/02_design/systems/launch-floor-content-matrix.md`

## 목적

이 문서는 launch floor의 `encounter / enemy squad / boss overlay` 문법을 고정한다.
전투 엔진 위에 hand-authored 조우 계층을 세우고, smoke-only 관찰자 경로를 정상 진행 경로에서 분리하는 것이 목적이다.

## 핵심 원칙

- `Faction`은 서사와 소속만 표현한다.
- `Faction`은 `Race`나 `Class`가 아니며 synergy family에 포함되지 않는다.
- canonical synergy count는 기존 3 race + 4 class, 총 7 family만 사용한다.
- chapter/site 진행은 hand-authored로 유지하고 procedural expedition graph는 이번 패스에서 열지 않는다.

## launch floor 조우 문법

- story chapter: `3`
- expedition site: chapter당 `2`, 총 `6`
- encounter template: site당 `2 skirmish + 1 elite + 1 boss`
- site track: `skirmish -> skirmish -> elite -> boss -> extract`
- endless mode: 모든 story chapter clear 후 unlock

## difficulty와 hidden budget

플레이어에게는 skull indicator만 노출하고, 내부 밸런싱은 `ThreatTier`와 `ThreatCost`로 계산한다.

| 구분 | 내부 id | 내부 비용 | UI 표시 |
| --- | --- | --- | --- |
| skirmish | `ThreatTier 1` | `1` | skull `1` |
| elite | `ThreatTier 2` | `2` | skull `2` |
| boss | `ThreatTier 3` | `3` | skull `3` |

- elite와 boss overlay는 encounter budget을 소비한다.
- TFT식 gold cost label은 player-facing UI에 노출하지 않는다.
- difficulty band는 `site_skirmish`, `site_elite`, `site_boss` 세 층으로 잠근다.

## 적군 squad 문법

적군 구성의 canonical authored 단위는 `EnemySquadTemplateDefinition`이다.

- squad는 `FactionId`, `EnemyPosture`, `Members`를 가진다.
- `Members`는 archetype id, anchor, optional trait id, role tag를 가진다.
- 적군은 플레이어 roster와 유닛 풀을 공유할 수 있지만 피아 구분은 `Faction`이 소유한다.
- 같은 archetype이 chapter에 따라 아군 roster와 적군 squad 양쪽에 모두 등장할 수 있다.

## 보스 문법

launch floor boss는 큰 단일 HP 바가 아니라 `BossCaptain + Escorts + BossOverlayRule` 구조를 사용한다.

- `BossCaptain`: 기존 archetype 기반 captain
- `Escorts`: 같은 squad 안의 escort 2~3명
- `BossOverlayRule`: phase trigger, aura, signature utility, reward/drop tag를 추가

overlay는 boss identity를 만들지만 base archetype 문법을 뒤엎지 않는다.

## authored 카탈로그

| chapter | site | encounter ids |
| --- | --- | --- |
| `chapter_ashen_frontier` | `site_ashen_gate` | `site_ashen_gate_skirmish_1`, `site_ashen_gate_skirmish_2`, `site_ashen_gate_elite_1`, `site_ashen_gate_boss_1` |
| `chapter_ashen_frontier` | `site_cinder_watch` | `site_cinder_watch_skirmish_1`, `site_cinder_watch_skirmish_2`, `site_cinder_watch_elite_1`, `site_cinder_watch_boss_1` |
| `chapter_warren_depths` | `site_forgotten_warren` | `site_forgotten_warren_skirmish_1`, `site_forgotten_warren_skirmish_2`, `site_forgotten_warren_elite_1`, `site_forgotten_warren_boss_1` |
| `chapter_warren_depths` | `site_twisted_den` | `site_twisted_den_skirmish_1`, `site_twisted_den_skirmish_2`, `site_twisted_den_elite_1`, `site_twisted_den_boss_1` |
| `chapter_ruined_crypts` | `site_ruined_crypt` | `site_ruined_crypt_skirmish_1`, `site_ruined_crypt_skirmish_2`, `site_ruined_crypt_elite_1`, `site_ruined_crypt_boss_1` |
| `chapter_ruined_crypts` | `site_grave_sanctum` | `site_grave_sanctum_skirmish_1`, `site_grave_sanctum_skirmish_2`, `site_grave_sanctum_elite_1`, `site_grave_sanctum_boss_1` |

## boss overlay 카탈로그

- `boss_overlay_ashen_gate`
- `boss_overlay_cinder_watch`
- `boss_overlay_forgotten_warren`
- `boss_overlay_twisted_den`
- `boss_overlay_ruined_crypt`
- `boss_overlay_grave_sanctum`

모든 boss overlay는 launch floor에서 `ThreatCost 1`을 추가로 소비하고, `RewardDropTags`에 `boss`를 포함한다.

## 비목표

- procedural expedition generation
- enemy faction을 별도 synergy 축으로 확장
- boss 전용 독립 class/race 체계
- shop pool, unit copy pool, TFT-style gold tier 노출
