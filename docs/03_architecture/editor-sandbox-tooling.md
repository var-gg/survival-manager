# Unity 전투 샌드박스와 에디터 툴링

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/03_architecture/editor-sandbox-tooling.md`
- 관련문서:
  - `docs/03_architecture/content-authoring-model.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`
  - `docs/03_architecture/replay-persistence-and-run-audit.md`
  - `docs/04_decisions/adr-0015-build-compile-audit-pipeline.md`

## 목적

이 문서는 Unity editor 안에서 build, tactic, augment, skill, passive 조합을 빠르게 검증하는 샌드박스 경계를 정의한다.
샌드박스는 전투 truth를 만들지 않고, 입력 조립과 실행, 비교, 로그 확인만 담당한다.

## 기본 원칙

1. authoring은 `ScriptableObject`와 serialized state를 중심으로 둔다.
2. 샌드박스는 `LoadoutCompiler -> BattleLoadoutSnapshot -> BattleSimulator -> ReplayAssembler` 경계를 그대로 사용한다.
3. editor window는 layout/binding만 맡고, 실행 로직은 별도 서비스로 분리한다.
4. `SM.Editor`는 `SM.Unity`와 runtime model을 참조할 수 있지만, runtime은 editor를 참조하지 않는다.

## 현재 구성

### runtime

- `SM.Unity.Sandbox.CombatSandboxConfig`: 샌드박스 preset asset
- `SM.Unity.Sandbox.CombatSandboxRunRequest`: compile 후 실행 입력
- `SM.Unity.Sandbox.CombatSandboxRunResult`: replay hash, metrics, provenance 결과
- `SM.Unity.Sandbox.CombatSandboxSceneController`: simulator/replay 조합과 scene anchor preview 진입점

### editor

- `SM.Editor.Authoring.CombatSandbox.CombatSandboxWindow`: UI Toolkit 기반 상위 창
- `SM.Editor.Authoring.CombatSandbox.CombatSandboxState`: editor transient state
- `SM.Editor.Authoring.CombatSandbox.CombatSandboxExecutionService`: session/build/request 조립기
- `SM.Editor.Authoring.CombatSandbox.CombatSandboxToolbarOverlay`: scene view overlay
- `SM.Editor.Authoring.CombatSandbox.DeploymentAnchorTool`: anchor handle 편집기
- `SM.Editor.Authoring.CombatSandbox.CombatRangeGizmos`: scene view range preview
- `SM.Editor.Authoring.Drawers.TacticRuleDrawer`: tactic preset drawer

## 창 구조

`CombatSandboxWindow`는 네 구역으로 고정한다.

- `Squad Setup`: config asset 선택
- `Overrides`: seed, batch count
- `Execution`: single run, batch run
- `Results`: compile hash, replay hash, metrics, validation message

편집은 `SerializedObject`/`SerializedProperty` 기반으로 묶어 Undo와 dirty 처리 경로를 유지한다.

## scene tooling

- `DeploymentAnchorTool`은 `CombatSandboxSceneController`가 가진 anchor transform만 직접 만진다.
- `CombatRangeGizmos`는 preview ring만 그린다.
- scene tool은 배치와 공간 확인 전용이고, 실제 판정 규칙은 domain simulator가 계속 소유한다.

## 현재 범위

이번 패스의 sandbox는 아래만 연다.

- compile hash와 replay hash preview
- 기본 observer smoke encounter 실행
- config 기반 posture / tactic / enemy slot skeleton
- scene overlay와 anchor handle scaffold

다음 패스에서 열 일:

- passive tree 전용 editor
- reward / drop preview
- replay scrubber
- 대규모 balance sweep UI

## 검증 규칙

- same snapshot + same seed는 같은 replay hash를 만들어야 한다.
- editor window는 play mode 없이 request를 만들 수 있어야 한다.
- sandbox는 `GameSessionState`를 직접 truth로 저장하지 않는다.
- validator warning/error 기준은 `ContentDefinitionValidator`가 맡는다.
