# 캐릭터 / 종족 / 직업 / 역할 / 전투 원형 taxonomy

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-02
- 소스오브트루스: `docs/02_design/meta/character-race-class-role-archetype-taxonomy.md`
- 관련문서:
  - Pindoc character-lore / roster artifacts
  - `docs/02_design/combat/team-tactics-and-unit-rules.md`
  - `docs/03_architecture/character-axis-and-localized-battle-metadata.md`
  - `docs/04_decisions/adr-0021-character-definition-identity-layer.md`

## 목적

이 문서는 prototype과 launch floor에서 `Character / Race / Class / Role / Archetype`을 어떻게 분리하고 연결하는지 정의한다.

## 용어 고정

- `Character` = 캐릭터
- `Race` = 종족
- `Class` = 직업
- `Role` = 역할
- `RoleFamily` = 역할군
- `Archetype` = 전투 원형

## 축별 ownership

- `Character`
  - 서사, 이름, 정체성 축
  - `Race / Class / DefaultArchetype / DefaultRoleInstruction`를 묶는 identity layer
- `Race`
  - 종족 시너지와 세계관 분류 축
- `Class`
  - 성장, passive board, class synergy owner
- `RoleFamily`
  - class에서 파생되는 플레이어-facing 설명 라벨
  - 별도 authored asset을 만들지 않는다
- `Role`
  - 전투 운용 지시 축
  - `RoleInstructionDefinition`이 소유한다
- `Archetype`
  - 실제 전투 패키지 owner
  - 스킬셋, 행동 성향, 기본 스탯, 기본 전술 preset을 묶는다

## 관계 규칙

1. 캐릭터는 반드시 하나의 종족과 하나의 직업을 가진다.
2. 캐릭터는 기본 전투 원형 하나와 기본 역할 지시 하나를 가진다.
3. 같은 종족/같은 직업 조합이라도 서로 다른 캐릭터가 될 수 있다.
4. 같은 캐릭터라도 이후 패스에서 전투 원형 override나 역할 override를 가질 수 있다.
5. `RoleFamily`는 class에서 파생되므로 저장 truth로 들고 다니지 않는다.

## 현재 launch floor 해석

- launch floor는 `12 core archetypes`를 유지한다.
- 현재 실행 `CharacterDefinition` 카탈로그는 `12 core characters + 4 specialist characters`의 16개 exact set으로 닫는다.
- 16개 실행 캐릭터는 paid launch safe target authoring closure이며, 20명 lore registry 전체를 playable roster로 승격했다는 뜻이 아니다.
- Relicborn core 4명(`aegis_sentinel / echo_savant / shardblade / prism_seeker`)은 `race_relicborn`과 대응 archetype asset이 닫히기 전까지 lore-only/deferred-runtime으로 둔다.
- 이 상태는 기존 archetype 중심 구조에 빠져 있던 캐릭터 축을 명시화한 것이며, 신규 종족 runtime closure를 암시하지 않는다.

## 전투 화면 노출 규칙

- 모든 유닛 오버헤드
  - `Character (Archetype)`
  - `Race / Class / Role`
- 선택 패널
  - `Character / Archetype / Race / Class / Role / RoleFamily / Anchor`

## 편집 규칙

- Quick Battle 인스펙터는 ally/enemy 모두 위 축을 preview로 보여줘야 한다.
- ally slot은 `HeroId`를 소유하고, `RoleInstructionIdOverride`만 override한다.
- enemy slot은 `CharacterId`를 기본 owner로 두고, 필요 시 `ArchetypeIdOverride`와 `RoleInstructionId`로 덮어쓴다.

## deferred

- story faction
- Relicborn runtime race/archetype/character promotion
- portrait/voice
- character 고유 passive/quest hook
- same character multi-archetype 변형
