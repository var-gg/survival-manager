# presentation cue / hook coverage 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-06-14
- 소스오브트루스: `docs/03_architecture/presentation-cue-hook-coverage.md`
- 관련문서:
  - `docs/03_architecture/sfx-hook-id-contract.md`
  - `docs/03_architecture/sfx-sound-style-bible.md`
  - `docs/03_architecture/battle-actor-wrapper-and-asset-intake-seam.md`
  - `docs/03_architecture/combat-state-and-event-model.md`

## 목적

실제 그래픽·오디오 asset을 붙이기 전 단계(pre-art)에서도, 더미 presentation만으로 전투와 narrative의 의미가 읽히는지 점검하는 단일 기준이다. 이 문서는 visual/audio/cue hook이 어느 축에 어떤 필드로 존재하는지 inventory하고, "에셋 없음 / placeholder 허용 / 필수 hook 누락 / debug-only"를 가르는 정책과, normal lane이 디버그 오버레이 없이 의미를 전달하는지 보는 수동 witness 기준, seek/reset/replay·배속에서 transient cue가 중복·번지지 않는지 보는 smoke 기준을 잠근다.

실제 wav/png 생성, 믹싱, 최종 연출 품질 평가는 이 문서 범위가 아니다(그건 asset generation pipeline과 `sfx-hook-id-contract.md`가 다룬다). 이 문서는 "asset을 나중에 붙였을 때 빠진 hook과 의미 전달 실패를 구분할 수 있게" 만드는 계약이다.

## cue hook 축 inventory

presentation hook은 6개 축에 흩어져 있고, 모두 `SM.Combat` typed 채널 → `SM.Unity` presentation 소비라는 단방향 seam을 탄다(presentation이 authored asset lookup을 소유한다).

### 1. cue 모델 (런타임 이벤트 단위)

`Assets/_Game/Scripts/Runtime/Unity/BattlePresentationCue.cs`

- `BattlePresentationCueType` 16종: `WindupEnter`, `TargetChanged`, `ActionCommitBasic`, `ActionCommitSkill`, `ActionCommitHeal`, `ImpactDamage`, `ImpactHeal`, `GuardEnter`, `GuardExit`, `RepositionStart`, `RepositionStop`, `DeathStart`, `BattleResolved`, `PlaybackReset`, `SeekSnapshotApplied`, `ActionCanceled`.
- `BattlePresentationCue` record가 운반하는 typed 채널: `ContactAccent`(P2 협격/후방/차단/구출/후열다이브킬), `PresentationFamily`/`PresentationSkin`/`PresentationGesture`(VFX 4-axis), `IsComboEmphasis`(콤보 강조 → 히트스톱/텍스트/스케일).

### 2. skill 축

- `Assets/_Game/Scripts/Runtime/Content/Definitions/SkillDefinitionAsset.cs`: `AnimationHookId`, `IconId`, `VfxHookId`, `SfxHookId`.
- `Assets/_Game/Scripts/Runtime/Combat/Model/SkillDefinition.cs`(`BattleSkillSpec`): `IconId`, `VfxHookId`, `PresentationProfile`(Family/Skin/Gesture/CueSequence).
- `SkillPresentationProfile.cs`: `SkillPresentationFamily`/`Skin`/`Gesture`/`CueSequence` enum + `BattleSkillPresentationProfile`(`IsResolved` 포함). 미지정 시 `EffectivePresentation`이 Default(Melee/Physical/Melee/StrikeImpact)로 떨어진다.

### 3. status 축

- `Assets/_Game/Scripts/Runtime/Content/Definitions/StatusFamilyDefinition.cs`: `VfxCueId`, `SfxHookId`. (skill과 달리 `VfxHookId`가 아니라 `VfxCueId`를 쓴다.) 현재 status apply용 전용 presentation cue surface는 없다(아래 미해결 참조).

### 4. impact / battle-actor 축

- `Assets/_Game/Scripts/Runtime/Unity/BattleActorAudioSurface.cs`: 10개 cue type → socket + 10개 cue type → `sfx.combat.*` runtime hook id 매핑(상세 매핑표는 `sfx-hook-id-contract.md`의 "Common Combat Cue"가 SoT).
- `BattleActorSocketId` 9종: `Center`, `Head`, `Hud`, `Hit`, `FeetRing`, `Telegraph`, `Cast`, `ProjectileOrigin`, `CameraFocus`.
- `Assets/_Game/Scripts/Runtime/Unity/BattleVfxCatalog.cs`: 4-axis 해상(Family/Skin/Semantic/CueType) + cue-type fallback. entry는 prefab/socket/lifetime/offset/scale + Family/Skin/Semantic.

### 5. UI feedback 축

- `Assets/_Game/Scripts/Runtime/Unity/BattleActorView.cs`: floating text(`ResolveImpactLabel`), 히트스톱(`StartHitstop(cue.AnimationIntensity)`), 콤보 스케일(`ResolveImpactScale` ← `IsComboEmphasis`), accent suffix(`ResolveAccentSuffix` → "측면"/"후방!"/"차단" 등). UI 전용 오디오 surface(`sfx.ui.*`)는 아직 없다(`sfx-hook-id-contract.md` 미해결 참조).

### 6. narrative portrait 축

- narrative portrait/emote/bust resource는 본 문서가 아니라 `pindoc://task-narrative-portrait-resource-coverage`(런타임 `ResourcesStoryPortraitResolver` + `NarrativePortraitValidator`)가 소유한다. 본 문서는 battle/presentation 공통 기준만 다룬다(중복 항목은 그 Task로 넘긴다).

## hook 상태 정책 (4-state)

asset/hook을 4가지 상태로 가른다. 핵심은 "에셋 미생성"과 "필수 hook 누락"을 섞지 않는 것이다.

| 상태 | 정의 | 현재 lane 동작 | 판정 |
| --- | --- | --- | --- |
| 에셋 없음 (no asset) | hook id는 있으나 생성된 clip/prefab이 아직 없음 | 오디오: hook id만 보유, 재생 무음. VFX: production이 `null` 반환 → spawn skip | 정상. pre-art 기대 상태 |
| placeholder 허용 (editor fallback) | 에디터에서 Epic Toon FX baseline로 대체 표시 | `TryCreateEditorEpicToonFxFallbackCatalog()`가 vendor prefab을 임시 catalog로 구성 | 에디터 검증에서만 허용. production 빌드엔 미반영 |
| 필수 hook 누락 (required missing) | authored asset이 갖춰야 할 hook id 자체가 비어 있음 | 현재 **경고/에러 로그 없이 silent degrade** | **결함**. asset이 붙어도 영영 안 울림 → 아래 coverage gate가 잡아야 함 |
| debug-only | normal lane 의미 전달엔 불필요, 진단용 정보 | telemetry/debug overlay 채널로만 노출 | normal lane witness에서 제외 |

normal lane이 받아야 하는 의미 전달용 cue와 debug overlay에서만 의미 있는 진단 정보를 섞지 않는다. 예: `ImpactDamage`의 floating damage + accent suffix는 normal lane semantic이고, step별 cinematic score 같은 수치는 debug-only다.

## validator / report coverage 확인 (acceptance #2)

현재 cue-hook coverage가 어떻게 보장되는지 확인한 결과:

- **mapping 계약은 EditMode 테스트가 강제한다.** `BattleActorAudioSurfaceHookTests`(10 cue → hook id + non-audio cue 거부), `BattleVfxCatalogTests`(4-axis spawn + socket fallback), `BattlePresentationCueBuilderTests`(Started→commit, Contacted→reaction, IsComboEmphasis)가 매핑이 깨지면 잡는다.
- **authored hook id 채움은 `sfx-hook-id-contract.md`의 fill 표와 canonical id validator가 보장한다**(skill `SfxHookId` 패턴, status apply hook 12종).
- **그러나 "필수 hook 누락"을 런타임에서 드러내는 coverage report는 없다.** missing hook은 경고 없이 silent degrade한다(`BattleVfxCatalog`는 production에서 `null` 반환, `BattleActorAudioSurface.TryResolveHookId`는 false 반환, 로그 없음). 즉 "어느 skill/status가 hook을 안 채웠나"를 한 장으로 보는 missing-hook coverage report는 미구축이다.
- 후속(observability/validator lane, Codex 도메인): skill/status authored asset 전수에서 빈 `SfxHookId`/`VfxHookId`/`VfxCueId`와 placeholder(editor-fallback only) 사용 현황을 集계하는 coverage report pass. 본 문서는 그 report의 입력 inventory와 4-state 판정 기준을 제공한다.

## normal lane witness 기준 (수동, acceptance #3)

더미 asset(또는 editor fallback)만으로도, 디버그 오버레이를 끄고 한 판을 봤을 때 아래가 화면만으로 읽혀야 한다. 하나라도 debug overlay 없이는 안 읽히면 pre-art 검증 실패다.

- [ ] 누가 누구를 공격했는가 — 기본 공격/스킬 commit이 actor 모션 + commit cue로 구분된다.
- [ ] 맞았는가/빗나갔는가, 피해량 — `ImpactDamage` floating text(+ 측면/후방/차단 accent suffix)가 뜬다.
- [ ] 힐/보호가 들어갔는가 — `ImpactHeal`("구출! +N" 금빛 포함)과 `GuardEnter/Exit`가 구분된다.
- [ ] 위치 사건이 읽히는가 — 넉백/돌진 reposition trail, 후열 다이브킬 사망 배너("후열 격파!")가 보인다.
- [ ] 누가 죽었는가 — `DeathStart`가 모션/배너로 명확하다.
- [ ] 전투 종료 — MVP/하이라이트 종료 요약 줄이 1회 뜬다.

witness는 캡쳐 하네스 비전 판정 또는 사용자 실플레이 어느 쪽이든 좋으나, **debug overlay off** 상태가 전제다.

## seek/reset/replay · 배속 smoke 기준 (acceptance #4)

transient cue(스폰형 VFX/플로팅 텍스트/오디오 트리거)가 재생 제어에서 중복·smear되지 않아야 한다. 판정 cue/필드와 회귀 테스트:

- **seek/reset 경계**: `PlaybackReset`·`SeekSnapshotApplied` cue가 `ClearTransients(reason)`를 호출해 spawned VFX 파괴·애니/오디오/노크 transient 초기화. 스냅샷 seek는 discrete cue를 **재생하지 않는다**.
  - 회귀: `BattlePresentationTransientSurfaceTests`(AdvanceStep으로 스폰 → RenderSnapshot(PlaybackReset/SeekSnapshotApplied)에서 spawn count 불변 = 중복 없음), `BattlePresentationSnapshotTests`(스냅샷 렌더가 discrete cue replay 안 함, LastCueCount=0).
- **배속(x0.5/x2/x4)**: `TickTransients(deltaTime, playbackSpeed, paused)`가 transient 타이머를 `deltaTime * playbackSpeed`로 스케일, contact pin(`BattleContactPinScheduler`)이 playbackSpeed-aware로 recovery-end tick 재계산.
  - 회귀: `BattlePresentationTimelineTests`(frame-rate-independent timeline, seed-driven stress).

smoke 합격 기준: 같은 시드 한 판을 (a) 정상 재생, (b) 중간 seek 후 재개, (c) x2/x4 재생으로 돌렸을 때 transient cue가 한 번씩만 울리고(중복 스폰 0), 배속에서도 잔상/smear 없이 시작·정지 타이밍이 contact pin을 따른다.

## 보류 및 미해결

- status apply 전용 presentation cue surface가 없다(`StatusFamilyDefinition.VfxCueId`는 authored되지만 런타임 cue로 흐르는 전용 경로 미배선). status 시각화 패스는 별도 task.
- UI 오디오(`sfx.ui.*`)와 story line-level SFX(`DialogueLineDefinition`)는 아직 runtime surface가 없어 본 inventory 범위 밖이다(`sfx-hook-id-contract.md` 미해결과 동일).
- missing-hook coverage report(위 acceptance #2)는 미구축 — observability/validator lane 후속.
- narrative portrait coverage는 `pindoc://task-narrative-portrait-resource-coverage` 소유.
