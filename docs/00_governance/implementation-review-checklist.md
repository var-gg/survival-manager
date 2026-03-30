# 구현 검수 체크리스트

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/00_governance/implementation-review-checklist.md`
- 관련문서:
  - `docs/03_architecture/coding-principles.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/04_decisions/adr-0012-code-structure-and-dependency-policy.md`

## 목적

이 문서는 인간과 AI가 공통으로 쓰는 구조 검수 질문을 제공한다.
각 항목은 예/아니오로 답할 수 있어야 하며, 아니오일 때 즉시 취할 조치를 같이 적는다.

## 체크리스트

| 항목 | 예/아니오 질문 | 실패 시 조치 |
| --- | --- | --- |
| 문서/구현 동기화 | 변경된 구조나 정책이 관련 문서와 인덱스에 반영되었는가? | 관련 `index.md`, 기준 문서, ADR 또는 task 문서를 같은 변경에 포함한다. |
| 파일 책임 | 클래스/파일이 한 종류의 truth만 소유하는가? | domain, persistence, Unity adapter 책임을 분리하고 god file을 해체한다. |
| 의존 방향 | asmdef와 namespace 의존이 `dependency-direction.md`를 따르는가? | 허용되지 않은 참조를 제거하고 port 또는 더 낮은 shared contract로 다시 설계한다. |
| asmdef 순환 | 새로운 순환 참조가 생기지 않았는가? | 공통 계약을 하위 계층으로 내리거나 조립 지점을 composition root로 옮긴다. |
| `MonoBehaviour` 책임 | `MonoBehaviour`가 orchestration/view/input에 머무르는가? | 규칙 계산과 저장 I/O를 domain/persistence 서비스로 이동한다. |
| `ScriptableObject` 책임 | `ScriptableObject`가 authored definition만 다루는가? | runtime mutable field와 save payload를 별도 타입으로 분리한다. |
| localization 경계 | 신규 플레이어 노출 텍스트가 localization key/table을 경유하는가? | raw literal을 제거하고 key, table, validator, ko/en entry를 같은 변경에 포함한다. |
| global state | `static` mutable global state가 session truth를 대신하지 않는가? | 상태를 session root, runtime state, repository 경계로 옮긴다. |
| 임시 유틸 남발 | `Manager`, `Helper`, `Util`, `Common` 같은 임시 해킹성 파일이 늘어나지 않았는가? | 역할 이름이 분명한 새 타입으로 쪼개고 호출 경로를 재배치한다. |
| 테스트/검증 | 구조 변경에 맞는 최소 검증이 추가되었는가? | EditMode, validator, checklist, 수동 검증 중 최소 하나를 추가한다. |
| ADR 필요성 | 이번 변경이 durable choice인데 ADR이 빠지지 않았는가? | `docs/04_decisions/`에 ADR을 추가하거나 기존 ADR을 갱신한다. |

## 사용 규칙

- 하나라도 `아니오`면 그대로 merge-ready로 보지 않는다.
- 구조 변경인데 테스트가 없으면 최소한 validator, checklist, 수동 검증 근거를 남긴다.
- 구조 정책 위반은 “나중에 정리” 메모로 닫지 않는다.
