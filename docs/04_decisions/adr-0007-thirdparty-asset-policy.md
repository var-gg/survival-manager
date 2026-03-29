# ADR-0007 서드파티 에셋 정책 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 결정일: 2026-03-29
- 소스오브트루스: `docs/04_decisions/adr-0007-thirdparty-asset-policy.md`
- 관련문서:
  - `docs/03_architecture/asset-intake-boundary.md`
  - `docs/05_setup/asset-workflow.md`

## 문맥

서드파티 Unity 에셋을 도입할 때 원본과 프로젝트 수정본의 경계가 흐려지면 업그레이드 비용과 손상 위험이 크게 늘어난다.
AI가 벤더 원본을 직접 편집하면 그 위험은 더 커진다.

## 결정

다음 정책을 채택한다.

- 벤더 원본은 `Assets/ThirdParty/**`에 둔다.
- 프로젝트 소유 wrapper, 수정, integration asset은 `Assets/_Game/**`에 둔다.
- 일반 작업에서 Codex는 벤더 원본을 직접 수정하지 않는다.
- 고위험 import와 예외 처리는 sandbox 검증과 사람 리뷰를 거친다.

## 결과

### 기대 효과

- 벤더 업그레이드와 교체가 쉬워진다.
- 프로젝트 소유 코드와 외부 코드의 책임이 분명해진다.

### 감수할 비용

- wrapper와 adapter 작성 비용이 추가된다.
- invasive modification을 강제하는 에셋은 도입이 어려울 수 있다.

## 후속

- intake boundary 문서와 asset workflow를 계속 동기화한다.
- 예외가 필요하면 문서화된 승인 경로를 거친다.
