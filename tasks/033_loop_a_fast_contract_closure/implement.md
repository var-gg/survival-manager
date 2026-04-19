# Loop A fast contract closure implementation

## Phase 1 summary

- ignored `LoopA_4v4_BattleEndsBeforeTimeout`를 `LoopA_4v4_AsymmetricBattleEndsBeforeTimeout`로 대체했다.
- fixture는 4v4, front/back anchor, melee/ranged mix, seed `42`를 유지한다.
- enemy side만 HP/armor/attack profile을 낮춰 deterministic fast 종료 oracle로 만들었다.
- assertion은 Ally winner, 한쪽 전멸, `BattleSimulator.DefaultMaxSteps` 미도달을 함께 확인한다.

## Phase 2 summary

- asset authoring은 하지 않았다.
- Unity batch가 `BattleActor_PrimitiveWrapper.prefab`를 자동 재저장한 side effect는 작업 범위 밖이라 되돌렸다.

## deviation

- 첫 비대칭 fixture는 300 step에 적 1명이 남아 실패했다.
- production combat formula를 바꾸지 않고 fixture HP/armor/attack profile만 한 번 더 낮춰 focused test를 통과시켰다.

## blockers

- 없음.

## diagnostics

- ignored test inventory.
- focused replacement test result.
- full FastUnit result.

## why this loop happened

028에서 category closure를 진행하며 오래된 대칭 4v4 timeout assertion이 드러났고, 당시에는 balance retune 범위 밖이라 `Ignore`로 남았다. 033은 repo-wide production balance를 건드리지 않고 FastUnit이 놓치던 “다인전 종료 contract”만 deterministic fixture로 닫는다.
