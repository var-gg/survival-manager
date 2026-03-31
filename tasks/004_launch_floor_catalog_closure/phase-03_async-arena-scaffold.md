# Phase 03: async arena scaffold

- 상태: blocked
- 최종수정일: 2026-03-31
- parent task: `tasks/004_launch_floor_catalog_closure/status.md`

## authoritative axis

- async arena scaffold의 runtime/service 경계만 닫는다.
- persistence ownership 변경은 이 phase의 preflight에서 명시적으로 검토하고, scope를 넘기면 별도 refactor sprint로 분리한다.

## in scope

- `ArenaSimulationService`
- `ArenaModels`, `CampaignProgressModels`
- arena 관련 persistence record 영향 검토
- arena path에 필요한 최소 runtime entry 검토

## out of scope

- encounter/status/drop authored path
- skill/support tag contract
- broad PvP / live ops scope

## preflight

- `SM.Meta`, `SM.Unity`, `SM.Persistence.Abstractions` 책임을 먼저 재확인
- asmdef cycle 위험과 persistence ownership drift를 먼저 적는다
- 현재 phase의 targeted test와 runtime smoke가 없는 상태면 구현을 시작하지 않는다

## code-only closure

- arena scaffold는 service boundary와 model shape까지만 code-only로 닫는다.
- persistence record를 직접 참조하는 Meta service가 생기면 즉시 중단하고 refactor sprint로 분리한다.

## asset authoring closure

- 기본적으로 없음
- arena sample content나 menu bootstrap이 필요해지면 별도 child task로 분리한다

## validator / test oracle

- validator: 아직 정의 필요
- targeted tests: 아직 정의 필요
- runtime smoke: arena path 또는 equivalent debug path 정의 필요

## done signal

- asmdef/persistence preflight 통과
- compile green
- oracle 정의 완료 후 validator/test/smoke evidence 확보

## current blockers

- persistence ownership과 feature scaffold가 한 축으로 섞일 위험이 크다.
- 현재는 oracle보다 구조 위험이 먼저라서 blocked 상태로 둔다.
