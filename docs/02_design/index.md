# 02 design/runtime contract

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-16
- 소스오브트루스: Pindoc Wiki for product/design planning; `docs/02_design/index.md` for remaining repo contract inventory
- 관련문서:
  - `docs/index.md`
  - `docs/00_governance/source-of-truth-matrix.md`
  - `docs/03_architecture/index.md`
  - `pindoc://decision-doc-harness-pindoc-migration`

## 목적

이 폴더는 더 이상 제품/게임기획/창작 방향의 source-of-truth가 아니다.
Pindoc 이관이 끝나지 않았거나 코드·validator·runtime contract와 직접 묶여 아직 repo에 남겨 둔 gameplay/content/UI 계약만 보관한다.

새 product vision, MVP 범위, design pillars, narrative/world/campaign/character lore, visual mockup 결정은 Pindoc Wiki에 작성한다.
repo에 남은 문서를 읽을 때도 "무엇을 만들지"의 창작 기준이 아니라 "현재 구현과 validation이 어떤 contract를 기대하는지"로 제한해서 해석한다.

## 유지 기준

- 코드나 validator가 직접 기대하는 schema, enum, content budget, fatal validation rule은 repo에 남긴다.
- Pindoc 최신본이 있는 creative planning 문서는 repo에서 제거한다.
- 삭제하지 않은 draft narrative seed는 runtime seed 동기화 전 reference로만 취급한다.
- 이 폴더의 code-facing contract도 후속 작업에서 `docs/03_architecture/**` 또는 Pindoc artifact로 세분 이동할 수 있다.

## combat contract 문서

- `combat/authority-matrix.md`: UnitKit/Skill/Affix/Synergy/Augment/Status 권한 경계
- `combat/resource-cadence-loadout.md`: 6-slot topology, energy cadence, action lane 기준
- `combat/targeting-and-ai-vocabulary.md`: selector, fallback, hysteresis, range discipline vocabulary
- `combat/summon-ownership-and-deployables.md`: summon/deployable category, ownership, credit, cap
- `combat/v1-exclusions.md`: Loop A에서 막는 exclusion registry
- `combat/battlefield-and-camera.md`: 좌 vs 우, slanted camera, off-grid 전장 기준
- `combat/realtime-simulation-model.md`: fixed-step 기반 실시간 체감 전투 모델
- `combat/deployment-and-anchors.md`: 3x2 앵커와 4인 배치 규칙
- `combat/team-tactics-and-unit-rules.md`: team posture와 per-unit rule 체계
- `combat/skill-taxonomy-and-damage-model.md`: 6-slot loadout와 수식 기준
- `combat/skill-authoring-schema.md`: effect descriptor, targeting, presentation hook, learn source schema
- `combat/unit-blueprint-schema.md`: unit blueprint budget/rarity/counter governance schema
- `combat/counter-system-topology.md`: 8-lane threat/answer topology와 answer semantics
- `combat/skill-keywords-support-modifiers-and-weapon-restrictions.md`: keyword catalog, flex passive modifier compatibility, weapon/class restriction
- `combat/encounter-catalog-and-scaling.md`: encounter/squad/boss overlay/threat grammar
- `combat/status-effects-cc-and-cleanse-taxonomy.md`: launch floor status, cleanse, DR 규칙
- `combat/status-keyword-and-proc-rulebook.md`: status / keyword / proc ownership rulebook
- `combat/authoritative-replay-and-ledger.md`: authoritative replay와 전투 원장 기준
- `combat/stat-system-and-power-budget.md`: stat v2와 파워 예산 기준
- `combat/combat-readability.md`: 전투 가독성 예산과 표시 기준
- `combat/battle-presentation-contract.md`: overhead UI, nameplate, damage text 기준
- `combat/combat-spatial-contract.md`: footprint, separation, slotting 기준
- `combat/combat-behavior-contract.md`: phase, reevaluation, range discipline 기준
- `combat/mobility-contract.md`: dash/roll/blink 공통 schema 기준
- `combat/combat-mechanics-glossary.md`: crit/dodge/block/energy/summon 용어집
- `combat/hero-traits.md`: recruit trait/quirk와 token 정책
- `combat/battle-playback-contract.md`: 플레이백 정책, seek, 스크러버, 리플레이 계약

## meta contract 문서

- `meta/index.md`: meta contract 문서 인덱스

## systems contract 문서

- `systems/launch-content-scope-and-balance.md`: 출시 기준 콘텐츠 수량과 Loop A loadout 문법 허브
- `systems/first-playable-slice.md`: Loop D first playable subset cap, quota, pool filtering
- `systems/launch-floor-content-matrix.md`: 12 core archetype launch floor matrix
- `systems/launch-encounter-variety-and-answer-lane-matrix.md`: encounter family/site/reward routing matrix
- `systems/squad-blueprint-and-build-ownership.md`: squad blueprint와 빌드 소유권 기준
- `systems/skills-items-and-passive-boards.md`: 스킬, 아이템, 패시브 보드, permanent thesis build 구조
- `systems/content-budgeting-contract.md`: BudgetCard, domain window, derived sanity 계약
- `systems/rarity-ladder-contract.md`: `Common / Rare / Epic` governance rarity 계약
- `systems/v1-forbidden-list.md`: fatal validator forbidden policy

## narrative transition 문서

- `narrative/index.md`: Pindoc-first narrative transition hold 인덱스

## UI contract 문서

- `ui/battle-observer-ui.md`: 전투 관전자 UI 기준
- `ui/town-character-sheet-ui.md`: Town 5-panel character sheet source/view-state contract
- `ui/localization-policy.md`: 플레이어 노출 텍스트와 localization table 정책
