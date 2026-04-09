# 내러티브 코드 아키텍처

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/03_architecture/narrative-code-architecture.md`
- 관련문서:
  - `docs/02_design/narrative/dialogue-event-schema.md`
  - `docs/02_design/meta/story-gating-and-unlock-rules.md`
  - `docs/04_decisions/adr-0022-narrative-architecture.md`
  - `docs/03_architecture/dependency-direction.md`

## 목적

스토리/대사/이벤트 정의, 런타임 상태, 세이브 모델, scene flow 삽입 지점을 기술 관점에서 고정한다. 이 문서는 narrative code placement의 source of truth다.

## 설계 원칙

| 원칙 | 적용 방식 |
|---|---|
| 기존 asmdef 의존방향 준수 | story definitions는 `SM.Content`, runtime state는 `SM.Meta`, save host는 `SM.Persistence.Abstractions`, playback은 `SM.Unity` |
| presentation이 truth를 만들지 않음 | truth commit 후 `StoryDirectorService`가 관찰, presenter는 queue 재생만 |
| mutable static global state 금지 | 모든 진행상태는 record와 save root field로 관리 |
| 이유 없는 interface 금지 | v1에서는 concrete service. 두 번째 구현체 시 interface 추출 |
| 풀 컷씬 엔진 금지 | `story-card`, `dialogue-overlay`, `toast-banner` 3단만 |

## 어셈블리 경계

| assembly | narrative 관련 타입 종류 | 의존 허용 |
|---|---|---|
| `SM.Core` | enum (NarrativeMoment, StoryOncePolicy, StoryPresentationKind, StoryConditionKind, StoryEffectKind, NarrativeTier) | 없음 |
| `SM.Content` | definition (StoryEventDefinition, ChapterBeatDefinition, DialogueSequenceDefinition, HeroLoreDefinition, etc.) | `SM.Core` |
| `SM.Meta` | record + service (NarrativeProgressRecord, StoryDirectorService, DialogueAssemblyService, etc.) | `SM.Core`, `SM.Content` |
| `SM.Persistence.Abstractions` | save host field (NarrativeProgressRecord를 기존 root에 추가) | `SM.Core`, `SM.Content`, `SM.Meta` |
| `SM.Unity` | MonoBehaviour/controller/presenter (StorySceneFlowBridge, StoryPresentationRunner, etc.) | Core, Content, Meta, Persistence.Abstractions |

`SM.Content -> SM.Meta` 역참조는 금지한다.

## 타입 카탈로그

### SM.Core enums

| type_name | kind | 역할 |
|---|---|---|
| `NarrativeMoment` | enum | 스토리 트리거 시점: `BootLoaded`, `TownEntered`, `SiteEntered`, `BattleStarted`, `BattleResolved`, `RewardCommitted`, `ExtractCommitted`, `CampaignCompleted`, `EndlessCycleStarted`, `ExpeditionSelected` |
| `StoryOncePolicy` | enum | 이벤트 재생 횟수: `OncePerProfile`, `OncePerRun`, `Repeatable` |
| `StoryPresentationKind` | enum | 연출 타입: `ToastBanner`, `DialogueOverlay`, `StoryCard` |
| `StoryConditionKind` | enum | 조건 종류: `ChapterIs`, `SiteIs`, `NodeIs`, `FlagSet`, `FlagNotSet`, `HeroUnlocked`, `HeroNotUnlocked` |
| `StoryEffectKind` | enum | 효과 종류: `UnlockHero`, `SetFlag`, `ClearFlag`, `EnqueuePresentation` |
| `NarrativeTier` | enum | hero lore tier: `Lead`, `Support`, `Background` |

### SM.Content definitions

| type_name | kind | 역할 |
|---|---|---|
| `NarrativeWorldDefinition` | definition | 세계관/세력/연대기 데이터 |
| `CampaignStoryArcDefinition` | definition | chapter-level arc 데이터 |
| `ChapterBeatDefinition` | definition | site/node beat 데이터 (감정값 포함) |
| `StoryEventDefinition` | definition | 트리거 가능한 스토리 이벤트 (moment, priority, oncePolicy, conditions, effects, presentationKey) |
| `StoryConditionDefinition` | definition | 조건 식 단위 (Kind + OperandA + OperandB) |
| `StoryEffectDefinition` | definition | 효과 식 단위 (Kind + Payload) |
| `DialogueSequenceDefinition` | definition | 대사 시퀀스 (line list, portrait/sfx refs) |
| `DialogueLineDefinition` | definition | 한 줄 대사 (speaker, text key, emote, auto-advance hint) |
| `HeroLoreDefinition` | definition | 영웅 canon shortform (registry 1:1 대응) |
| `RecruitConversionDefinition` | definition | encounter family -> hero unlock 규칙 |
| `FactionConflictDefinition` | definition | faction pair 충돌 정의 |

### SM.Meta records and services

| type_name | kind | 역할 |
|---|---|---|
| `StoryMomentContext` | record | 현재 moment 평가용 입력 묶음 |
| `NarrativeProgressRecord` | record | persistent story state (flags, seen/resolved events, unlocked heroes, endless) |
| `StoryEventStateRecord` | record | 개별 이벤트 상태 (seen count, last played tick, resolved bool) |
| `StoryPresentationRequest` | record | presentation queue payload |
| `RecruitConversionStateRecord` | record | 모집 전환 상태 |
| `EndlessCycleStateRecord` | record | endless mode state (cycle index, heat, modifiers) |
| `StoryDirectorService` | service | 스토리 이벤트 트리거 소유자: event ledger 평가, narrative state 갱신, queue 생성 |
| `DialogueAssemblyService` | service | definitions + runtime facts -> `StoryPresentationRequest` 조립 |
| `RecruitConversionService` | service | enemy-to-hero 전환 처리 |

### SM.Unity controllers and presenters

| type_name | kind | 역할 |
|---|---|---|
| `StorySceneFlowBridge` | MonoBehaviour/controller | scene lifecycle와 `StoryDirectorService` 연결. 한 씬당 하나 |
| `StoryPresentationRunner` | MonoBehaviour/controller | queue 순차 재생, skip/continue 처리 |
| `StoryOverlayPresenter` | presenter | `story-card`, `toast-banner` 렌더. state mutation 금지 |
| `DialoguePanelPresenter` | presenter | portrait/text overlay 렌더. state mutation 금지 |

## scene flow 통합표

| moment | raised_by | precondition | side_effect | ui_surface |
|---|---|---|---|---|
| `BootLoaded` | boot sequence | profile/load 완료 | prologue gate, pending queue 평가 | `story-card` 0~1회 |
| `TownEntered` | town flow | town state hydrate 완료 | extract 후속 반응, bark, codex reveal | `toast` 또는 짧은 `dialogue-overlay` |
| `ExpeditionSelected` | meta flow | expedition 선택 commit | eligibility 검증만 | 기본 무연출 |
| `SiteEntered` | site flow | site context 생성 완료 | site intro event 평가 | `story-card` + 선택적 `dialogue-overlay` |
| `BattleStarted` | combat flow | 전투 시작 확정 | pre-battle bark 평가 | 짧은 `dialogue-overlay` |
| `BattleResolved` | combat flow | battle result + reward table 확정 | story effect 적용 | `toast` 또는 짧은 `dialogue-overlay` |
| `RewardCommitted` | meta flow | 실제 reward 수령 완료 | hero unlock reaction, codex unlock | `toast` |
| `ExtractCommitted` | meta flow | site extract 처리 완료 | chapter/site resolve, hero unlock, hook 변경 | `story-card` + `toast` |
| `CampaignCompleted` | meta flow | final extract + credits gate 확정 | main conflict closed, endless unlock | `story-card` |
| `EndlessCycleStarted` | endless flow | endless state 생성 완료 | endless intro (once only) | `toast` 또는 짧은 `story-card` |

핵심 규칙:
1. Combat/Reward/Town 시스템이 truth를 먼저 commit한다.
2. `StoryDirectorService.Advance(moment, context)`가 호출된다.
3. 서비스는 `NarrativeProgressRecord`를 갱신하고 `StoryPresentationRequest`를 ordered list에 넣는다.
4. `SM.Unity` presenter는 queue를 재생만 하고 save/meta/combat truth를 만들지 않는다.

## 세이브 모델

`NarrativeProgressRecord`는 `SM.Meta`에 정의한다. 기존 root save에 `Narrative` 필드를 추가하고, 별도 narrative save 파일을 만들지 않는다.

| field | type | description | migration_default |
|---|---|---|---|
| `CurrentChapterId` | `string` | 마지막으로 확정된 chapter | `""` |
| `CurrentSiteId` | `string` | 마지막으로 확정된 site | `""` |
| `SeenEventIds` | `List<string>` | 한 번 이상 본 event IDs | empty list |
| `ResolvedEventIds` | `List<string>` | 효과까지 소비된 event IDs | empty list |
| `StoryFlags` | `Dictionary<string, bool>` | narrative flags | empty map |
| `UnlockedStoryHeroIds` | `List<string>` | 스토리 해금 hero IDs | empty list |
| `RecruitConversions` | `RecruitConversionStateRecord` | enemy-to-hero 전환 진행도 | default record |
| `PendingPresentations` | `List<StoryPresentationRequest>` | 다음 scene에서 재생할 ordered list | empty list |
| `EndlessCycle` | `EndlessCycleStateRecord` | endless mode 진행 상태 | null |

## 구현 패턴

```
BattleResolved
  -> Combat/Meta systems commit result
  -> StorySceneFlowBridge builds StoryMomentContext
  -> StoryDirectorService.Advance(BattleResolved, context)
  -> NarrativeProgressRecord updated
  -> StoryPresentationRequest appended
  -> StoryPresentationRunner drains ordered list
  -> StoryOverlayPresenter / DialoguePanelPresenter render
```

## 안티패턴

- presenter가 save truth를 생성하는 것 금지
- mutable static state 금지
- interface 남발 금지
- god MonoBehaviour 금지
- `SM.Content -> SM.Meta` 역참조 금지

## 작성 지침

- C# 타입명은 PascalCase, 문서 path/ID는 kebab/snake 규칙을 따른다.
- sample code보다 type table을 우선한다.
- scene flow는 gameplay truth commit 이후에만 narrative가 반응하는 순서로 적는다.
