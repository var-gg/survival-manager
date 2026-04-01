# 작업 명세

## 메타데이터

- 작업명: Loop A Contract Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-01
- 관련경로:
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/**`
  - `Assets/_Game/Scripts/Runtime/Combat/**`
  - `Assets/_Game/Scripts/Runtime/Meta/**`
  - `Assets/_Game/Scripts/Runtime/Unity/**`
  - `Assets/_Game/Scripts/Editor/**`
  - `Assets/Tests/EditMode/**`
  - `docs/02_design/combat/**`
  - `docs/02_design/meta/**`
  - `docs/03_architecture/**`
  - `tasks/009_loop_a_contract_closure/**`
- 관련문서:
  - `tasks/006_system_deepening_pass/status.md`
  - `docs/02_design/combat/combat-behavior-contract.md`
  - `docs/02_design/combat/skill-authoring-schema.md`
  - `docs/02_design/meta/affix-authoring-schema.md`
  - `docs/02_design/meta/synergy-and-augment-taxonomy.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## Goal

- 전투 contract를 Loop A 기준의 authority, cadence, loadout, targeting, summon ownership까지 닫아 다음 구현자가 임의 판단 없이 같은 전투 규칙을 확장하게 만든다.

## Authoritative boundary

- 이번 task는 기존 `4-slot + mana/cooldown_recovery + bias-driven tactic` 계약을 `6-slot + energy + data-driven targeting + summon ownership` 계약으로 교체한다.
- source-of-truth는 `docs/02_design/combat/**`, `docs/02_design/meta/**`, `docs/03_architecture/**`, `ContentDefinitionValidator`, `LoadoutCompiler`, `RuntimeCombatContentLookup`, `SM.Combat` runtime으로 같이 이동한다.
- recruitment/meta progression 전체 재설계, encounter scripting 전면 교체, full UI polish는 동시에 닫지 않는다.

## In scope

- authority matrix, loadout/cadence, targeting vocabulary, summon/deployable ownership용 enum/definition/runtime model 추가
- `UnitArchetypeDefinition`, `SkillDefinitionAsset`, `AffixDefinition`, `AugmentDefinition`, `SynergyDefinition`, `StatusFamilyDefinition` 확장
- `BattleUnitLoadout`, `BattleSkillSpec`, `UnitSnapshot`, `BattleState`, `BattleFactory`, `LoadoutCompiler`, `RuntimeCombatContentLookup`, `GameSessionState`, replay/read model/persistence 조정
- energy/resource, action lane arbitration, target lock/hysteresis, summon mirrored credit/inheritance/cap/despawn runtime 구현
- validator, seed/runtime content migration path, EditMode test/harness, debug overlay/gizmo, 관련 문서와 index 갱신
- 새 task packet과 이전 task handoff 동기화

## Out of scope

- 신규 asmdef 추가
- scene/prefab 대규모 재구조화
- `Disarm`, `Fear`, `Charm`의 full runtime rollout
- live balance 재튜닝 전부
- compile green만으로 닫기

## asmdef impact

- 영향 asmdef:
  - `SM.Content`
  - `SM.Combat`
  - `SM.Meta`
  - `SM.Unity`
  - `SM.Editor`
  - `SM.Tests`
- 새 seam type은 가능한 한 `SM.Content` 또는 `SM.Combat`의 작은 파일로 분리하고, Unity/Editor 타입이 `SM.Combat`으로 새지 않게 유지한다.
- `SM.Meta`와 `SM.Persistence.Abstractions`는 mutable flex slot 저장과 compile orchestration까지만 맡고, battle truth는 `SM.Combat`에 남긴다.

## persistence impact

- `SkillInstanceRecord` / `SkillInstanceState`는 flex mutable slot을 `ActionSlotKind` 기준으로 저장하도록 바뀐다.
- archetype-owned basic/signature/mobility는 save에서 직접 편집하지 않는다.
- `SM.Meta`, `SM.Unity`, `SM.Persistence.Abstractions`의 책임은 유지하되, 새 slot/resource contract를 읽도록 변환한다.

## validator / test oracle

- validator:
  - authority matrix 위반
  - signature/flex/mobility activation model 위반
  - 6-slot topology drift
  - summon-chain / slot topology mutation / mirrored credit contract 위반
- targeted EditMode test:
  - content validation workflow
  - loadout compiler / persistence migration
  - cadence baseline
  - target lock / hysteresis / exposed backline
  - summon ownership / direct ally targeting
- runtime path smoke:
  - `pwsh -File tools/unity-bridge.ps1 compile`
  - `pwsh -File tools/unity-bridge.ps1 test-edit`
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
  - `pwsh -File tools/docs-check.ps1 -RepoRoot .`
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## done definition

- six-slot loadout, energy cadence, authority matrix, targeting vocabulary, summon ownership이 코드/문서/validator/test에서 같은 의미로 읽힌다.
- A1~A7 시나리오에 대응하는 validator/test/harness 근거가 생긴다.
- task packet, 관련 문서, index, 이전 task status가 Loop A follow-up 기준으로 동기화된다.
- compile, validator, targeted test, docs/smoke evidence를 `status.md`에 기록한다.

## deferred

- advanced fear/charm/disarm runtime
- projectile-only ephemeral effect 세부 DSL
- recruitment UX polish와 full card redesign
- Unity test runner 자체의 응답 불안정성 해결
