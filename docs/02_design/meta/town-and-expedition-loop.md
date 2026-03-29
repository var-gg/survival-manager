# Town and Expedition Loop

- 상태: active
- 최종수정일: 2026-03-29
- phase: prototype

## 현재 MVP loop

1. Town에서 roster / recruit / squad / deploy를 본다.
2. 필요하면 recruit / reroll / save / load를 누른다.
3. `Debug Start`로 Expedition으로 간다.
4. Expedition에서 5노드 box track과 현재 노드를 본다.
5. `Next Battle` 또는 `Return Town`을 선택한다.
6. Battle / Reward를 거쳐 Town으로 돌아온다.

## Quick Battle smoke

- Town `Quick Battle`은 Expedition을 건너뛰고 바로 Battle observer smoke를 연다.
- 이 경로는 Expedition 진행도를 건드리지 않는다.

## 비목표

- squad 편성 drag-and-drop
- 실제 branching 선택 로직
- town facility 운영
