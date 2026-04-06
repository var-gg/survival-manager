# 작업 명세

## 메타데이터

- 작업명: Session Realm Authority Boundary
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-06
- 관련경로:
  - `Assets/_Game/Scripts/Runtime/Meta/**`
  - `Assets/_Game/Scripts/Runtime/Unity/**`
  - `Assets/_Game/UI/Screens/**`
  - `Assets/Tests/EditMode/**`
  - `docs/02_design/meta/**`
  - `docs/03_architecture/**`
  - `docs/04_decisions/**`
- 관련문서:
  - `docs/02_design/meta/session-realm-and-official-online-boundary.md`
  - `docs/03_architecture/session-realm-authority-and-offline-online-ports.md`
  - `docs/04_decisions/adr-0020-session-realm-authority-boundary.md`

## Goal

- `OfflineLocal`과 `OnlineAuthoritative`를 session realm으로 고정하고, offline-first authority seam을 코드 구조와 UI에 반영한다.

## Authoritative boundary

- 이번 task는 `session realm / authority seam` 한 축만 닫는다.
- source-of-truth는 `GameSessionRoot -> SessionRealmCoordinator -> OfflineLocalSessionAdapter`로 재절단한다.
- 실제 서버 연동, online mock, live PvP settlement는 이번 task에서 닫지 않는다.

## In scope

- realm/capability/query/command 포트 도입
- offline adapter/coordinator 도입
- Boot realm 선택 UX
- Town/Reward capability 표시
- scene/runtime contract와 targeted test 보강
- 관련 design/architecture/ADR/task 문서 갱신

## Out of scope

- `OnlineMockAdapter`
- auth gateway / platform login
- server DB / `/Server/**`
- `SaveProfile` aggregate 분해
- official arena/reward settlement 구현

## asmdef impact

- 영향 asmdef:
  - `SM.Meta`
  - `SM.Unity`
  - `SM.Tests.EditMode`
- 새 asmdef는 추가하지 않는다.
- `SM.Meta`는 계약만, `SM.Unity`는 adapter/composition root만 가진다.
- `SM.Meta -> SM.Persistence.*` 새 참조는 만들지 않는다.

## persistence impact

- `ISaveRepository` 사용 범위를 `OfflineLocalSessionAdapter`로 제한한다.
- `SaveProfile` 자체 분해는 이번 task에서 하지 않는다.
- online authoritative port는 repository가 아니라 query/command 계약으로 먼저 연다.

## validator / test oracle

- `FastUnit`: realm capability mapping, unsupported official commands, auto-start policy
- `EditMode`: Boot/Town contract
- lint: `tools/test-harness-lint.ps1`
- docs: `tools/docs-policy-check.ps1`, `tools/docs-check.ps1`, `tools/smoke-check.ps1`

## done definition

- realm/capability/query/command seam이 code/docs/UI에 반영됐다.
- Boot에서 realm 선택 대기, Town/Reward에서 capability 상태 표시가 가능하다.
- offline 경로가 existing local-first playable을 유지한다.
- evidence는 `tasks/015_session_realm_authority_boundary/status.md`에 남긴다.

## deferred

- online adapter 구현
- live PvP / leaderboard / reward delivery
- `SaveProfile` concern 분해
