# 내러티브 코드 아키텍처

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/03_architecture/narrative-code-architecture.md`
- 관련문서:
  - `docs/02_design/meta/story-gating-and-unlock-rules.md`
  - `docs/04_decisions/adr-0022-narrative-architecture.md`
  - `docs/03_architecture/dependency-direction.md`
  - `tasks/001_mvp_vertical_slice/status.md`

## 목적

내러티브 runtime core의 실제 코드 배치와 저장 경계를 고정한다.
이 문서는 현재 저장소에 **구현된 narrative runtime core와 Unity playback surface**를 함께 설명하고, 아직 deferred인 authored seed/runtime hardening 항목을 구분한다.

## 현재 구현 범위

- `SM.Core`: narrative enum 6개
- `SM.Content`: narrative `ScriptableObject` definition 7개
- `SM.Meta`: narrative progress record 6개와 service 3개
- `SM.Persistence.Abstractions`: `SaveProfile.Narrative` save host field
- `SM.Unity`: `NarrativeRuntimeBootstrap`가 `Resources.LoadAll<T>(string.Empty)`로 narrative asset을 읽어 `GameSessionState`에 `StoryDirectorService`를 바인딩한다.
- `SM.Unity.Narrative`: `StorySceneFlowBridge`, `StoryPresentationRunner`, portrait resolver, viewstate, presenter, UITK view wrapper가 scene lifecycle과 UI playback을 담당한다.
- `GameSessionState` public surface:
  - `AdvanceNarrative`
  - `TryDequeueNarrativePresentation`
  - `ResetNarrativeRunScopedProgress`
- scene controller integration:
  - `TownScreenController -> NarrativeMoment.TownEntered`
  - `ExpeditionScreenController -> NarrativeMoment.SiteEntered`
  - `BattleScreenController -> NarrativeMoment.BattleResolved`
  - `RewardScreenController -> NarrativeMoment.RewardOpened`

아직 구현되지 않은 것:

- narrative 전용 seed asset과 validator
- `RewardCommitted` 시점의 authored narrative trigger 연결
- same-SHA Unity compile / batch evidence refresh

## 어셈블리 경계

| assembly | narrative 관련 구현 | 허용 의존 |
| --- | --- | --- |
| `SM.Core` | `NarrativeMoment`, `StoryOncePolicy`, `StoryPresentationKind`, `StoryConditionKind`, `StoryEffectKind`, `NarrativeTier` | 없음 |
| `SM.Content` | `StoryEventDefinition`, `StoryConditionDefinition`, `StoryEffectDefinition`, `DialogueSequenceDefinition`, `DialogueLineDefinition`, `ChapterBeatDefinition`, `HeroLoreDefinition` | `SM.Core` |
| `SM.Meta` | `NarrativeProgressRecord`, `StoryMomentContext`, `StoryEventStateRecord`, `StoryPresentationRequest`, `RecruitConversionStateRecord`, `EndlessCycleStateRecord`, `DialogueAssemblyService`, `RecruitConversionService`, `StoryDirectorService` | `SM.Core`, `SM.Content` |
| `SM.Persistence.Abstractions` | `SaveProfile.Narrative` | `SM.Core`, `SM.Content`, `SM.Meta` |
| `SM.Unity` | `NarrativeRuntimeBootstrap`, `GameSessionState` binding/wrapper, `StorySceneFlowBridge`, `StoryPresentationRunner`, presenter/view/runtime adapter | `SM.Core`, `SM.Content`, `SM.Meta`, `SM.Persistence.Abstractions` |

금지 규칙은 그대로 유지한다.

- `SM.Content -> SM.Meta` 역참조 금지
- presenter/UI가 narrative truth 생성 금지
- `static` mutable state 금지

## 구현된 타입 카탈로그

### `SM.Core`

| type | 역할 |
| --- | --- |
| `NarrativeMoment` | `BootLoaded`, `TownEntered`, `ExpeditionSelected`, `SiteEntered`, `BattleStarted`, `BattleResolved`, `RewardCommitted`, `ExtractCommitted`, `CampaignCompleted`, `EndlessCycleStarted`, `RewardOpened` |
| `StoryOncePolicy` | `OncePerProfile`, `OncePerRun`, `Repeatable` |
| `StoryPresentationKind` | `ToastBanner`, `DialogueOverlay`, `DialogueScene`, `StoryCard` |
| `StoryConditionKind` | `ChapterIs`, `SiteIs`, `NodeIs`, `FlagSet`, `FlagNotSet`, `HeroUnlocked`, `HeroNotUnlocked` |
| `StoryEffectKind` | `UnlockHero`, `SetFlag`, `ClearFlag`, `EnqueuePresentation`, `UnlockMode` |
| `NarrativeTier` | `Lead`, `Support`, `Background` |

### `SM.Content`

모든 narrative definition은 `Id` stable id accessor를 가진다.
기존 저장소의 authored definition 패턴을 따라 stable id를 `Id`로 읽고, narrative-specific payload는 private serialized field + read-only property로 노출한다.

| type | 역할 |
| --- | --- |
| `StoryEventDefinition` | moment, priority, once policy, condition/effect list, presentation key |
| `StoryConditionDefinition` | single condition term |
| `StoryEffectDefinition` | single effect term |
| `DialogueSequenceDefinition` | ordered dialogue line list |
| `DialogueLineDefinition` | speaker, text key, emote, auto-advance hint |
| `ChapterBeatDefinition` | chapter/site/node beat target |
| `HeroLoreDefinition` | hero lore card metadata |

### `SM.Meta`

| type | 역할 |
| --- | --- |
| `NarrativeProgressRecord` | save truth. current chapter/site, seen/resolved events, flags, unlocked heroes, pending queue, endless, recruit conversion을 보관 |
| `StoryMomentContext` | moment evaluation input snapshot |
| `StoryEventStateRecord` | `StoryDirectorService` 내부 세션 캐시용 평가 상태 |
| `StoryPresentationRequest` | ordered presentation queue payload |
| `RecruitConversionStateRecord` | encounter family별 누적 conversion count |
| `EndlessCycleStateRecord` | endless cycle index / heat / modifier stacks |
| `DialogueAssemblyService` | definition + progress snapshot으로 `StoryPresentationRequest` 조립 |
| `RecruitConversionService` | encounter family 누적 횟수 기반 hero unlock 변환 |
| `StoryDirectorService` | once policy, condition/effect, queue truth, run reset을 소유 |

### `SM.Unity.Narrative`

| type | 역할 |
| --- | --- |
| `StorySceneFlowBridge` | scene controller가 `NarrativeMoment`를 raise하고, pending presentation dequeue를 request dispatch 직전까지 늦춰 scene 전환 중 손실을 최소화한다 |
| `StoryPresentationRunner` | `StoryPresentationRequest`를 `DialogueSequenceDefinition` / `HeroLoreDefinition` / localized story key 기반 UI model로 변환하고 presenter를 순차 실행한다 |
| `ResourcesStoryPortraitResolver` | `Narrative/Portraits/{characterId}/{emoteId}` 규칙으로 portrait를 resolve한다 |
| `ToastBannerPresenter`, `DialogueOverlayPresenter`, `DialogueScenePresenter`, `StoryCardPresenter` | UITK view wrapper를 순수 C# presenter로 구동한다 |
| `StoryToastBannerView`, `DialogueOverlayView`, `DialogueSceneView`, `StoryCardView` | `Q<T>("name")` 바인딩, 표시 토글, 이벤트 emit만 담당한다 |
| `Story*ViewState`, `StorySpeakerModel`, `StoryDialogueLineModel`, `Dialogue*PlaybackModel` | runtime adapter가 presenter에 전달하는 렌더링 snapshot / playback contract다 |

## 런타임 규칙

### stable id

- story event / dialogue sequence / hero lore lookup은 `Id`를 사용한다.
- `definition.name`이나 asset path를 save key로 사용하지 않는다.

### evaluation

- 같은 `NarrativeMoment` bucket은 `Priority` 내림차순, tie-break는 stable id 오름차순으로 평가한다.
- 상위 priority event가 세운 flag / unlock은 같은 `Advance()` 호출의 하위 event가 바로 볼 수 있다.
- 조건은 전부 AND다.

### queue ownership

- truth owner는 `StoryDirectorService.Progress.PendingPresentations`다.
- `GameSessionState.TryDequeueNarrativePresentation()`는 dequeue 후 다시 `Profile.Narrative`에 sync한다.
- UI/presenter는 queue를 소비만 해야 한다.
- `StorySceneFlowBridge`는 request를 batch 단위로 한 번에 dequeue하지 않고, `StoryPresentationRunner`가 실제 dispatch를 시작하기 직전에 1개씩 dequeue한다.
- 이미 dequeue된 현재 request는 scene teardown 중 유실될 수 있지만, 아직 시작되지 않은 pending request는 save queue에 남아 다음 scene에서 다시 소비된다.

### run/profile scope

- `SeenEventIds`: profile scope
- `ResolvedEventIds`: run scope
- `ResetNarrativeRunScopedProgress()`는 `ResolvedEventIds`, `PendingPresentations`, 세션 cache의 resolved flag만 비운다.

### mode unlock

- 전용 mode 저장소가 아직 없으므로 `UnlockMode`는 `StoryFlags`에 `mode:{modeId}` 형태로 기록한다.

## 세이브 모델

`SaveProfile`은 narrative를 별도 파일로 분리하지 않고 `Narrative` field를 직접 품는다.
repository load 단계와 `GameSessionState.BindProfile()`에서 `NarrativeProgressRecord.Normalize()`를 한 번 더 거쳐 null/legacy payload를 보정한다.

| field | type | notes |
| --- | --- | --- |
| `CurrentChapterId` | `string` | 마지막 narrative chapter snapshot |
| `CurrentSiteId` | `string` | 마지막 narrative site snapshot |
| `SeenEventIds` | `string[]` | profile scope unique sorted set |
| `ResolvedEventIds` | `string[]` | run scope unique sorted set |
| `StoryFlags` | `string[]` | persistent flag set, `mode:{modeId}` 포함 |
| `UnlockedStoryHeroIds` | `string[]` | narrative unlock hero set |
| `RecruitConversions` | `RecruitConversionStateRecord` | `Dictionary<string,int>` with ordinal comparer |
| `PendingPresentations` | `StoryPresentationRequest[]` | queue order 유지 |
| `EndlessCycle` | `EndlessCycleStateRecord` | endless progression state |

## 구현 메모

- `StoryMomentContext.BattleSummary` / `RewardSummary`는 현재 `object?`로 유지한다.
- concrete summary record는 repo에 명확한 canonical type이 생길 때 교체한다.
- `StoryPresentationRunner`는 현재 `PresentationKey -> localized story key / dialogue sequence / hero lore` adapter를 내부에 둔다.
- authored dialogue/human-facing story string asset이 비어 있으면 runner는 localization key 또는 presentation key fallback으로 degrade한다.
