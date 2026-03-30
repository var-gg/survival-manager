# MCP 도입 계획

- 상태: draft
- 최종수정일: 2026-03-29
- 소유자: repository

## 목적

이 문서는 Survival Manager 저장소에서 Unity MCP(Editor bridge 계열 포함)를 도입할 때의 평가 순서와 운영 범위를 고정한다.
도입 목적은 **Unity editor state 작업 가속**이다.

우선 대상은 아래와 같다.

- scene 상태 확인
- game object / component 조회 및 제한적 수정
- asset 검색 및 참조 추적
- menu 실행
- console/log 확인

핵심 의도는 Discord 기반 외부 Codex 에이전트가 Unity editor 내부 상태를 더 빠르게 파악하도록 돕는 것이다.

## 범위 밖

다음은 MCP 도입 목적에 포함하지 않는다.

- 런타임 의존성 추가
- production gameplay dependency 형성
- main 브랜치 런타임 기능이 MCP 없이는 동작하지 않도록 만드는 일
- 플레이어 빌드/배포 결과가 MCP 패키지나 서비스에 의존하도록 만드는 일

즉 MCP는 **개발 툴링 가속 계층**이지, 게임 런타임의 일부가 아니다.

## 운영 원칙

- main 브랜치 런타임은 MCP에 의존하지 않는다.
- MCP는 local dev / tooling acceleration 용도로만 사용한다.
- 대량 삭제를 금지한다.
- `Assets/ThirdParty` 수정은 금지한다.
- 명시적 승인 없는 package import는 금지한다.
- editor bridge를 쓰더라도 저장소 diff로 검토 가능한 변경만 허용한다.
- risky write는 sandbox 또는 tooling branch에서 먼저 검증한다.

## 흐름 구분

이 저장소는 **Discord 기반 외부 Codex 에이전트**를 사용한다.
따라서 Unity MCP 흐름은 아래 두 가지를 분리해서 본다.

### 1. Unity Assistant 내부 전용 흐름

Unity Editor 내부 Assistant 또는 Unity가 직접 제공하는 에이전트/게이트웨이를 통해 editor state를 다루는 흐름이다.
이 흐름은 editor 내부 컨텍스트 접근성이 좋을 수 있지만, 외부 Discord 운영 흐름과 직접 동일시하면 안 된다.

특징:

- editor 내부 UX와 결합되기 쉽다
- Unity 버전 및 vendor 정책 변화 영향을 크게 받는다
- 외부 Codex 운영 모델과 직접 호환되지 않을 수 있다

### 2. 외부 direct MCP 흐름

Discord 기반 외부 Codex 에이전트가 직접 MCP 서버 또는 editor bridge endpoint에 붙는 흐름이다.
이 저장소의 운영 관점에서 더 중요한 평가 대상은 이 경로다.

특징:

- 현재 운영 모델과 더 직접적으로 연결된다
- Discord handoff / 외부 agent orchestration과 궁합을 따지기 쉽다
- read/write 범위, 승인 절차, diff 가시성을 더 명확히 강제해야 한다

## 도입안 비교

### A안: 공식 Unity MCP / AI Gateway 평가

장점:

- vendor 정렬성이 높다
- Unity 버전 호환성과 향후 지원 기대치가 상대적으로 높다
- editor 내부 capability와 연결될 가능성이 크다

단점:

- Discord 기반 외부 Codex direct flow에 바로 맞지 않을 수 있다
- Unity Assistant 내부 전용 흐름으로 잠길 가능성을 평가해야 한다
- 도입 시 계정/게이트웨이/정책 경로가 더 무거울 수 있다

### B안: 커뮤니티 MCP(CoplayDev) 도입

장점:

- 외부 direct MCP 흐름 실험이 빠르다
- 로컬 개발용 read/write acceleration 검증이 쉽다
- Discord 기반 외부 Codex 에이전트와 연결 가설을 빨리 검증할 수 있다

단점:

- 유지보수/신뢰도/버전 추적 리스크가 공식안보다 크다
- Unity 업그레이드 시 호환성 관리 비용이 커질 수 있다
- project governance를 강하게 걸지 않으면 범위가 쉽게 퍼진다

## 기본안

현재 프로젝트의 기본안은 다음과 같다.

- **first playable 이후**
- **tooling branch에서 먼저 평가**
- main 브랜치에는 즉시 상주시키지 않음

즉, 도입 자체를 부정하지는 않되,
실험은 first playable을 넘긴 뒤 별도 tooling branch에서 수행한다.

## 평가 단계

1. first playable 완료 전까지는 정책 문서만 유지한다.
2. first playable 이후 `tooling/*` 브랜치에서 read-heavy 시나리오를 먼저 검증한다.
3. scene/gameobject/asset/menu/console 중심의 editor state 가속 가치가 실제로 있는지 확인한다.
4. write 범위는 project-owned asset과 sandbox scene에 한정해 시험한다.
5. rollback, diff visibility, package provenance를 검토한 뒤 main 브랜치 반영 여부를 다시 결정한다.

## 승인 기준

다음 조건을 만족해야 다음 단계로 넘어간다.

- 외부 direct MCP 흐름과 실제 운영 모델이 맞아야 한다.
- hidden state change보다 repo-visible diff가 우세해야 한다.
- package/source provenance가 확인되어야 한다.
- 대량 변경 방지 장치가 있어야 한다.
- 제거/비활성화가 쉬워야 한다.

## 비승인 항목

이 문서는 다음을 승인하지 않는다.

- 즉시 main 브랜치 도입
- production dependency 형성
- 승인 없는 package 설치
- `Assets/ThirdParty` 변경
- 대량 scene/object rewrite

## 관련 문서

- `docs/05_setup/unity-mcp.md`
- `docs/04_decisions/adr-0011-mcp-adoption-policy.md`
- `docs/00_governance/agent-operating-model.md`
