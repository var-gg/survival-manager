# 전투 런타임 아키텍처

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-25
- 소스오브트루스: `docs/03_architecture/combat-runtime-architecture.md`
- 관련문서:
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/03_architecture/combat-content-mapping.md`
  - `docs/02_design/combat/combat-spatial-contract.md`
  - `docs/02_design/combat/combat-behavior-contract.md`
  - `docs/04_decisions/adr-0006-combat-sim-boundary.md`
  - `docs/04_decisions/adr-0014-grid-deployment-continuous-combat.md`

## 목적

이 문서는 fixed-row replay 모델을 step-based live simulation 구조로 바꾼 뒤의 전투 런타임 책임 분리를 정리한다.

## 레이어 기준

- `SM.Combat`
  - `BattleFactory`: definition과 posture, anchor를 받아 초기 `BattleState`를 만든다.
  - `BattleSimulator`: fixed-step loop를 돌리는 domain orchestrator다.
  - `TacticEvaluator`: rule chain을 평가해 action intent를 만든다. LoopA 평가는 ground state + interrupt 모델을 따른다: `TryMobility` → `TrySignature(interrupt)` → `TryCombatFlex(interrupt)` → `BasicAttack(ground state)` → `TryUtilityFlex(fallback)`. 전투형 Flex(`Strike`/`Debuff`)만 ground state를 중단하고, 비전투형 Flex는 기본공격 불가 시 폴백이다.
  - `TargetScoringService`: spatial target score를 계산한다.
  - `MovementResolver`: home position, slotting, range band, reposition, spacing을 계산한다.
  - `EngagementSlotService`: target occupancy와 slot ring을 계산한다.
  - `HitResolutionService`: dodge -> crit -> block -> armor 순서를 소유한다.
  - `CombatActionResolver`: hit, heal, defend event와 수치 변화를 적용한다.
  - `BattleReadModelBuilder`: domain state를 step read model로 변환한다.
  - `IBattleDiagnosticObserver`: headless 계측 run이 실제 target/tactic/intent/action predicate의 입력과 판정 결과를 읽는 internal side channel이다. observer는 생성 시 선택적으로 주입하며 전투 resolution, RNG, canonical telemetry, replay/hash 입력에 참여하지 않는다.
- `SM.Unity`
  - `GameSessionState`: 배치 assignment와 team posture를 session에 유지한다.
  - `RuntimePanelHost`: battle shell의 runtime panel seam을 소유한다. 현재 backend는 `UIDocument`다.
  - `BattleScreenController`: simulator, timeline, camera를 연결하고 scene flow와 HUD를 오케스트레이션한다.
  - `BattleScreenPresenter` + `BattleScreenView`: battle shell의 UITK view state와 input binding을 담당한다.
  - `BattleTimelineController`: 시뮬레이션 스텝 전수 녹화, 임의 seek(뒤로/앞으로), play/pause/speed 제어를 담당한다. 순수 C# 클래스다.
  - `BattlePlaybackPolicy`: QuickBattle(상시 조작) / InGame(종료 후 조작) 모드별 플레이백 권한을 판단한다.
  - `BattleCameraController`: 마우스 드래그 패닝, 휠 줌, edge scrolling, 키보드 패닝, suggested frame 수용을 담당한다. single-target follow는 금지한다.
  - `BattleCameraFramingPolicy`: bootstrap / passive observer frame을 계산하는 순수 C# 정책이다.
  - `BattlePresentationController`: `RenderSnapshot`, `AdvanceStep`, `SetBlend`, `TickTransients`, `SetFocus`를 분리해 actor view와 stage readability layer를 갱신한다.
  - `BattlePresentationCueBuilder`: `BattleEvent`와 read model delta에서 semantic cue를 파생한다.
  - `BattleActorView`: 위치 보간, head/cast anchor, overhead UI, floating text, current/selected telegraph, stance/transient channel을 담당한다.
  - `UiEventSystemConfigurator` + `UiInputSystemModuleConfigurator`: first playable scene의 `EventSystem`을 `InputSystemUIInputModule` 기준으로 정규화하고, `InputSystem_Actions.inputactions`의 `UI` action map을 canonical binding으로 강제한다.

## 런타임 흐름

1. session이 배치 앵커와 team posture를 유지한다.
2. Battle scene이 `UnitDefinition` 목록을 조립한다.
3. `BattleFactory.Create`가 `BattleState`를 만든다.
4. `BattleSimulator.Step()`이 domain truth를 한 step씩 진행한다.
5. `BattleReadModelBuilder`가 `BattleSimulationStep`을 만든다.
6. `BattleTimelineController`가 매 스텝을 녹화하고, seek/play/pause/speed를 관리한다.
7. `BattleCameraController`가 사용자 입력과 suggested frame을 조합해 observer camera를 갱신한다.
8. `RuntimePanelHost` 아래의 battle shell은 compact presenter state로 갱신되고, actor-follow overhead/telegraph는 battle truth의 2차 파생물로 유지된다.
9. battle 완료 시 session과 Reward scene flow에 결과만 넘긴다. 스크러버로 리플레이가 가능하다.

## 경계 규칙

- 전투 승패, 타깃 선택, 이동, 사거리 판정은 `SM.Combat`가 결정한다.
- Unity는 position interpolation과 feedback만 담당한다.
- `MonoBehaviour`는 전투 truth를 저장하거나 재판정하지 않는다.
- NavMesh, physics collision, scene object state는 battle truth source가 아니다.
- snapshot render path는 cue를 재생성하지 않는다.
- normal lane과 debug lane은 같은 read model / event stream을 다르게 표현할 뿐, 다른 truth를 만들지 않는다.
- `BattleDiagnosticEvent`는 `TelemetryEventRecord`의 대체 source가 아니다. 진단 observer가 없을 때는 record를 만들지 않고, 있을 때도 별도 consumer가 읽기만 하며 `BattleState.TelemetryEvents`, `ActivityTelemetry`, replay와 canonical hash에는 기록하지 않는다.
- Battle observer UI 입력은 runtime 수동 `onClick.Invoke()`나 `StandaloneInputModule` fallback에 의존하지 않는다. scene/runtime 모두 `InputSystemUIInputModule` + canonical `UI` action map 바인딩을 사용한다.

## 비수치 enemy rule package와 action-scoped 피해

- `CombatModifierPackage`는 `StatKey` 수치 변경만 소유한다. 타깃 재분배처럼 `StatModifier`로 표현할 수 없는 규칙은 `CombatRuleModifierPackage`로 `BattleUnitLoadout.RulePackages`에 들어간다.
- `EndlessCycleService`의 Heat 적용은 같은 `sourceId`를 쓰는 numeric package와 rule package로 나뉜다. `UnitSnapshot`은 이 provenance를 이용해 rule package에 연결된 Heat 수치 modifier를 제외한 pre-Heat action budget을 재구성한다.
- secondary pressure는 `CombatActionResolver`가 primary target을 확정한 뒤 `SecondaryPressureService`를 한 번 호출하는 action-scoped 경로다. AoE 또는 multi-hit 대상 수만큼 재호출하지 않는다.
- 분배는 살아 있는 non-primary ally를 entity ID 전순서로 정렬한 뒤 fixed-point raw remainder를 앞에서부터 1씩 배정한다. 피해 유형과 armor/resist 및 incoming-damage 배수는 유지하지만 crit, block/dodge roll, drain, direct-hit energy, combo와 offensive kill trigger에는 합류하지 않는다.
- H0에는 rule package와 secondary battle event가 모두 없다. `SecondaryPressureTelemetryAccumulator`는 측정 전용 side channel이며 `BattleResult`, replay, canonical state hash의 입력이 아니다.

## 현재 단순화

- movement는 custom `CombatVector2` 기반이다.
- same-team spacing은 약한 분리 보정만 사용한다.
- normal expedition lane의 적 roster와 encounter는 authored catalog -> `EncounterResolutionService` -> `BattleSetupBuilder` -> `BattleUnitLoadout` 경로를 사용한다.
- `BattleSetupBuilder`는 authored enemy participant spec과 debug smoke participant spec을 pure loadout으로 바꾸는 adapter이며, authored object나 scene truth를 직접 읽지 않는다.
- prototype의 live battle definition 일부는 content asset 대신 runtime hero record에서 조립된다.
- `QuickBattle`/combat sandbox는 debug smoke lane으로 유지되며 normal expedition source-of-truth가 아니다.

## 주요 상수 및 API 가시성 (2026-04-04)

- `HitResolutionService.ArmorScalingK = 10f`: 방어력 damage reduction 공식 상수 `reduction = mitigation / (mitigation + K)`.
- `UnitSnapshot`: 에너지 상수 `EnergyPerBasicAttack(12)`, `EnergyPerKill(15)`, `EnergyPerAssist(8)`, `EnergyPerDirectHit(6)`, `SignatureCastThreshold(100)`.
- `CombatActionResolver.BuildEvent()`: `internal` (SM.Combat asmdef 내부 전용).
- `TargetScoringService.ComputeExposureScore()`: `internal` (TacticEvaluator 전용).
- `BattleState.TelemetryEvents`: `IReadOnlyList<>` 반환, 내부 `_telemetryEvents`로 변경 보호.
