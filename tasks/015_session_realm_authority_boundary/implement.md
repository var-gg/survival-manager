# 구현 메모

## Phase 1 code-only

- `SM.Meta`에 realm/capability/DTO/port 계약을 추가했다.
- `SM.Unity`에 `SessionRealmCoordinator`, `OfflineLocalSessionAdapter`, `SessionRealmAutoStartPolicy`를 추가했다.
- `GameSessionRoot`는 active realm과 capability, query/command port를 노출하고 `ISaveRepository` 직접 사용을 끊었다.
- `GameBootstrap`는 Boot 대기 흐름으로 바꾸고 Quick Battle만 offline auto-start를 유지한다.

## Phase 2 asset/UI

- Boot는 uGUI realm 선택 화면 계약을 갖도록 installer/runtime binder를 둘 다 갱신했다.
- Town는 realm/capability/arena availability와 Session Menu 버튼을 추가했다.
- Reward는 local-only reward 경고를 명시적으로 표시하도록 상태 문구를 바꿨다.

## Phase 3 validation

- `test-batch-fast`는 Unity project lock 때문에 fresh run을 회수하지 못했고 기존 결과 xml만 반환했다.
- `tools/test-harness-lint.ps1`는 green이었다.
- `tools/docs-policy-check.ps1`와 `tools/smoke-check.ps1`는 green이었다.
- `tools/docs-check.ps1`는 이번 변경과 무관한 저장소 전반 markdownlint 이슈 때문에 실패했다.
- local `MSBuild.exe`로 solution build를 호출해 syntax/assembly compile 오류는 확인했다.

## deviations

- Unity bridge `bootstrap`는 port 8090 timeout으로 실패했다.
- scene asset canonical save는 현재 열린 Unity 인스턴스 때문에 직접 재생성하지 못할 가능성을 고려해 runtime binder도 동일 contract를 보장하게 했다.

## blockers

- fresh `test-batch-edit` / bootstrap 증거는 Unity project lock이 풀려야 안정적으로 다시 회수할 수 있다.

## diagnostics

- stale `TestResults-Batch.xml`만 반환될 수 있어서 batch 결과는 lock 경고와 함께 해석해야 한다.
