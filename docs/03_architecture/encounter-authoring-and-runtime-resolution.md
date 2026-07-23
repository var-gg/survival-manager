# 조우 authoring과 런타임 resolve

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-23
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
4. `EnemySquadMemberDefinition.RuleModifierTags`는 `EnemySquadMemberTemplate` -> `BattleParticipantSpec.RuleModifierTags` -> `BattleSetupBuilder`를 거쳐 `CombatRuleModifierPackage`로 컴파일된다.
5. `BattleSetupBuilder.Build(...)`는 조립된 authored encounter spec만 소비한다.
6. boss encounter면 `BossOverlayDefinition`을 bootstrap에 추가한다.
7. `BattleScreenController`는 resolved context만 받아 battle simulation을 연다.

## deterministic seed 규칙

- battle seed 소유권은 run/session state가 가진다.
- seed는 `BattleContextHash`에서 계산한다.
- 같은 run id + chapter/site/node + encounter/reward source 조합은 같은 seed를 만든다.
- hard-coded `17`은 정상 경로에서 제거한다.

## boss bootstrap 규칙

- `EncounterKindValue.Boss`면 `BossOverlayDefinition`을 추가 조회한다.
- overlay는 captain/escort 구성 자체를 바꾸지 않고, phase/status/aura/reward tag와 선택적인 boss 전용 pressure clock을 bootstrap에 추가한다.
- launch floor에서 boss overlay는 `guarded` 같은 상태와 signature utility tag를 먼저 적용한다.

### boss pressure clock 계약

- authored source는 `BossOverlayDefinition`의 `PressureClockFirstPulseSeconds`, `PressureClockIntervalSeconds`, `PressureClockMaxHealthDamageRatio`, `PressureClockMaxPulses` 네 필드다. 네 값이 모두 양수일 때만 clock이 활성화된다.
- `SM.Unity.ContentConversion`은 authored 필드를 pure `BossPressureClockSpec`으로 변환하고, `EncounterResolutionService`는 boss captain의 `BattleUnitLoadout.BossPressureClock`에만 이를 부착한다. 일반 적과 escort에는 복제하지 않는다.
- `BossPressureClockService`는 `BattleState.StepIndex`와 fixed step을 사용해 첫 pulse와 간격을 정수 tick으로 양자화한다. captain이 살아 있는 동안 최대 횟수까지만 발동하며 RNG와 mutable static state를 사용하지 않는다.
- pulse는 생존 ally 각각에게 대상 최대 체력 비율 피해를 준다. 기존 `TakeDamage` 경로를 사용하므로 barrier가 먼저 흡수하고, 피해를 받은 유닛은 기존 피격 energy를 얻는다. 상시 heal reduction이나 원시 HP/ATK overlay로 대체하지 않는다.
- 각 pulse는 `boss_pressure_clock:pulse_{n}` battle event와 `boss_pressure_clock` telemetry marker를 남긴다. clock kill도 기존 kill/assist 및 trigger 경로로 합류한다.
- Atlas encounter preview는 첫 pulse 시점, 간격, 최대 체력 비율, 최대 횟수를 boss 정보에 노출한다. clock을 숨긴 채 런타임에만 발동시키지 않는다.

## debug-only fallback

정상 경로에서 authored catalog를 찾지 못할 때만 아래 fallback을 허용한다.

- `EncounterResolutionService.BuildDebugSmokeContext(...)`
- `BattleEncounterPlans.CreateObserverSmokePlan()`

이 경로는 `quick_smoke` / `debug_smoke_observer` context에서만 사용한다.
normal expedition lane은 `GameSessionState.TryResolveCurrentEncounter()`가 `EncounterResolutionService.TryResolveEncounter()`를 먼저 호출하고, 실패할 때만 debug fallback으로 내려간다.

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
  - authored enemy squad member `RuleModifierTags` reach enemy `BattleUnitLoadout.RulePackages`
  - pressure clock은 authored tick에만 발동하고 barrier를 존중하며 최대 횟수 뒤 멈춤
  - captain 사망 뒤 pressure clock 미발동
  - boss preview에 authored pressure clock 값 노출
