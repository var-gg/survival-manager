# 02 디자인

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/index.md`
- 관련문서:
  - `docs/index.md`
  - `docs/03_architecture/index.md`
  - `docs/04_decisions/adr-0014-grid-deployment-continuous-combat.md`

## 목적

게임 디자인, UX 흐름, 화면/시스템 설계 문서를 모은다.

## combat 문서

- `combat/battlefield-and-camera.md`: 좌 vs 우, slanted camera, off-grid 전장 기준
- `combat/realtime-simulation-model.md`: fixed-step 기반 실시간 체감 전투 모델
- `combat/deployment-and-anchors.md`: 3x2 앵커와 4인 배치 규칙
- `combat/team-tactics-and-unit-rules.md`: team posture와 per-unit rule 체계
- `combat/authoritative-replay-and-ledger.md`: authoritative replay와 전투 원장 기준
- `combat/combat-readability.md`: 전투 가독성 예산과 표시 기준
- `combat/hero-traits.md`: hero trait 방향
- `combat/synergy-system.md`: race/class synergy 설계
- `combat/combat-loop.md`: deprecated pointer
- `combat/formation-and-targeting.md`: deprecated pointer
- `combat/tactics-rules.md`: deprecated pointer
- `combat/explanation-combat-premise.md`: deprecated pointer

## meta 문서

- `meta/town-and-expedition-loop.md`: Town -> Expedition 루프 기준
- `meta/recruitment-and-reroll.md`: 리크루트와 리롤 문법
- `meta/reward-economy.md`: 보상 경제 설계
- `meta/item-and-affix-system.md`: 아이템과 affix 구조
- `meta/augment-system.md`: augment 문법
- `meta/augment-synergy-operating-model.md`: augment offer와 synergy 운영 기준
- `meta/permanent-augment-progression.md`: 영구 강화 진행
- `meta/crafting-and-reroll-economy.md`: 제작과 리롤 비용 구조
- `meta/pvp-boundary.md`: PVP 범위 경계

## systems 문서

- `systems/squad-blueprint-and-build-ownership.md`: squad blueprint와 빌드 소유권 기준
- `systems/skills-items-and-passive-boards.md`: 스킬, 아이템, 패시브 보드 빌드 구조

## 기타 디자인 문서

- `deck/explanation-deck-premise.md`: deprecated pointer, deck 용어 이전 문서
- `progression/explanation-progression-premise.md`: progression premise 설명 문서
- `ui/battle-observer-ui.md`: 전투 관전자 UI 기준
- `ui/mvp-debug-ui.md`: MVP debug UI 기준
