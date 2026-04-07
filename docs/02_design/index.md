# 02 디자인

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `docs/02_design/index.md`
- 관련문서:
  - `docs/index.md`
  - `docs/03_architecture/index.md`
  - `docs/04_decisions/adr-0014-grid-deployment-continuous-combat.md`

## 목적

게임 디자인, UX 흐름, 화면/시스템 설계 문서를 모은다.

## design와 harness 경계

- 이 폴더는 `무엇을 만들지`와 플레이어 경험 기준을 정의한다.
- Unity task를 `어떻게 닫을지`에 대한 preflight, phase gate, validator-first, loop budget은 `docs/03_architecture/unity-agent-harness-contract.md`와 그 하위 운영 계약 문서를 따른다.
- 큰 migration task의 실행/핸드오프 기준은 design 문서가 아니라 현재 task 상태 문서와 task template를 우선한다.
- design 문서에 implementation loop 규칙을 섞지 않는다. 그런 규칙은 architecture/harness 문서로 보낸다.

## combat 문서

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
- `combat/skill-catalog-v1.md`: role packet 기반 skill seed catalog
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
- `combat/synergy-system.md`: race/class synergy 설계
- `combat/battle-playback-contract.md`: 플레이백 정책, seek, 스크러버, 리플레이 계약

## meta 문서

- `meta/town-and-expedition-loop.md`: Town -> chapter/site -> Battle -> Reward 루프 기준
- `meta/campaign-chapter-and-expedition-sites.md`: story chapter, site, endless unlock 기준
- `meta/recruitment-contract.md`: 4-slot recruit pack, on-plan/protected, refresh, scout 기준
- `meta/retrain-contract.md`: flex-only retrain, cost curve, previous-result exclusion, pity 기준
- `meta/economy-protection-contract.md`: Gold/Echo split, refit/recovery rail, no-dead-reward, dismiss refund 기준
- `meta/duplicate-handling-contract.md`: duplicate 판정, Echo conversion, recruit pool exclusion 기준
- `meta/drop-table-rarity-bracket-and-source-matrix.md`: automatic drop/source/rarity floor
- `meta/item-and-affix-system.md`: 아이템과 affix 구조, Echo refit, advanced crafting 경계
- `meta/affix-authoring-schema.md`: affix schema와 authority / budget / line-density 기준
- `meta/affix-pool-v1.md`: affix catalog와 live subset
- `meta/equipment-family-and-crafting-depth.md`: weapon family floor와 deep crafting 경계
- `meta/item-passive-augment-budget.md`: 출시 기준 item/passive/augment 예산 허브와 passive/permanent V1 cap
- `meta/passive-board-node-catalog.md`: launch floor passive board node 카탈로그
- `meta/augment-system.md`: augment 문법
- `meta/augment-synergy-operating-model.md`: augment offer와 synergy 운영 기준
- `meta/augment-catalog-v1.md`: augment catalog와 live subset
- `meta/permanent-augment-progression.md`: `unlock many, equip one` permanent progression
- `meta/pvp-boundary.md`: PVP 경계
- `meta/pvp-ruleset-and-arena-loop.md`: async arena ruleset과 season cadence
- `meta/session-realm-and-official-online-boundary.md`: `OfflineLocal` / `OnlineAuthoritative` session realm과 공식 온라인 경계
- `meta/character-race-class-role-archetype-taxonomy.md`: `Character / Race / Class / Role / Archetype` taxonomy와 launch floor identity layer
- `meta/synergy-breakpoints-and-soft-counters.md`: 출시 기준 2/4 breakpoint와 soft counter
- `meta/synergy-family-catalog.md`: 7 family 2/4 payload 카탈로그
- `meta/synergy-and-augment-taxonomy.md`: synergy / augment 역할 경계, 2/4 threshold, rarity

## systems 문서

- `systems/launch-content-scope-and-balance.md`: 출시 기준 콘텐츠 수량과 Loop A loadout 문법 허브
- `systems/first-playable-slice.md`: Loop D first playable subset cap, quota, pool filtering
- `systems/launch-floor-content-matrix.md`: 12 core archetype launch floor matrix
- `systems/launch-encounter-variety-and-answer-lane-matrix.md`: 24 encounter family/site/reward routing matrix
- `systems/squad-blueprint-and-build-ownership.md`: squad blueprint와 빌드 소유권 기준
- `systems/skills-items-and-passive-boards.md`: 스킬, 아이템, 패시브 보드, permanent thesis build 구조
- `systems/content-budgeting-contract.md`: BudgetCard, domain window, derived sanity 계약
- `systems/rarity-ladder-contract.md`: `Common / Rare / Epic` governance rarity 계약
- `systems/v1-forbidden-list.md`: fatal validator forbidden policy

## 기타 디자인 문서

- `deck/roster-archetype-launch-scope.md`: 출시 기준 roster와 archetype package
- `progression/explanation-progression-premise.md`: progression premise 설명 문서
- `ui/battle-observer-ui.md`: 전투 관전자 UI 기준
- `ui/localization-policy.md`: 플레이어 노출 텍스트와 localization table 정책
