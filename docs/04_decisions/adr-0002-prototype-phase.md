# ADR-0002 prototype 단계 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 결정일: 2026-03-29
- 소스오브트루스: `docs/04_decisions/adr-0002-prototype-phase.md`
- 관련문서:
  - Pindoc strategy/product artifacts
  - `tasks/001_mvp_vertical_slice/spec.md`

## 문맥

저장소는 이전까지 skeleton 단계 제약으로 운영되었다.
이제는 실제 playable proof를 검증할 수 있을 정도의 구현을 허용하되, 안전 경계를 무너뜨리지 않는 단계 승격이 필요하다.

## 결정

저장소 phase를 `skeleton`에서 `prototype`으로 승격한다.

이 결정과 함께 다음 기준을 채택한다.

- 현재 목표는 목각인형 수준의 playable vertical slice 검증이다.
- 구현 허용 범위는 `Assets/_Game/**`, `Assets/Tests/**`로 제한한다.
- `Assets/ThirdParty/**` 원본 직접 수정 금지는 유지한다.
- 구조/정책 변화는 문서와 구현을 같은 작업 단위에서 갱신한다.

## 결과

### 기대 효과

- 좁은 범위의 실제 구현을 시작할 수 있다.
- AI가 수정해도 안전 구역을 명확히 유지할 수 있다.
- vertical slice 중심 우선순위를 저장소 전체에 고정한다.

### 감수할 비용

- 빠른 우회 구현을 하고 싶어도 허용 구역을 지켜야 한다.
- 구조 정책을 어기면 나중에 되돌리는 비용이 커진다.

## 후속

- vertical slice 작업은 `tasks/001_mvp_vertical_slice/` 아래에서 추적한다.
- 거버넌스와 아키텍처 문서는 prototype 기준에 맞춰 계속 정비한다.
