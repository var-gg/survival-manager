# ADR-0011 MCP 도입 정책

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 결정일: 2026-03-29
- 소스오브트루스: `docs/04_decisions/adr-0011-mcp-adoption-policy.md`
- 관련문서:
  - `docs/05_setup/mcp-adoption-plan.md`
  - `docs/05_setup/unity-mcp.md`
  - `docs/04_decisions/adr-0008-editor-bridge-policy.md`

## 문맥

Discord 기반 외부 Codex 작업 흐름에서 Unity editor 상태를 빠르게 확인하고 제한적으로 조작하는 수단은 유용하다.
반면 MCP를 런타임 의존성처럼 취급하면 구조 경계가 흐려지고 main 브랜치가 실험성 툴링에 종속될 수 있다.

## 결정

이 저장소는 Unity MCP를 local development와 tooling acceleration 용도로만 평가한다.
구체적 기준은 다음과 같다.

- 목적은 scene/gameobject/asset/menu/console 작업 가속이다.
- MCP는 main 브랜치 런타임 또는 production gameplay dependency가 아니다.
- 대량 삭제, `Assets/ThirdParty/**` 수정, 무승인 package import를 금지한다.
- first playable 이후 tooling branch에서 read-heavy 실험을 우선한다.
- 공식 경로와 커뮤니티 경로는 운영 문서 기준으로 비교 평가한다.

## 결과

### 기대 효과

- tooling 실험과 런타임 구조를 분리할 수 있다.
- 에디터 자동화 도입이 구조 정책을 침범하지 않게 된다.

### 감수할 비용

- first playable 전에는 MCP 도입 속도를 의도적으로 늦춘다.
- 운영 문서와 실험 브랜치 관리가 필요하다.

## 후속

- `docs/05_setup/mcp-adoption-plan.md`와 `docs/05_setup/unity-mcp.md`를 계속 유지한다.
- main 브랜치 도입 전에는 tooling branch 검증을 선행한다.
