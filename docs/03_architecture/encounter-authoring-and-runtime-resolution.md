# 조우 authoring과 런타임 resolve

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/03_architecture/encounter-authoring-and-runtime-resolution.md`
- 관련문서:
  - `docs/02_design/combat/encounter-catalog-and-scaling.md`
  - `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`
  - `docs/03_architecture/combat-runtime-architecture.md`

## 목적

이 문서는 authored encounter catalog가 runtime battle path로 어떻게 resolve되는지 정의한다.
정상 story/runtime 경로는 더 이상 smoke-only encounter builder를 source-of-truth로 사용하지 않는다.

## canonical content model

조우 계층의 canonical authored model은 아래 다섯 타입이다.

- `CampaignChapterDefinition`
- `ExpeditionSiteDefinition`
- `EncounterDefinition`
- `EnemySquadTemplateDefinition`
- `BossOverlayDefinition`

canonical content root는 아래 경로를 사용한다.

- `Assets/Resources/_Game/Content/Definitions/CampaignChapters`
- `Assets/Resources/_Game/Content/Definitions/ExpeditionSites`
- `Assets/Resources/_Game/Content/Definitions/Encounters`
- `Assets/Resources/_Game/Content/Definitions/EnemySquads`
- `Assets/Resources/_Game/Content/Definitions/BossOverlays`

## runtime ownership

### session / run state

`GameSessionState`, `ActiveRunState`, `SaveProfile`는 아래 battle context를 저장한다.

- `ChapterId`
- `SiteId`
- `SiteNodeIndex`
- `EncounterId`
- `BattleSeed`
- `BattleContextHash`
- `RewardSourceId`
- `StoryCleared`
- `EndlessUnlocked`

### faction 경계

- `FactionId`는 encounter/site/squad metadata에만 머문다.
- `FactionId`는 synergy count, team tag family, compile family logic에 들어가지 않는다.

## resolve 흐름

1. `RuntimeCombatContentLookup`가 authored content snapshot을 로드한다.
2. `EncounterResolutionService`가 chapter/site/node context에서 `BattleContextState`를 만든다.
3. 같은 service가 `EncounterDefinition`과 `EnemySquadTemplateDefinition`을 읽어 enemy participant spec을 조립한다.
4. `BattleSetupBuilder.Build(...)`는 조립된 authored encounter spec만 소비한다.
5. boss encounter면 `BossOverlayDefinition`을 bootstrap에 추가한다.
6. `BattleScreenController`는 resolved context만 받아 battle simulation을 연다.

## deterministic seed 규칙

- battle seed 소유권은 run/session state가 가진다.
- seed는 `BattleContextHash`에서 계산한다.
- 같은 run id + chapter/site/node + encounter/reward source 조합은 같은 seed를 만든다.
- hard-coded `17`은 정상 경로에서 제거한다.

## boss bootstrap 규칙

- `EncounterKindValue.Boss`면 `BossOverlayDefinition`을 추가 조회한다.
- overlay는 captain/escort 구성 자체를 바꾸지 않고, phase/status/aura/reward tag를 bootstrap에 추가한다.
- launch floor에서 boss overlay는 `guarded` 같은 상태와 signature utility tag를 먼저 적용한다.

## debug-only fallback

정상 경로에서 authored catalog를 찾지 못할 때만 아래 fallback을 허용한다.

- `EncounterResolutionService.BuildDebugSmokeContext(...)`
- `BattleEncounterPlans.CreateObserverSmokePlan()`

이 경로는 `quick_smoke` / `debug_smoke_observer` context에서만 사용한다.

## validator / test oracle

- validator:
  - invalid encounter id
  - missing squad ref
  - invalid boss overlay ref
  - invalid reward/drop tag
  - faction leakage into synergy counted tags
- tests:
  - same node context => same encounter + same seed
  - all story sites cleared => endless unlock
  - normal runtime path does not resolve `debug_smoke_observer`
