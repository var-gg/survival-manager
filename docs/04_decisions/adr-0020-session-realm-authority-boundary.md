# ADR-0020: session realm authority boundary 채택

- 상태: accepted
- 날짜: 2026-04-06

## 컨텍스트

현재 prototype은 `GameSessionState`와 `SaveProfile`을 중심으로 local-first 흐름을 빠르게 검증하는 구조다.
이 구조는 vertical slice에는 유리하지만, 이후 official server/PvP/reward를 붙일 때 online/offline 경계를
단순 persistence backend 선택으로 보면 authority가 쉽게 섞일 위험이 있다.

특히 아래 문제가 있었다.

- `ISaveRepository.LoadOrCreate/Save`가 full profile 저장 패턴을 전제한다.
- Boot가 곧바로 Town으로 들어가서 realm 선택 seam이 없다.
- UI가 `SaveProfile` 내부 shape에 직접 기대는 부분이 남아 있다.
- future server/mock port가 없어 online authoritative 진입점을 미리 고정하기 어렵다.

## 결정

- `OfflineLocal`과 `OnlineAuthoritative`를 서로 다른 session realm으로 취급한다.
- `OfflineLocal`은 local save truth와 비공식 progression을 가진다.
- `OnlineAuthoritative`는 server truth와 공식 progression/reward/PvP/rating을 가진다.
- 현재 slice에서는 `OfflineLocal`만 실제 동작하고, `OnlineAuthoritative`는 타입/문서에만 남기며 current playable UI에는 노출하지 않는다.
- realm/capability/query/command 계약은 `SM.Meta`에 두고, offline adapter와 bootstrap orchestration은 `SM.Unity`에 둔다.
- `ISaveRepository` full save는 `OfflineLocalSessionAdapter` 안으로만 가둔다.
- official mutation 확장 seam은 repository-first가 아니라 command/query-first로 연다.

## 결과

### 장점

- future server/mock adapter를 추가해도 현재 local vertical slice를 깨지 않는다.
- Boot에서 realm 선택을 강제해 mid-run authority drift를 막는다.
- future official/local 경계를 타입과 문서에서 유지한 채 current playable UI를 더 단순하게 유지할 수 있다.
- `SaveProfile`를 future authoritative contract로 오해하지 않게 된다.

### 비용

- `SM.Meta`에 public port/DTO 수가 늘어난다.
- current playable UI에서 online capability를 직접 체험할 수는 없고, future seam이 문서/타입에만 남는다.
- direct-scene tooling path를 유지하기 위해 auto-start policy를 별도 seam으로 관리해야 한다.

### 후속

- `OnlineMockAdapter`
- 실제 server adapter와 auth gateway
- official arena/dashboard/reward flow
- `SaveProfile` concern 분해와 ledger/audit/persistence record 재절단
