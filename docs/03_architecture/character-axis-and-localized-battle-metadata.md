# Character 축과 localized battle metadata 흐름

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `docs/03_architecture/character-axis-and-localized-battle-metadata.md`
- 관련문서:
  - `docs/03_architecture/localization-runtime-and-content-pipeline.md`
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/03_architecture/session-realm-authority-and-offline-online-ports.md`
  - `docs/02_design/meta/character-race-class-role-archetype-taxonomy.md`
  - `docs/04_decisions/adr-0021-character-definition-identity-layer.md`

## 목적

이 문서는 `CharacterDefinition`을 identity layer로 도입한 뒤, runtime battle HUD와 editor inspector가 같은 축을 같은 localization 규칙으로 보여주는 경계를 정리한다.

## 레이어별 책임

- `SM.Content`
  - `CharacterDefinition`, `RaceDefinition`, `ClassDefinition`, `UnitArchetypeDefinition`, `RoleInstructionDefinition`
  - localization package를 모른다
  - `Id`와 `...Key`만 truth로 가진다
- `SM.Meta`
  - compile 시 `CharacterId`, `RoleInstructionId`를 carry-through 한다
  - battle truth는 여전히 archetype/loadout/snapshot이 만든다
- `SM.Unity`
  - `ContentTextResolver`
  - `BattleUnitMetadataFormatter`
  - battle HUD와 selected unit panel render
- `SM.Editor`
  - localized inspector
  - Quick Battle preview resolve
  - seed/validator/table sync

## resolve 우선순위

전투 metadata resolve는 아래 순서를 따른다.

1. `Character`
2. `Archetype`
3. `Race / Class`
4. `RoleInstruction`
5. `RoleFamily`는 class에서 파생

## Quick Battle 규칙

### ally

- ally slot truth는 계속 `HeroId`다
- hero instance가 `CharacterId`를 소유한다
- `RoleInstructionIdOverride`만 slot에서 override 가능하다

### enemy

- enemy slot truth는 `CharacterId`다
- `ArchetypeIdOverride`는 기본 전투 원형만 덮어쓴다
- `RoleInstructionId`는 기본 역할만 덮어쓴다
- legacy raw `RoleTag`는 migration fallback으로만 남긴다

## localization 흐름

### runtime

1. `GameLocalizationController`가 현재 locale을 소유한다
2. `ContentTextResolver`가 content table에서 localized text를 읽는다
3. `BattleUnitMetadataFormatter`가 axis label과 value를 조립한다
4. locale change 시 battle HUD와 actor overhead는 현재 step 기준으로 다시 그린다

### editor

1. `LocalizationSettings.SelectedLocale`를 기준 locale로 읽는다
2. custom inspector는 `selected -> ko -> en -> id` fallback 순서를 따른다
3. Quick Battle preview와 source asset inspector가 같은 resolver를 재사용한다

## validator 계약

- `CharacterDefinition`은 `Race / Class / DefaultArchetype / DefaultRoleInstruction`을 모두 가져야 한다
- `DefaultArchetype`의 race/class는 character와 일치해야 한다
- legacy prose가 남아 있으면 localization policy에 따라 warning 또는 error가 된다

## 구현 메모

- `RoleFamily`는 별도 asset을 만들지 않는다
- `duelist` canonical id는 유지하고, player-facing label만 `striker`로 읽는다
- battle selected panel과 inspector preview는 raw serialized field name 대신 localized label을 쓴다
