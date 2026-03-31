# Assembly boundaries and persistence ownership

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
- 관련문서:
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/replay-persistence-and-run-audit.md`
  - `docs/03_architecture/persistence-strategy.md`
  - `docs/03_architecture/unity-agent-harness-contract.md`

## 목적

이 문서는 `SM.Meta`, `SM.Unity`, `SM.Persistence.Abstractions` 사이의 책임과 금지 의존을 Unity task preflight 수준으로 고정한다.
핵심은 feature 구현 중간에 asmdef cycle이나 persistence ownership drift를 발견하지 않게 만드는 것이다.

## 책임 정의

| 레이어 | 책임 | 하지 말아야 할 일 |
| --- | --- | --- |
| `SM.Meta` | town, expedition, encounter, reward, progression, arena 같은 비즈니스 상태 변화 | persistence record 타입 참조, Unity scene/component 타입 참조, repository concrete 생성 |
| `SM.Unity` | Boot, scene flow, session orchestration, runtime composition root, UI/controller 진입점 | editor-only logic 소유, persistence ownership을 Meta로 밀어 넣는 일 |
| `SM.Persistence.Abstractions` | save contract, record 모델, repository port, persistence-facing DTO | gameplay 규칙 결정, Unity scene 진입점 소유, concrete serializer 구현 |

## forbidden dependency examples

- `SM.Meta` 서비스가 `ArenaSeasonStateRecord` 같은 persistence record를 직접 참조하는 것
- `SM.Meta`가 save repository를 직접 new 하거나 adapter 세부를 아는 것
- `SM.Persistence.Abstractions`가 `GameSessionState`, `BattleScreenController` 같은 Unity 진입점을 아는 것
- `SM.Unity`가 editor bootstrap과 validation 구현을 runtime asmdef 안에 끌어오는 것
- 순환을 피하려고 record와 domain 모델을 한 asmdef로 합쳐 버리는 것

## persistence ownership 원칙

- 비즈니스 truth는 `SM.Meta` 또는 그 하위 domain model이 소유한다.
- 저장 계약과 record shape는 `SM.Persistence.Abstractions`가 소유한다.
- composition root와 persistence 포트 연결은 `SM.Unity`가 맡는다.
- domain state를 persistence record로 바꾸는 translation은 persistence boundary에서 수행한다.
- feature sprint 안에서 domain model과 persistence record를 동시에 뒤섞지 않는다.

## asmdef cycle 사전 점검 규칙

preflight에서 아래를 먼저 적는다.

1. 영향을 받는 asmdef 목록
2. 새로 필요한 참조가 허용 의존인지
3. 순환 가능성이 있는 참조쌍
4. shared contract를 낮은 계층으로 내려야 하는지 여부

구현 중간에 cycle 의심이 생기면 feature sprint를 멈추고 refactor sprint로 분리한다.

## service responsibility 재절단 기록 규칙

서비스 책임을 다시 자를 때는 단순 리팩터링으로 취급하지 않는다.
`status.md`에 최소 아래를 남긴다.

- 어떤 서비스 책임이 어디서 어디로 이동했는가
- 이유가 asmdef cycle인지, persistence ownership drift인지, runtime entry 문제인지
- 영향을 받는 asmdef와 port
- 이번 sprint에서 닫지 않고 deferred로 넘긴 항목

## current repo 적용 메모

- `ArenaSimulationService` 계열 작업은 async scaffold와 persistence record 변경을 한 번에 묶지 않는다.
- `GameSessionState`, `BattleScreenController`, `RunStateService`가 persistence truth를 직접 소유하지 않도록 경계를 유지한다.
- `SM.Meta -> SM.Persistence.Abstractions` 참조가 필요해 보이면 먼저 boundary 문서와 `dependency-direction.md`를 다시 확인한다.
