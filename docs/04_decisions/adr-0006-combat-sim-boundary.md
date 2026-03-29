# ADR-0006 전투 시뮬레이션 경계 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 결정일: 2026-03-29
- 소스오브트루스: `docs/04_decisions/adr-0006-combat-sim-boundary.md`
- 관련문서:
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/03_architecture/bounded-contexts.md`

## 문맥

전투 규칙을 `MonoBehaviour`와 scene orchestration에 직접 넣으면 읽기, 테스트, 재사용이 빠르게 무너진다.
전투 truth와 시각 연출을 분리해야 vertical slice 이후에도 구조를 유지할 수 있다.

## 결정

전투 시뮬레이션은 UnityEngine 결합을 최소화한 순수 C# 영역에 둔다.
전투 truth는 `SM.Combat`에서 만들고, Unity scene/animation/battle visual은 그 결과를 소비하는 adapter로 둔다.

## 결과

### 기대 효과

- 전투 규칙을 EditMode 테스트로 검증하기 쉬워진다.
- presentation을 교체해도 battle truth를 다시 쓰지 않아도 된다.
- scene script가 전투 규칙 저장소가 되는 것을 막는다.

### 감수할 비용

- domain 결과와 presentation 상태 사이의 변환 계층이 필요하다.
- 빠른 scene 해킹보다 초기 속도가 느릴 수 있다.

## 후속

- `SM.Combat`은 presentation 가정을 직접 갖지 않는다.
- scene script는 outcome, event, read model을 소비하는 쪽으로 유지한다.
