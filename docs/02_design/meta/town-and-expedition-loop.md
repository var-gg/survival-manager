# Town and Expedition Loop

- 상태: active
- 최종수정일: 2026-03-30
- 단계: prototype

## 현재 MVP loop

1. Town에서 roster / recruit / squad / deploy를 본다.
2. 필요하면 recruit / reroll / save / load를 누른다.
3. `Debug Start`로 Expedition으로 간다.
4. Expedition에서 5노드 box track과 현재 노드, 선택 가능한 분기를 본다.
5. route 버튼으로 다음 node를 고른다.
6. `Next Battle` 또는 safe advance를 진행한다.
7. Battle / Reward를 거쳐 Town으로 돌아오고, 진행 중이면 `Debug Start`로 원정을 재개할 수 있다.

## Quick Battle smoke

- Town `Quick Battle`은 Expedition을 건너뛰고 바로 Battle observer smoke를 연다.
- 이 경로는 Expedition 진행도를 건드리지 않는다.

## 현재 구현 메모

- 현재 branching graph는 `camp -> ambush/relay -> shrine -> extract` 고정형이다.
- node effect는 gold / trait reroll / temporary augment로 실제 적용된다.
- extract는 safe advance 노드라 전투 없이 정리 가능하다.

## 비목표

- squad 편성 drag-and-drop
- procedural expedition graph
- town facility 운영
