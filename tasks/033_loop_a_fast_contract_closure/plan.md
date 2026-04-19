# Loop A fast contract closure plan

## Preflight

- `BattleResolutionTests.cs`, `CombatTestFactory.cs`, `BattleSimulator.cs`를 확인한다.
- ignored test가 dependency leak가 아니라 fast behavior contract hole인지 확인한다.
- 작업트리와 Unity batch side effect를 확인한다.

## Phase 1 code-only

- `LoopA_4v4_BattleEndsBeforeTimeout`의 `[Ignore]`를 제거한다.
- 완전 대칭 fixture를 비대칭 deterministic 4v4 fixture로 바꾼다.
- winner, side wipe, max step 미도달을 함께 assert한다.

## Phase 2 asset authoring

- 없음. authored asset, scene, prefab, ScriptableObject는 변경하지 않는다.
- Unity batch가 prefab을 자동 재저장하면 task 범위 밖 side effect로 되돌린다.

## Phase 3 validation

- focused replacement test를 먼저 실행한다.
- `test-batch-fast`와 `test-harness-lint`를 순차 실행한다.
- 문서 변경이 있으므로 docs policy/check/smoke를 실행한다.

## rollback / escape hatch

- 비대칭 fixture가 여전히 timeout이면 production 수치 대신 fixture HP/armor/attack profile만 조정한다.
- fixture가 너무 좁아져 다인전 coverage 의미가 사라지면 4v4 anchor/range mix는 유지한다.
- focused test가 flake이면 seed와 expected winner를 고정하고 task status에 loop를 기록한다.

## tool usage plan

- 파일 수정은 `apply_patch`로 수행한다.
- Unity 검증은 `tools/unity-bridge.ps1` batch verbs를 사용한다.
- 두 batchmode 테스트는 동시에 실행하지 않는다.

## loop budget

- fixture retune retry: 2회
- validation retry: 2회
- docs-check retry: 1회
