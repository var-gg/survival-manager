# session service object extraction 상태

## 메타데이터

- 작업명: session service object extraction
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Current state

- 022 commit `3223b38` 이후 clean main에서 시작했다.
- `GameSessionState.cs`는 1,894줄이다.
- `GameSessionState`는 five flow service fields를 보유하고 public/internal entrypoint를 service object로 위임한다.
- `Assets/_Game/Scripts/Runtime/Unity/Session/SessionDeploymentFlow.cs`를 추가했다.
- 기존 partial helper 파일에 같은 이름의 `internal sealed` service object를 추가했다.
- behavior migration을 피하기 위해 기존 method body는 private `*Core` entrypoint로 보존했다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 |
| --- | --- | --- | --- |
| public surface | constructor/facade method 이름 유지 | 통과 | focused tests exit 0 |
| service object | 5개 internal sealed object | 통과 | `SessionProfileSync`, `SessionDeploymentFlow`, `SessionRecruitmentFlow`, `SessionExpeditionFlow`, `SessionRewardSettlementFlow` |
| line budget | `GameSessionState.cs` 2,500줄 이하 | 통과 | 2,048줄 |
| Meta guard | forbidden using 없음 | 통과 | source guard pass |
| tests | fast/focused pass | 통과 | fast 132 passed / 3 ignored, focused filters exit 0 |

## Handoff notes

- 이번 단계는 behavior-preserving extraction이다.
- 외부 UI/controller 호출부 migration은 만들지 않았다.
- 다음에 더 줄일 경우 private `*Core` body를 service object 내부로 옮기는 단계로 이어가면 된다.
