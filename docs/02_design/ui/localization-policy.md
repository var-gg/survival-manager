# Localization 정책

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/ui/localization-policy.md`
- 관련문서:
  - `docs/03_architecture/localization-runtime-and-content-pipeline.md`
  - `docs/04_decisions/adr-0016-localization-boundary.md`
  - `docs/05_setup/localization-workflow.md`

## 목적

이 문서는 플레이어 노출 텍스트를 어떤 기준으로 분류하고 authoring할지 정의한다.
핵심은 `영어 ID`와 `표시 문자열`을 분리하고, MVP 단계부터 신규 텍스트 하드코딩을 막는 것이다.

## MVP locale 범위

- shipped locale: `ko`, `en`
- startup selector 순서: `PlayerPrefLocaleSelector -> SystemLocaleSelector -> SpecificLocaleSelector(en)`
- pseudo locale은 editor/layout 검증 전용으로만 유지한다.

## 기본 원칙

- 코드, 파일명, namespace, asmdef, API 식별자는 영어를 유지한다.
- `Id`, `ArchetypeId`, `SkillId`, `ItemId`, `AugmentId` 같은 domain identifier는 계속 영어 truth로 유지한다.
- 플레이어에게 보이는 텍스트는 반드시 localization table을 경유한다.
- 문자열 조립은 코드 `+` 연결 대신 Smart String 또는 semantic message token으로 처리한다.
- 신규 플레이어 노출 문자열은 raw literal로 merge하지 않는다.

## 테이블 구조

- 공통 UI: `UI_Common`
- 화면 UI: `UI_Town`, `UI_Expedition`, `UI_Battle`, `UI_Reward`
- 콘텐츠: `Content_Items`, `Content_Skills`, `Content_Augments`, `Content_Synergies`, `Content_Traits`, `Content_Races`, `Content_Classes`, `Content_Affixes`, `Content_Archetypes`, `Content_Passives`, `Content_TeamTactics`, `Content_Roles`, `Content_Tags`, `Content_Stats`, `Content_Rewards`, `Content_Expeditions`
- 로그/시스템: `Combat_Log`, `System_Messages`

## 키 규칙

- lower-case 점 표기만 허용한다.
- 사람이 읽는 문장 대신 안정적인 semantic key를 쓴다.
- 예:
  - `ui.common.confirm`
  - `ui.town.start_run`
  - `content.item.iron_sword.name`
  - `content.item.iron_sword.desc`
  - `combat.log.damage`

## 화면 정책

- 정적 버튼/라벨은 `LocalizeStringEvent` 또는 project binder를 사용한다.
- locale change 후 같은 화면에서 즉시 refresh되어야 한다.
- layout, sprite on/off, locale별 오브젝트 차이는 예외적으로만 Property Variants를 쓴다.
- 일반 텍스트 카탈로그 용도에는 Property Variants를 쓰지 않는다.

## 콘텐츠 정책

- `ScriptableObject` authored definition은 raw prose 대신 `NameKey`, `DescriptionKey`, `LabelKey` 같은 key를 가진다.
- legacy prose 필드는 migration source로 한 사이클만 숨김 보존한다.
- validator는 phase 규칙에 따라 legacy prose 잔존, key naming 위반, missing table entry를 경고 또는 실패로 처리한다.

## fallback phase 정책

### `PhaseA`

- runtime fallback: 허용
- validator severity: warning
- 목적: localization 도입 초기 migration

### `PhaseB`

- runtime fallback: `editor/dev`에서만 허용
- validator severity: shipped player-facing content 기준 error
- 현재 저장소 기본값: `PhaseB`
- 목적: committed content를 localization key 중심으로 굳히는 단계

### `PhaseC`

- runtime fallback: 허용 안 함
- validator severity: missing entry와 legacy prose 모두 error
- 목적: shipped player-facing content 최종 gate

## sunset rule

- migration fallback은 `PhaseA -> PhaseB -> PhaseC`로 줄여야 한다.
- `PhaseB` 이후에는 hidden fallback이 빠진 key를 가리면 안 된다.
- 출시 주장 전에 player-facing content는 `PhaseC` 규칙을 만족해야 한다.

## 로그 정책

- `BattleEvent`는 최종 문장을 저장하지 않는다.
- 전투 로그는 `BattleLogCode`와 typed payload를 저장하고 UI에서 `Combat_Log` Smart String으로 렌더링한다.
- locale change 후 최근 로그는 다시 렌더링 가능해야 한다.

## 폰트 정책

- MVP는 ko/en 공용 UI font 1개를 기본 경로로 유지한다.
- glyph 부족은 locale별 font swap보다 fallback font 또는 공용 font 교체로 먼저 해결한다.
- locale별 asset swap은 실제 glyph/branding 차이가 확인된 뒤에만 추가한다.
