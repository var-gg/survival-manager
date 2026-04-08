# _Game

- Status: active
- Last Updated: 2026-04-08
- Phase: prototype

## Purpose

`Assets/_Game`는 prototype playable vertical slice의 프로젝트 소유 구현 영역이다.

## Current Rules

- 구현은 `Assets/_Game/**`에서 진행한다.
- 테스트는 `Assets/Tests/**`에 둔다.
- `Assets/ThirdParty/**` 원본 vendor content는 직접 수정하지 않는다.

## Current Scope

- Boot -> Town -> Expedition -> Battle -> Reward -> Town canonical lane 구현과 관련 런타임/씬/UI 자산을 포함한다.
- battle actor wrapper, sample content, localization, persistence, editor validation 자산을 포함한다.
- debug smoke lane은 secondary path로만 유지한다.

## Maintenance Notes

- 빈 placeholder 폴더는 유지하지 않는다.
- 구조나 운영 기준이 바뀌면 관련 문서와 task status를 함께 갱신한다.
