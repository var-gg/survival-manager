# 02 디자인

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/index.md`
- 관련문서:
  - `docs/index.md`
  - `docs/03_architecture/index.md`
  - `docs/04_decisions/adr-0014-grid-deployment-continuous-combat.md`

## 목적

게임 디자인, UX 흐름, 화면/시스템 설계 문서를 모은다.

## design와 harness 경계

- 이 폴더는 `무엇을 만들지`와 플레이어 경험 기준을 정의한다.
- Unity task를 `어떻게 닫을지`에 대한 preflight, phase gate,
  validator-first, loop budget은
  `docs/03_architecture/unity-agent-harness-contract.md`와 그 하위 운영
  계약 문서를 따른다.
- 큰 migration task의 실행/핸드오프 기준은 design 문서가 아니라 현재
  task 상태 문서와 task template를 우선한다.
- design 문서에 implementation loop 규칙을 섞지 않는다. 그런 규칙은 architecture/harness 문서로 보낸다.

## combat 문서

- `combat/battlefield-and-camera.md`: 좌 vs 우, slanted camera, off-grid 전장 기준
- `combat/realtime-simulation-model.md`: fixed-step 기반 실시간 체감 전투 모델
- `combat/deployment-and-anchors.md`: 3x2 앵커와 4인 배치 규칙
- `combat/team-tactics-and-unit-rules.md`: team posture와 per-unit rule 체계
- `combat/skill-taxonomy-and-damage-model.md`: 4-slot compile contract와 수식 기준
- `combat/authoritative-replay-and-ledger.md`: authoritative replay와 전투 원장 기준
- `combat/stat-system-and-power-budget.md`: stat v2와 파워 예산 기준
- `combat/combat-readability.md`: 전투 가독성 예산과 표시 기준
- `combat/hero-traits.md`: hero trait 방향
- `combat/synergy-system.md`: race/class synergy 설계

## meta 문서

- `meta/town-and-expedition-loop.md`: Town -> Expedition 루프 기준
- `meta/recruitment-and-reroll.md`: 리크루트와 리롤 문법
- `meta/reward-economy.md`: 보상 경제 설계
- `meta/item-and-affix-system.md`: 아이템과 affix 구조
- `meta/item-passive-augment-budget.md`: 출시 기준 item/passive/augment 예산 허브
- `meta/passive-board-node-catalog.md`: launch floor passive board node 카탈로그
- `meta/augment-system.md`: augment 문법
- `meta/augment-synergy-operating-model.md`: augment offer와 synergy 운영 기준
- `meta/permanent-augment-progression.md`: 영구 강화 진행
- `meta/crafting-and-reroll-economy.md`: 제작과 리롤 비용 구조
- `meta/pvp-boundary.md`: PVP 범위 경계
- `meta/synergy-breakpoints-and-soft-counters.md`: 출시 기준 breakpoint와 soft counter
- `meta/synergy-family-catalog.md`: 7 family exact 2/3/4 payload 카탈로그

## systems 문서

- `systems/launch-content-scope-and-balance.md`: 출시 기준 콘텐츠 수량과 밸런스 허브
- `systems/launch-floor-content-matrix.md`: 12 core archetype launch floor matrix
- `systems/squad-blueprint-and-build-ownership.md`: squad blueprint와 빌드 소유권 기준
- `systems/skills-items-and-passive-boards.md`: 스킬, 아이템, 패시브 보드 빌드 구조

## 기타 디자인 문서

- `deck/roster-archetype-launch-scope.md`: 출시 기준 roster와 archetype package
- `progression/explanation-progression-premise.md`: progression premise 설명 문서
- `ui/battle-observer-ui.md`: 전투 관전자 UI 기준
- `ui/mvp-debug-ui.md`: MVP debug UI 기준
- `ui/localization-policy.md`: 플레이어 노출 텍스트와 localization table 정책
