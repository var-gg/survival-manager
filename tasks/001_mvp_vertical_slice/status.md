# Task Status: 001 MVP Vertical Slice

- Status: active
- Last Updated: 2026-03-29
- Phase: prototype
- Task ID: 001

## Current State

현재 playable 상태 기준으로, 가장 보수적으로 확인 가능한 자동 루프는 `Boot -> Town`이다.
PlayMode smoke는 더 이상 빈 GameObject 생성 테스트가 아니라, 실제 `Boot` scene load와 session root 생성, 그리고 가능 시 `Town` 진입을 본다.

코드 기준으로는 `Town -> Expedition -> Battle -> Reward -> Town` adapter가 추가되어 첫 playable 루프 뼈대가 있다.
다만 이 전체 루프는 Unity scene wiring과 버튼 연결까지 에디터에서 최종 확인이 아직 필요하다.

## Completed in This Step

- 기존 PlayMode smoke를 Boot scene load 기반으로 교체
- `Boot` 진입 후 `GameSessionRoot` 존재 검증 추가
- 가능 시 `Boot -> Town` 자동 진입 검증 추가
- first playable review 문서 추가
- local runbook / mvp playtest checklist를 첫 playable 기준으로 갱신
- 상태 문서를 "문서 정리"가 아니라 실제 playable 검증 상태 기준으로 갱신

## Verified / Playable Boundary

### 자동 검증됨
- Boot scene load
- session root 생성
- Boot -> Town 자동 진입 시도

### 수동 playable 목표
- Town -> Expedition -> Battle -> Reward -> Town

## Risks

- Battle/Reward 포함 전체 루프는 코드상 adapter가 있어도 scene wiring 미완료면 실제 에디터에서는 바로 보이지 않을 수 있음
- Town/Expedition/Battle/Reward는 아직 debug placeholder UI 단계
- Postgres는 여전히 placeholder adapter
- sample content 미생성 시 bootstrap이 멈출 수 있음

## Next State

다음 우선순위:

1. Town/Expedition/Battle/Reward 씬 내부 Canvas/Text/Button 참조를 실제로 연결
2. Boot -> Town 이후 수동 클릭 경로를 에디터에서 직접 검증
3. 가능하면 Reward 선택까지 PlayMode integration coverage 확대
