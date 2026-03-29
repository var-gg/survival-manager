# ADR-0012 코드 구조와 의존 정책 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 결정일: 2026-03-29
- 소스오브트루스: `docs/04_decisions/adr-0012-code-structure-and-dependency-policy.md`
- 관련문서:
  - `docs/03_architecture/coding-principles.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/00_governance/implementation-review-checklist.md`

## 문맥

AI는 기능을 빨리 통과시키려 할 때 거대한 파일, 무분별한 interface, scene-first hack, 상향 의존 누수를 반복적으로 만들기 쉽다.
이 저장소는 prototype 단계지만 vertical slice 이후에도 구조를 유지할 수 있어야 하므로, 코딩 원칙과 의존 정책을 기본 선택으로 명시할 필요가 있다.

## 결정

이 저장소는 다음 기본 선택을 채택한다.

- 클래스와 파일은 한 종류의 truth만 소유한다.
- `SM.*` asmdef는 하향 의존만 허용하며 순환 참조를 금지한다.
- `SM.Unity`는 orchestration/view/input adapter이고 battle truth/save truth를 만들지 않는다.
- interface와 abstract class는 실제 경계, 대체 구현, 테스트 이점이 있을 때만 도입한다.
- runtime instance, save model, authored definition은 별도 타입으로 유지한다.
- 구조 변경은 기준 문서, 체크리스트, ADR 갱신 없이 완료로 보지 않는다.

## 반복되는 AI 구조 실수

- `Manager` 하나에 규칙, 저장, UI, scene 전환을 몰아넣는다.
- 구현이 하나뿐인데 interface를 기계적으로 생성한다.
- `MonoBehaviour` 안에서 계산과 I/O를 같이 처리한다.
- 편의상 상위 계층에서 하위 adapter 세부를 직접 다룬다.
- 문서 정합성 없이 asmdef, 경로, 정책을 바꾼다.

## 결과

### 기대 효과

- 구조 리뷰 기준이 명확해진다.
- asmdef와 파일 분리가 우연이 아니라 정책이 된다.
- AI가 빠른 해킹 경로를 기본값으로 택하지 못하게 막는다.

### 감수할 trade-off

- 초기 구현 속도가 약간 느려질 수 있다.
- 작은 작업에도 문서와 구조 검토 비용이 추가된다.
- 과도한 추상화 대신 정확한 분류와 분리에 더 많은 판단이 필요하다.

## 후속

- 구조 변경 검수에는 `implementation-review-checklist.md`를 사용한다.
- 에이전트 구조 점검에는 `code-structure-guard` 스킬을 사용한다.
- 세부 기준은 `coding-principles.md`, `dependency-direction.md`, `unity-boundaries.md`가 유지한다.
