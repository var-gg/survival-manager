# ADR-0008 에디터 브리지 정책 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 결정일: 2026-03-29
- 소스오브트루스: `docs/04_decisions/adr-0008-editor-bridge-policy.md`
- 관련문서:
  - `docs/05_setup/unity-mcp.md`
  - `docs/05_setup/mcp-adoption-plan.md`
  - `docs/03_architecture/unity-boundaries.md`

## 문맥

Unity MCP 같은 editor bridge는 inspection과 자동화를 빠르게 만들 수 있지만, 숨은 state mutation과 대형 오동작 위험도 함께 가져온다.
도구를 먼저 설치하고 규칙을 나중에 만드는 접근은 이 저장소에 맞지 않는다.

## 결정

정책 우선 접근을 채택한다.

- editor bridge는 기본 설치/기본 의존성으로 두지 않는다.
- 허용 작업, 금지 작업, 리뷰 필요 작업을 먼저 문서화한다.
- 향후 도입 시 읽기 위주와 좁은 범위 쓰기 작업을 우선한다.
- `Assets/ThirdParty/**`와 `Assets/_Game/**` 경계를 bridge 작업에서도 그대로 유지한다.
- 고위험 작업은 sandbox 우선과 rollback 경로를 요구한다.

## 결과

### 기대 효과

- 무분별한 툴 의존을 늦추고 통제한다.
- 구조 정책과 도구 사용 정책이 충돌하지 않게 한다.

### 감수할 비용

- 단기적으로는 수동 또는 파일 기반 작업이 더 많다.
- 도입 평가에 별도 거버넌스 비용이 든다.

## 후속

- 운영 기준은 `docs/05_setup/unity-mcp.md`에 유지한다.
- bridge 예외 도입은 별도 문서와 검토를 거친다.
