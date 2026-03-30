# 작업 계획: 001 MVP Vertical Slice

- 상태: draft
- 최종수정일: 2026-03-31
- 단계: prototype
- 작업 ID: 001

## 목적

이 문서는 prototype 단계 첫 playable vertical slice의 단기 계획을 기록한다.
구현 dump가 아니라, 무엇을 언제까지 좁게 검증할지 정하는 planning shell이다.

## 현재 계획

1. prototype 단계 저장소 규칙을 고정한다.
2. playable slice 목표와 제약을 다시 확인한다.
3. 가장 좁은 proof scenario를 정의한다.
4. 필요한 최소 data, prefab, test asset을 식별한다.
5. slice 경계가 명확해진 뒤에만 구현을 시작한다.

## 현재 가드레일

- 구현은 `Assets/_Game/**`, `Assets/Tests/**` 안에 둔다.
- `Assets/ThirdParty/**` 원본은 수정하지 않는다.
- 좁고 검토 가능한 변경을 우선한다.
- 구현 결정과 함께 문서를 동기화한다.

## 즉시 풀어야 할 질문

- 어떤 encounter 또는 demo flow가 루프를 가장 빨리 증명하는가?
- 무엇을 실제 구현하고, 무엇을 시뮬레이션으로 대체할 것인가?
- prototype 성공 신호로 충분한 테스트/검증 기준은 무엇인가?
