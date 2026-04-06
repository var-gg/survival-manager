# Task 016 Spec

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `tasks/016_character_axis_and_localized_battle_metadata/spec.md`

## Goal

`Character` 축을 `Race / Class / Archetype / Role`과 분리하고, Quick Battle 전투화면과 인스펙터에서 모든 축을 locale-aware하게 노출한다.

## Authoritative boundary

- content truth: `CharacterDefinition`, `RaceDefinition`, `ClassDefinition`, `UnitArchetypeDefinition`, `RoleInstructionDefinition`
- compile truth: `CharacterId`, `RoleInstructionId`
- UI truth: localized resolver가 조립한 view text

## In scope

- `CharacterDefinition` 도입
- `HeroInstanceRecord.CharacterId`
- quick battle ally/enemy preview resolve
- battle HUD axis 노출
- localized custom inspector
- seed/validator/localization table 보강
- task/doc/ADR 기록

## Out of scope

- story faction
- portrait/voice
- 온라인 authoritative 서버 연동
- same character multi-archetype system

## asmdef impact

- 없음
- 기존 `SM.Content`, `SM.Meta`, `SM.Unity`, `SM.Editor`, `SM.Tests` 안에서만 변경

## persistence impact

- `HeroInstanceRecord`와 `HeroRecord`에 `CharacterId`가 추가된다
- missing 값은 `ArchetypeId`로 backfill 한다

## validator / test oracle

- `CharacterDefinition` reference mismatch validator
- FastUnit fallback/localized metadata 테스트
- Quick Battle inspector preview

## done definition

- Quick Battle 전투화면에서 유닛 축이 보인다
- Quick Battle asset inspector에서 ally/enemy 축 preview가 보인다
- 한국어 locale일 때 battle label과 inspector label이 한글로 나온다
- docs/task/ADR/index가 동기화된다

## deferred

- character 고유 서사 필드
- story/faction integration
- portrait/voice presentation layer
