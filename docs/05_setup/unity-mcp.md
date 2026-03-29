# Unity MCP 운영 가이드

- 상태: active
- 최종수정일: 2026-03-30
- 소유자: repository

## 목적

Unity MCP 또는 editor bridge 계열 도구의 목적은 **editor state 작업 가속**이다.
이 저장소에서 우선적으로 기대하는 가치는 아래와 같다.

- scene 조회
- game object / component 조회
- asset 검색과 참조 추적
- menu 실행
- console/log 확인

즉, Unity Editor 안에만 있는 상태를 더 빠르게 보고 좁은 범위에서 조작하기 위한 도구다.

## 범위 밖

다음은 Unity MCP의 역할이 아니다.

- 런타임 의존성
- production gameplay dependency
- main 브랜치 플레이 가능 상태를 MCP 전제 하에 설계하는 일
- 게임 로직을 editor bridge 없이는 유지할 수 없게 만드는 일

## 저장소 운영 전제

이 저장소는 **Discord 기반 외부 Codex 에이전트**를 사용한다.
따라서 Unity 관련 자동화는 아래 두 경로를 구분해서 다뤄야 한다.

### Unity Assistant 내부 전용 흐름

Unity가 제공하거나 Unity Editor 내부에서만 자연스럽게 쓰이는 assistant/gateway 흐름이다.
이 경로는 editor 내부 컨텍스트 접근성은 좋지만, 현재 저장소의 외부 Discord 운영 모델과는 별개로 평가해야 한다.

이 경로를 쓸 때 주의할 점:

- 외부 Codex handoff와 바로 연결된다고 가정하지 않는다.
- editor 내부 전용 UX에 잠겨 버리지 않는지 확인한다.
- repo-visible diff와 승인 절차를 유지한다.

### 외부 direct MCP 흐름

외부 Codex 에이전트가 직접 MCP 서버 또는 editor bridge endpoint와 통신하는 흐름이다.
현재 저장소 운영 모델에서는 이 경로가 더 중요하다.

이 경로를 쓸 때 원칙:

- read-heavy usage를 먼저 검증한다.
- write는 sandbox 또는 project-owned 범위로 제한한다.
- Discord 기반 handoff와 충돌하지 않아야 한다.
- hidden editor mutation보다 파일 diff가 우선이어야 한다.

## 운영 원칙

- main 브랜치 런타임은 MCP에 의존하지 않는다.
- MCP는 local dev / tooling acceleration 용도다.
- 대량 삭제를 금지한다.
- `Assets/ThirdParty` 수정 금지.
- 무승인 package import 금지.
- broad scene rewrite 금지.
- review 불가능한 hidden state 변경 금지.
- 가능하면 read -> report -> narrow write 순서를 지킨다.

## Unity CLI Hybrid Note

`unity-cli`는 MCP 대체재가 아니라 local optional fast lane이다.
운영 기준은 다음처럼 나눈다.

- CLI first: `status`, `list`, `menu`, `console`, `compile`, `test`, aggregate report
- MCP typed lane: scene/prefab/component/package 구조 조작, typed guardrail이 중요한 작업

즉, 이 저장소의 기본 형태는 **file-first + CLI-fast-lane + MCP-typed-lane**이다.
자세한 setup과 routing rules는 `docs/05_setup/unity-cli.md`, `prompts/unity-cli-hybrid-ops.md`를 따른다.

## 허용 가능한 작업 예시

### 읽기 중심

- 현재 열린 scene hierarchy 조회
- 특정 GameObject/component 존재 여부 확인
- prefab/asset 검색
- console 에러 수집
- menu command 실행 가능 여부 확인
- missing reference / import error 조사

### 좁은 쓰기 중심

- sandbox scene에 한정된 object 생성/수정
- `Assets/_Game` 아래 project-owned asset 수정
- 승인된 prefab reference 연결
- 명확한 범위의 menu-driven repair 실행

## 금지 또는 보류 작업

- scene 전체 재작성
- project-wide find/replace 성격의 editor mutation
- package 설치/갱신/삭제를 승인 없이 수행
- `Assets/ThirdParty` 내부 수정
- 근거 없는 대량 object/asset 삭제
- main 브랜치에서 실험성 bridge 의존 workflow 고착화

## 도입안 비교

### A안: 공식 Unity MCP / AI Gateway

추천 검토 포인트:

- Unity 버전 호환성
- vendor support와 유지보수 안정성
- Unity Assistant 내부 흐름과의 결합도
- 외부 direct MCP 연결 가능성
- 권한 분리와 감사 가능성

### B안: 커뮤니티 MCP(CoplayDev)

추천 검토 포인트:

- 외부 direct MCP 연결 속도
- read-heavy 실험 편의성
- 설치/제거 간단성
- 유지보수 지속성
- Unity 업그레이드 시 리스크

## 현재 기본안

현재 프로젝트에서는 다음을 기본안으로 둔다.

- **first playable 이후 tooling branch에서 먼저 평가한다.**

이 말은 곧 다음을 뜻한다.

- 지금 당장 main 브랜치 표준 도구로 채택하지 않는다.
- first playable 이전에는 정책만 고정한다.
- 실제 도입 평가는 `tooling/*` 브랜치에서 시작한다.

## 추천 평가 순서

1. 공식 Unity MCP / AI Gateway의 외부 direct MCP 적합성 확인
2. 맞지 않으면 커뮤니티 MCP(CoplayDev)로 read-heavy 실험
3. 둘 다 write는 sandbox 범위에서만 시험
4. rollback과 diff visibility가 확보될 때만 확대 검토

## 결론

Unity MCP는 도입 가능성이 있는 **개발 가속 도구**이지만,
이 프로젝트의 런타임/프로덕션 경계 안으로 들어오면 안 된다.

정리하면:

- MCP는 editor state 가속용
- main 런타임 비의존
- first playable 이후 tooling branch 선평가
- Discord 외부 Codex direct flow와 Unity Assistant 내부 flow를 분리 평가
