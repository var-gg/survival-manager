# Unity 전투 샌드박스와 에디터 툴링

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-03
- 소스오브트루스: `docs/03_architecture/editor-sandbox-tooling.md`
- 관련문서:
  - `docs/03_architecture/content-authoring-model.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`
  - `docs/03_architecture/replay-persistence-and-run-audit.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`
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
- `SM.Editor.Authoring.CombatSandbox.CombatRangeGizmos`: scene view footprint/range/slot preview
- `SM.Editor.Authoring.Drawers.TacticRuleDrawer`: tactic preset drawer

## 창 구조

`CombatSandboxWindow`는 네 구역으로 고정한다.

- `Squad Setup`: config asset 선택
- `Overrides`: seed, batch count
- `Execution`: single run, batch run
- `Results`: compile hash, replay hash, metrics, validation message

편집은 `SerializedObject`/`SerializedProperty` 기반으로 묶어 Undo와 dirty 처리 경로를 유지한다.

## 3축 개발자 인터페이스

전투 밸런싱/디버그는 세 개의 축으로 구성한다.

### 축 1: Asset Inspector

`SM.Editor.Authoring.Inspectors` 네임스페이스.

| 인스펙터 | 대상 | 표시 내용 |
|----------|------|----------|
| `UnitArchetypeDefinitionEditor` | 아키타입 | 비기본 스탯, 스킬 슬롯 분포, 프로필 요약, compiled effective values (ResolveBehavior) |
| `SkillDefinitionAssetEditor` | 스킬 | 케이던스 타임라인, 스케일링 계수, 타겟팅 지오메트리 |
| `FootprintProfileDefinitionEditor` | 풋프린트 | 공간 반경, reach/separation 경고 |
| `MobilityProfileDefinitionEditor` | 모빌리티 | 이동 프로필, trigger range, total commit |

인스펙터는 raw authoring 값과 compiled effective 값을 같은 화면에 보여준다. compiled 값은 `CombatProfileDefaults.ResolveBehavior` 기반.

### 축 2: Sandbox Window

`SM.Editor.Authoring.CombatSandbox.CombatSandboxWindow`.

- Config → BuildRequest → Execute 파이프라인
- scene anchor handle → `ExportSceneLayout()` → `BattlefieldLayout` authoritative layout 연결
- Results: compile hash, replay hash, metrics, governance, readability, explanation, provenance, layout source
- Provenance: subject별 artifact 카운트 + drill-down

### 축 3: Runtime Battle HUD

`SM.Unity.BattleScreenController` OnGUI overlay.

- F3: debug overlay 토글
- F4: single-step (paused 상태에서)
- F5: same-seed rerun (state 재생성)
- Tab: selected unit 순환

표시 내용:
- 상단: Step/MaxSteps, Time, Speed, 양팀 생존 수, 키 안내
- 유닛별 1줄: side, name, HP%, target, action state, cooldown, retarget lock, selector, guard radius
- selected unit 패널: 좌표, energy, barrier, selector/fallback, guard/cluster radius, class/race/anchor
- decisive timeline: 킬/스킬 이벤트 시간순
- 타깃 연결선: `Debug.DrawLine` (cyan=ally, orange=enemy)

## scene tooling

- `DeploymentAnchorTool`은 `CombatSandboxSceneController`가 가진 anchor transform만 직접 만진다.
- `CombatRangeGizmos`는 12개 gizmo를 모두 그린다: head anchor, navigation, separation, combat reach, preferred range band, engagement slot ring, frontline guard radius, AoE cluster radius, entity kind label.
- scene handle에서 움직인 좌표는 `ExportSceneLayout()`을 통해 실행 request의 authoritative layout이 된다.
- scene tool은 배치와 공간 확인 전용이고, 실제 판정 규칙은 domain simulator가 계속 소유한다.

## knob 관리 규칙

- sandbox config의 모든 exposed field는 실행 경로에서 소비돼야 한다.
- 연결되지 않은 field는 `[HideInInspector]`로 숨기고 TODO를 유지한다.
- 실행 결과에 layout source ("Scene" / "Default")를 표시한다.

## canonical debug value 규칙

- debug overlay, read model, gizmo에 표시되는 수치는 canonical combat rules source에서 읽는다.
- 하드코딩 상수를 디버그 표시에 사용하지 않는다.
- `BehaviorProfile.FrontlineGuardRadius`처럼 규칙값은 content definition → record → UnitSnapshot → read model 경로로 전달한다.

## baseline 운영

- `SM/Generate/Write Baseline Asset Docs` 메뉴로 `Logs/baseline-docs/baseline-asset-summary.md` 생성.
- archetype, skill, footprint, behavior, mobility 프로필을 마크다운 테이블로 정리.
- release 전 baseline 스냅샷과 delta 비교는 생성된 마크다운을 git diff로 확인.

## 다음 패스에서 열 일

- passive tree 전용 editor
- reward / drop preview
- replay scrubber
- 대규모 balance sweep UI
- sandbox visual run / batch compare

## 검증 규칙

- same snapshot + same seed는 같은 replay hash를 만들어야 한다.
- editor window는 play mode 없이 request를 만들 수 있어야 한다.
- sandbox는 `GameSessionState`를 직접 truth로 저장하지 않는다.
- validator warning/error 기준은 `ContentDefinitionValidator`가 맡는다.
