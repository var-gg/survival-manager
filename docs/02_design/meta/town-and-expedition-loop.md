# Town and Expedition Loop

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-09
- 소스오브트루스: `docs/02_design/meta/town-and-expedition-loop.md`
- 관련문서:
  - `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`
  - `docs/02_design/meta/drop-table-rarity-bracket-and-source-matrix.md`
  - `docs/03_architecture/encounter-authoring-and-runtime-resolution.md`

## 현재 MVP loop

1. Town에서 roster / recruit / squad / deploy를 본다.
2. 필요하면 recruit / reroll / chapter / site 선택을 조정한다.
3. `Start Expedition` 또는 `Resume Expedition`으로 Expedition으로 간다.
4. Expedition에서 현재 `chapter -> site -> 5-node track`을 본다.
5. site track은 `skirmish -> skirmish -> elite -> boss -> extract` 순서로 진행한다.
6. battle 결과 뒤에는 항상 Reward를 거쳐 Town으로 돌아온다.
7. 진행 중이면 Town에서 `Resume Expedition`으로 다시 해당 site track을 재개할 수 있다.
8. extract는 전투가 아닌 settlement node지만 반드시 `Reward -> Town(close)`를 거친다.

## Quick Battle smoke

- Town의 `Quick Battle (Smoke)`와 `SM/전투테스트`는 Expedition을 건너뛰고 Battle observer smoke를 연다.
- 이 경로는 Expedition 진행도를 건드리지 않는다.
- smoke lane의 direct `Return Town`과 `Re-battle`은 normal lane 계약에 포함하지 않는다.

## 현재 구현 메모

- story progression은 authored `CampaignChapterDefinition` / `ExpeditionSiteDefinition`을 사용한다.
- node context는 `ChapterId`, `SiteId`, `SiteNodeIndex`, `EncounterId`, `BattleSeed`, `BattleContextHash`를 저장한다.
- extract는 전투가 아닌 정산 노드다.
- active run 중 Town에서는 chapter/site 선택만 잠기고, recruit/roster/deploy/posture 조정은 계속 허용한다.
- story clear 뒤에만 endless가 열린다.

## 비목표

- squad 편성 drag-and-drop
- procedural expedition graph
- town facility 운영
