# ADR-0021 CharacterDefinition identity layer 도입

- 상태: accepted
- 날짜: 2026-04-07
- 결정자: repository
- 관련문서:
  - `docs/02_design/meta/character-race-class-role-archetype-taxonomy.md`
  - `docs/03_architecture/character-axis-and-localized-battle-metadata.md`
  - `docs/03_architecture/localization-runtime-and-content-pipeline.md`

## 문맥

기존 prototype은 `Archetype`이 사실상 캐릭터처럼도 쓰이고 있었다. 이 구조로는 아래를 동시에 만족시키기 어렵다.

- 같은 종족/같은 직업의 다른 캐릭터
- archetype과 전술 역할의 분리
- launch 이후 story/portrait/voice 확장
- battle HUD와 inspector에서 identity 축을 안정적으로 노출하는 일

## 결정

`Archetype` 위에 `CharacterDefinition` identity layer를 추가한다.

- `CharacterDefinition`
  - `Id`
  - `NameKey`
  - `DescriptionKey`
  - `Race`
  - `Class`
  - `DefaultArchetype`
  - `DefaultRoleInstruction`

`RoleFamily`는 별도 asset으로 authoring하지 않고 class에서 파생한다.

## 결과

- battle compile과 read model은 `CharacterId`를 carry-through 한다
- Quick Battle enemy slot은 `CharacterId` 중심으로 authoring한다
- battle HUD와 inspector는 `Character / Archetype / Race / Class / Role / RoleFamily / Anchor`를 locale-aware하게 보여줄 수 있다
- 현재 launch floor에서는 character id를 core archetype id와 1:1로 맞춰 risk를 낮춘다

## 트레이드오프

- 단기적으로 asset 수와 validator 범위가 늘어난다
- `Archetype`만으로 충분하던 단순 경로가 하나 더 깊어진다
- 대신 story/identity 확장과 전술 축 분리가 쉬워진다
