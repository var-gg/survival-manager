# 작업 계획

## 메타데이터

- 작업명: Session Realm Authority Boundary
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-06
- 의존:
  - `docs/03_architecture/session-realm-authority-and-offline-online-ports.md`
  - `docs/04_decisions/adr-0020-session-realm-authority-boundary.md`

## Preflight

- `SM.Meta`, `SM.Unity`, `SM.Persistence.Abstractions` 경계를 먼저 고정한다.
- `ISaveRepository` 직접 호출 지점을 root/adapter 밖으로 퍼뜨리지 않는다.
- Boot가 자동 Town 진입을 계속하지 않도록 bootstrap 흐름을 먼저 끊는다.
- current UI/test/scene contract를 읽고 최소 변경 지점만 잡는다.

## Phase 1 code-only

- realm/capability/DTO/port 계약 추가
- `SessionRealmCoordinator`, `OfflineLocalSessionAdapter` 추가
- `GameSessionRoot`, `GameBootstrap`, `SceneFlowController` 갱신
- Town/Reward presenter가 profile view/capability를 읽도록 이동
- Quick Battle / direct-scene auto-start 정책을 pure seam으로 분리

## Phase 2 asset authoring

- Boot scene contract를 realm 선택 버튼 기준으로 재정의
- Town / Reward UXML에 session/capability 상태와 arena gate 표면 추가
- installer / runtime binder 둘 다 Boot contract를 보장하도록 맞춘다

## Phase 3 validation

- `test-batch-fast`
- `tools/test-harness-lint.ps1`
- 가능하면 `test-batch-edit`
- `tools/docs-policy-check.ps1`
- `tools/docs-check.ps1`
- `tools/smoke-check.ps1`

## rollback / escape hatch

- online mock/server scope가 섞이면 현재 task를 중단하고 후속 task로 분리한다.
- Unity batch lock이 있으면 stale artifact로 통과 처리하지 않고 blocker로 남긴다.
- scene asset 재생성이 막히면 installer + runtime binder contract 둘 다 남기고 수동 repair 필요성을 보고한다.

## tool usage plan

- file-first로 코드/문서/scene installer를 먼저 고친다.
- Unity 관련 확인은 `tools/unity-bridge.ps1`를 우선 사용한다.
- bridge가 막히면 non-mutating read와 local build/lint로 우회 근거를 남긴다.

## loop budget

- compile-fix 허용 횟수: 3
- refresh/read-console 반복 허용 횟수: 3
- asset authoring 재시도 허용 횟수: 2
