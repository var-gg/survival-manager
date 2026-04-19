# GameSessionState phase 2 ownership migration plan

## Preflight

- `git status --short --branch`로 clean main 확인.
- `rg "_session\..*Core\(" Assets/_Game/Scripts/Runtime/Unity/Session`로 remaining delegation inventory 확인.
- `GameSessionState.cs` line count와 `*Core` inventory baseline 기록.
- structure guard 관점에서 asmdef, public API, scene/asset touch 금지 여부 확인.

## Phase 1 code-only

- `SessionProfileSync`에 profile/narrative/current scene/debug snapshot 흐름의 실제 body를 옮긴다.
- `SessionExpeditionFlow`에 expedition/quick battle/campaign/node selection 흐름 중 안전한 body를 옮긴다.
- `SessionRewardSettlementFlow`에 battle audit/result/reward choice preview/apply 흐름의 실제 body를 옮긴다.
- `GameSessionState` 본 파일에는 public facade, fields/properties, composition, 그리고 아직 옮기지 않은 helper만 남긴다.
- `BuildBoundaryGuardFastTests`에 session service asset-loading token guard와 `_session.*Core(...)` delegation budget을 추가한다.

## Phase 2 asset authoring

없음. Unity batch run이 scene/prefab/asset을 자동 touch하면 task 범위 밖 side effect로 복구한다.

## Phase 3 validation

- 기본 fast lane과 focused session tests를 실행한다.
- lint와 fast boundary guard로 경계 회귀를 확인한다.
- 문서/task status 변경 후 docs policy/check/smoke를 실행한다.

## rollback / escape hatch

- public facade 변경이 필요해지는 body는 이번 sprint에서 옮기지 않는다.
- helper visibility 때문에 broad public surface가 필요해지면 이동을 중단하고 residual inventory에 남긴다.
- behavior가 흔들리는 흐름은 service object에서 `_session` private helper를 호출하는 얇은 migration으로 제한한다.
- guard가 false positive를 만들면 broad regex를 늘리지 않고 narrow allowlist나 deferred `030`으로 넘긴다.

## tool usage plan

- 파일 수정은 `apply_patch`를 사용한다.
- Unity batch 명령은 순차 실행한다.
- task status는 validation evidence를 기준으로 갱신한다.

## loop budget

- compile/test retry: 2회
- behavior regression fix: 2회
- docs-check retry: 1회
