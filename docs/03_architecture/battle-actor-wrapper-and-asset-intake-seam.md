# Battle Actor Wrapper And Asset Intake Seam

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `docs/03_architecture/battle-actor-wrapper-and-asset-intake-seam.md`
- 관련문서:
  - `docs/03_architecture/asset-intake-boundary.md`
  - `docs/02_design/combat/battle-presentation-contract.md`
  - `docs/02_design/combat/battlefield-and-camera.md`
  - `docs/05_setup/asset-workflow.md`

## 목적

이 문서는 battle presentation actor를 vendor asset과 분리된 `_Game` wrapper seam 뒤로 밀어 넣는 구조를 고정한다.
목표는 paid asset 반입 전에도 readable placeholder baseline을 유지하면서,
실제 asset pass에서는 wrapper prefab authoring만 수행하게 만드는 것이다.

## authoritative boundary

- 전투 truth는 계속 `BattleSimulationStep` + `BattlePresentationCue`가 소유한다.
- actor spawn truth는 `BattleActorPresentationCatalog`가 해석한 `_Game` wrapper prefab이다.
- `Assets/ThirdParty/**`는 원본 보존 구역이며 source-of-truth가 될 수 없다.
- localized overhead UI와 selected card 문자열 truth는 계속 `BattleUnitMetadataFormatter` + `ContentTextResolver`가 소유한다.
- seek/replay/reset에서는 bridge/audio/VFX surface가 fire하지 않고 정리만 수행한다.

## runtime structure

- `BattlePresentationController`
  - actor를 runtime `new GameObject`로 만들지 않는다.
  - `BattlePresentationRoot` 하위에 wrapper prefab만 인스턴스화한다.
  - `BattleStageRoot`에는 stage decor와 readability floor만 둔다.
- `BattleActorWrapper`
  - socket registry와 legacy anchor resolve compatibility를 담당한다.
  - `Center`, `Head`, `Hud`, `Hit`, `FeetRing`, `Telegraph`, `Cast`, `ProjectileOrigin`, `CameraFocus`를 `_Game` surface로 노출한다.
- `BattleActorView`
  - semantic cue를 visual state로 바꾸는 driver만 유지한다.
  - primitive mesh/socket authored root 생성 책임은 갖지 않는다.
- `BattleActorVisualAdapter`
  - primitive placeholder 또는 vendor visual adaptation을 캡슐화한다.
  - 현재 기본 구현은 `BattlePrimitiveActorVisualAdapter`다.
- `BattleAnimationEventBridge`, `BattleActorVfxSurface`, `BattleActorAudioSurface`
  - optional/no-op 가능 surface다.
  - cue window 밖 hook과 playback reset 이후 재발화를 허용하지 않는다.
- `BattleSocketFollower`
  - vendor bone을 직접 수정하지 않고 `_Game` proxy socket이 target transform을 따라가게 한다.

## root and socket rules

- wrapper root는 simulation position truth만 받는다.
- `VisualRoot`만 asset offset, rotation, pose 변형을 받는다.
- `Head`, `Hud`, `FeetRing`, `Telegraph`, `CameraFocus`는 actor pitch/roll에 오염되지 않는 `_Game` socket rig에 둔다.
- `ProjectileOrigin`과 `Cast`는 visual rig 하위여도 된다.
- `Cast`와 `ProjectileOrigin` 중 하나만 authored된 경우, 누락된 쪽은 sibling visual socket fallback을 사용할 수 있다. 둘을 분리된 cue surface로 쓰려면 두 socket을 모두 authored한다.
- `FeetRing`과 `Telegraph` fallback은 항상 root ground projection을 사용한다.

## asset intake contract

- vendor prefab은 wrapper 안의 nested child 또는 follower target으로만 연결한다.
- vendor asset은 ring, reticle, range, tether, HUD, camera truth를 소유하지 않는다.
- `_Game` wrapper 밖 코드와 `Assets/ThirdParty/**` 원본 수정 없이 intake가 가능해야 한다.
- 수용 불가 asset:
  - project-wide render pipeline 변경이 필요한 asset
  - gameplay truth, root motion, camera rig, scene singleton takeover를 강제하는 asset

## validation path

- sandbox scene source는 `Assets/_Game/Scenes/BattleAssetIntakeSandbox.unity`다.
- runtime controller는 `BattleAssetIntakeSandboxController`가 맡는다.
- editor validator는 `BattleActorWrapperValidator`가 필수 component, root transform, fallback socket, 금지 component를 검사한다.
- bootstrap command:
  - `SM/Setup/Repair Battle Asset Intake Assets`
- bootstrap은 primitive wrapper prefab, vendor wrapper template prefab, catalog asset, sandbox scene를 생성/복구한다.

## current operational note

- 현재 저장소는 unrelated `SM.Meta` / `SM.Unity` compile blocker가 남아 있어,
  auto bootstrap이 실제 asset 파일을 materialize하지 못할 수 있다.
- compile green을 회복한 뒤 `SM/Setup/Repair Battle Asset Intake Assets`를 다시 실행해
  prefab/catalog/sandbox asset을 생성한다.
