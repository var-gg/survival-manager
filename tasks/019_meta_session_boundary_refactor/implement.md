# SM.Meta/Session boundary refactor 구현

## 메타데이터

- 작업명: SM.Meta/Session boundary refactor
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19
- 실행범위:
  - `Assets/_Game/Scripts/Runtime/**`
  - `Assets/Tests/**`
  - `docs/**`
  - `tasks/019_meta_session_boundary_refactor/**`

## Phase log

- Phase 0 preflight:
  - `SM.Meta`의 `SM.Content` 직접 참조와 engine reference 허용 상태를 확인했다.
  - `SM.Tests.PlayMode`의 `SM.Editor` 참조를 확인했다.
  - `GameSessionState.cs`가 line budget을 넘는 session 중심 파일임을 확인했다.
- Phase 1 code-only:
  - content schema enum을 `SM.Core.Content`로 이동했다.
  - `SM.Meta` narrative/reward/loot/passive 경계가 pure model/spec를 받도록 갱신했다.
  - ScriptableObject-to-runtime 변환을 `SM.Unity.ContentConversion.NarrativeRuntimeContentAdapter`에 배치했다.
  - `GameSessionState` 본 파일을 partial facade로 낮추고 session 흐름 파일을 `Assets/_Game/Scripts/Runtime/Unity/Session/`에 분리했다.
- Phase 2 asset authoring:
  - 수행하지 않았다.
- Phase 3 validation:
  - asmdef/source/line guard를 `BuildCompileAuditTests`에 추가했다.
  - FastUnit batch와 harness lint를 실행했다.

## deviation

- `GameSessionState` 분해는 public facade 보존을 우선하여 partial session flow 파일 분리로 닫았다.
- save/domain `*Record` rename은 sprint 범위에서 제외했다.

## blockers

- 없음.

## diagnostics

- 첫 direct compile에서 `PermanentAugmentProgressionTests`, `PassiveBoardSelectionValidatorTests`의 `ModifierSource`/`StatModifier` using 누락을 확인하고 `SM.Core.Contracts`, `SM.Core.Stats` using을 보강했다.
- 이후 direct batch compile은 exit 0이다.
- `test-batch-fast`는 failed 0으로 완료했다.

## why this loop happened

- content schema enum 이동 후 일부 focused test가 기존 implicit namespace에 의존하고 있었다.
- 새 guard는 이 drift를 compile 전에 더 빨리 드러내기 위해 asmdef/source/line budget을 함께 검사한다.

## 기록 규칙

- 세부 compile 로그는 `Logs/compile-refactor.log`와 `TestResults-Batch.xml`에 남긴다.
- 구조 결정은 `docs/04_decisions/adr-0023-meta-content-adapter-boundary.md`를 따른다.
