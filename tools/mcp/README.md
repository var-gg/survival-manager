# tools/mcp

이 디렉터리는 Survival Manager에서 커뮤니티 MCP를 **로컬 tooling 전용**으로 준비할 때 참고할 문서/샘플을 두는 위치다.

## 원칙

- 런타임 코드와 섞지 않는다.
- repo에 비밀값/개인 설정을 커밋하지 않는다.
- main 브랜치 런타임은 MCP에 의존하지 않는다.
- 실제 연결은 sandbox/tooling 브랜치에서 먼저 검증한다.

## 여기에 둘 수 있는 것

- 설치 메모
- 샘플 config 템플릿(`*.example`)
- 검증 시나리오 메모
- 로컬 실행 절차 문서

현재 포함:

- `coplay-client-config.example.json`: HTTP localhost 연결 예시 (`http://localhost:43157/mcp`)

## 여기에 두면 안 되는 것

- 실제 토큰
- 개인 endpoint
- machine-specific secret config
- player/runtime dependency 파일

## 관련 문서

- `docs/05_setup/community-mcp-setup.md`
- `docs/05_setup/mcp-safety-rules.md`
- `docs/06_production/mcp-usage-checklist.md`
