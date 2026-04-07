# 작업 상태

## 메타데이터

- 작업명: Loop B Recruitment / Retrain / Economy Closure
- 담당: Codex
- 상태: 완료
- 최종수정일: 2026-04-07

## Current state

- `Gold / Echo` 이원화와 recruit/retrain/scout/dismiss/duplicate path 정리가 완료됐다.
- recruit offer는 `4-slot pack + on-plan/protected/scout/pity` contract로 런타임과 테스트에서 강제된다.
- retrain은 flex-only, current/previous exclusion, native coherence, retrain pity를 보장한다.
- editor-side `Recruitment Sandbox`와 Loop B source-of-truth 문서가 추가됐다.
- 2026-04-07 follow-up으로 buildcraft closure를 merge했다.
- permanent augment는 `unlock many, equip one` 1-slot thesis로 정규화됐다.
- temp augment 첫 선택은 same-family permanent candidate unlock으로 연결된다.
- passive board selection은 prerequisite / exclusion / keystone / node cap validator를 통과해야 한다.
- Town / Reward normal playable lane은 scout / retrain / refit / passive / permanent build management와 build identity summary를 노출한다.
- legacy reward/currency residue는 normal lane에서 parked 처리됐다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 |
| --- | --- | --- | --- |
| compile | Loop B contract 추가 후 compile green | 완료 | `pwsh -File tools/unity-bridge.ps1 compile` |
| editmode | recruit/retrain/duplicate/dismiss deterministic oracle | 완료 | `pwsh -File tools/unity-bridge.ps1 test-edit` -> `78 passed` |
| playmode | Town/Expedition/Reward smoke 유지 | 완료 | `pwsh -File tools/unity-bridge.ps1 test-play` -> `2 passed` |
| validator | recruit pool / native coherence / banned pairing drift 차단 | 완료 | `LoopBContractValidator`, `ContentDefinitionValidator` |
| harness | pack/retrain/duplicate/dismiss 수동 재현 | 완료 | `SM/Authoring/Recruitment Sandbox` |
| docs | Loop B source-of-truth 문서와 task packet 동기화 | 완료 | 본 task packet + 신규 docs |

## Evidence

- 핵심 코드:
  - `Assets/_Game/Scripts/Runtime/Meta/Services/RecruitPackGenerator.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/RetrainService.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/DuplicateResolver.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/DismissService.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/GameSessionState.cs`
  - `Assets/_Game/Scripts/Editor/Validation/LoopBContractValidator.cs`
  - `Assets/_Game/Scripts/Editor/Authoring/RecruitmentSandbox/RecruitmentSandboxWindow.cs`
- 핵심 문서:
  - `docs/02_design/meta/recruitment-contract.md`
  - `docs/02_design/meta/retrain-contract.md`
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/02_design/meta/duplicate-handling-contract.md`
  - `docs/03_architecture/recruit-offer-schema.md`
  - `docs/03_architecture/unit-economy-schema.md`
  - `docs/05_setup/recruitment-and-retrain-harness.md`
- 실행 명령:
  - `pwsh -File tools/unity-bridge.ps1 compile`
  - `pwsh -File tools/unity-bridge.ps1 test-edit`
  - `pwsh -File tools/unity-bridge.ps1 test-play`

## Closure note

1. duplicate를 second copy가 아니라 Echo conversion으로 닫은 이유는 roster identity를 고정한 채 0가치 결과를 없애기 위해서다.
2. retrain이 Gold가 아니라 Echo를 쓰는 이유는 외부 파워 획득과 RNG 복구를 경쟁시키지 않기 위해서다.
3. recruit offer에 on-plan slot을 구조적으로 강제한 이유는 하이롤을 유지하면서도 low-roll 복구 경로를 pack 단위에서 보장하기 위해서다.
4. scout를 exact unit targeting이 아니라 directional bias로 제한한 이유는 조립감은 살리되 deterministic shopping으로 무너지지 않게 하기 위해서다.

## Remaining blockers

- `docs-check`는 `.markdownlint-cli2.jsonc` 추가와 잔여 포맷 정리 후 green이다.
- fresh `test-batch-fast`는 다른 Unity 인스턴스 project lock 때문에 이번 턴에서 재실행 확인을 못 했다.
- legacy crafting currency 문서군은 normal-lane park 선언 이후에도 후속 통합 정리가 더 필요할 수 있다.

## Deferred / debug-only

- craft currency 완전 제거
- duplicate 이후 확장 progression
- sandbox UI polish와 richer filtering
- separate permanent inventory widget polish
- passive unlock progression currency / XP

## Loop budget consumed

- runtime/schema 확장
- validator/test closure
- UI/bootstrap/harness closure
- docs/task packet sync

## Handoff notes

- commit 전 untracked test temp scene과 `Saves/` 같은 개인/임시 산출물은 제외한다.
- sample content tag asset이 빈 shell로 생기면 regenerate보다 삭제 후 canonical seed/fallback을 우선 확인한다.
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`는 기본 검증이지만, project lock이 있으면 다른 Unity 인스턴스를 먼저 정리해야 한다.
