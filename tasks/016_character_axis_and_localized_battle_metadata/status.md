# Task 016 Status

- 상태: completed
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `tasks/016_character_axis_and_localized_battle_metadata/status.md`

## Current state

- `CharacterDefinition` 타입, `CharacterId` carry-through, quick battle enemy `CharacterId` 경로가 런타임/컴파일 경로까지 반영됐다.
- battle HUD는 `BattleUnitMetadataFormatter`를 통해 `Character / Archetype / Race / Class / Role / RoleFamily / Anchor`를 locale-aware 상태로 노출하고, Battle shell panel title / help / settings chrome도 같은 locale refresh 경로로 정리됐다.
- Quick Battle asset inspector, `CharacterDefinition`, `RoleInstructionDefinition`, `UnitArchetypeDefinition` preview는 localized custom inspector 경로로 닫혔다.
- `Content_Characters` string table, `Characters/**` sample assets, quick battle default config가 저장소에 실제 적재됐다.
- design / architecture / ADR / task / index 문서는 이번 슬라이스 기준으로 동기화됐다.

## Acceptance matrix

- `Character` 축 도입: 완료
- battle 화면 축 노출: 완료
- Quick Battle inspector localized preview: 완료
- seed/validator/localization sync: 완료
- docs/task/ADR/index sync: 완료

## Evidence

- 코드: `Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleUnitMetadataFormatter.cs`
- 코드: `Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleScreenPresenter.cs`
- 코드: `Assets/_Game/Scripts/Runtime/Unity/BattleScreenController.cs`
- 코드: `Assets/_Game/Scripts/Editor/Authoring/Inspectors/CombatSandboxConfigEditor.cs`
- 코드: `Assets/Tests/EditMode/CharacterAxisLocalizationTests.cs`
- 콘텐츠: `Assets/Resources/_Game/Content/Definitions/Characters/character_warden.asset`
- 로컬라이제이션: `Assets/Localization/StringTables/Content_Characters_ko.asset`
- 문서: `docs/02_design/meta/character-race-class-role-archetype-taxonomy.md`
- 문서: `docs/03_architecture/character-axis-and-localized-battle-metadata.md`
- ADR: `docs/04_decisions/adr-0021-character-definition-identity-layer.md`

## Remaining blockers

- 열린 Unity 인스턴스 때문에 fresh `test-batch-fast` / `test-batch-edit` batch evidence 수집은 막힌다.
- Town / Expedition / Reward까지 포함한 same-scene locale flip manual smoke evidence는 아직 별도 캡처가 없다.

## Deferred / debug-only

- story/faction/portrait/voice
- same character multi-archetype

## Loop budget consumed

- 1회: 구조 탐색과 carry-through 반영
- 1회: battle UI / inspector / seed / validator 연결

## Handoff notes

- 다음 루프는 열린 Unity editor에서 authored battle / quick smoke battle 분리, battle HUD locale refresh, Town / Expedition / Reward locale hot-refresh를 실제 확인하는 것이다.
- 사용자 로컬 editor 세션이 들고 있는 scene/localization dirty state는 이번 커밋 범위에서 제외한다.
