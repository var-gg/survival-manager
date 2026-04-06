# Localization runtime과 content 파이프라인

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `docs/03_architecture/localization-runtime-and-content-pipeline.md`
- 관련문서:
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/02_design/combat/battle-presentation-contract.md`
  - `docs/04_decisions/adr-0016-localization-boundary.md`
  - `docs/05_setup/localization-workflow.md`

## 목적

이 문서는 localization이 어떤 layer에 머물고, content authoring과 runtime UI refresh가 어디서 일어나는지 정의한다.

## 경계

- `SM.Content`: localization package를 참조하지 않는다. 콘텐츠 truth는 `Id`와 `...Key`만 가진다.
- `SM.Combat`: localization package를 참조하지 않는다. 전투 truth는 `BattleLogCode`와 typed payload만 가진다.
- `SM.Meta`: reward/application 결과를 semantic key 또는 audit summary로 기록한다.
- `SM.Unity`: `GameLocalizationController`, `ContentTextResolver`, scene binder, overlay UI를 소유한다.
- `SM.Editor`: localization foundation bootstrap, table sync, validator, seed generator를 소유한다.

## 런타임 흐름

1. Boot에서 `GameLocalizationController`가 `LocalizationSettings` 초기화를 보장한다.
2. startup selector는 `PlayerPref -> System -> Specific(en)` 순서로 locale을 고른다.
3. active scene binder가 공통 UI font, localizer binder, 글로벌 언어 오버레이를 scene에 주입한다.
4. 화면 controller는 locale change 이벤트를 받아 자기 view를 다시 그린다.
5. 동적 콘텐츠명, 증강명, 아이템명은 `ContentTextResolver`를 통해 key -> localized text로 변환한다.

## content authoring 계약

- definition asset은 raw prose 대신 `NameKey`, `DescriptionKey`, `LabelKey`, `RewardSummaryKey`를 가진다.
- `CharacterDefinition`도 같은 규칙을 따르고 `Content_Characters` table을 사용한다.
- key naming은 lower-case dot 표기만 허용한다.
- committed asset이 runtime truth이고, bootstrap/generator는 누락 복구만 담당한다.
- validator는 duplicate id, duplicate key, missing locale entry, legacy prose 잔존을 함께 검사한다.
- battle scene은 missing key를 warning으로만 기록하고, 화면에는 fallback label만 보여야 한다.

## fallback phase와 구현 매핑

### phase 정의

- `PhaseA`
  - runtime fallback 허용
  - validator는 missing entry와 legacy prose를 warning으로 취급
- `PhaseB`
  - runtime fallback은 `editor/dev`에서만 허용
  - validator는 shipped player-facing content의 missing entry와 legacy prose를 error로 취급
- `PhaseC`
  - runtime fallback 금지
  - shipped player-facing content에 raw id나 legacy prose가 드러나면 안 된다

### 현재 구현 매핑

- phase truth: `SM.Content.Definitions.ContentLocalizationPolicy`
- runtime gate: `SM.Unity.GameLocalizationController`
- content text resolve: `SM.Unity.ContentTextResolver`
- validator severity: `SM.Editor.Validation.ContentDefinitionValidator`

현재 저장소 기본 phase는 `PhaseB`다.

## 세션 요약과 메시지 계약

- `GameSessionState`는 raw UI 문장을 오래 들고 있지 않는다.
- session summary는 `SessionTextToken`처럼 locale refresh 가능한 semantic token으로 유지한다.
- Town / Expedition / Reward 화면은 token을 locale 기준으로 resolve해서 표시한다.

## 전투 로그 계약

- `BattleEvent.Note` 같은 localized prose는 금지한다.
- combat runtime은 `BattleLogCode`와 numeric payload만 저장한다.
- `BattleScreenController`가 `Combat_Log` Smart String으로 최근 로그를 렌더링한다.
- battle unit axis text는 `BattleUnitMetadataFormatter`가 `ContentTextResolver`를 통해 조립한다.
- locale change 후 `_recentLogs`를 다시 문자열로 변환해도 event truth는 바뀌지 않는다.

## Addressables와 preload

- Unity Localization table과 locale asset은 Addressables 기반으로 관리된다.
- MVP에서는 `UI_Common`, `UI_Town`, `UI_Expedition`, `UI_Battle`, `UI_Reward`, `Combat_Log`, `System_Messages`를 preload 대상으로 둔다.
- runtime은 `WaitForCompletion`에 의존하지 않고 initialization 이후 event-driven refresh를 기본으로 한다.

## migration 전략

- 신규 텍스트는 즉시 localization 경유로 authoring한다.
- 기존 하드코딩 문자열은 수정되는 파일부터 점진적으로 치환한다.
- legacy prose 필드는 migration source로만 잠시 유지하고 validator로 퇴거를 강제한다.
- shipped player-facing content는 `PhaseC` 진입 전에 fallback 없이 동작해야 한다.
