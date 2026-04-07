# session realm authority와 offline/online port 구조

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-06
- 소스오브트루스: `docs/03_architecture/session-realm-authority-and-offline-online-ports.md`
- 관련문서:
  - `docs/03_architecture/persistence-strategy.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/04_decisions/adr-0020-session-realm-authority-boundary.md`

## 목적

이 문서는 session realm, authority ownership, offline-first adapter, future online port 구조를
현재 repo 기준으로 고정한다.

## 핵심 원칙

- online/offline 구분은 저장소 backend가 아니라 truth owner 차이다.
- `SaveProfile` full save는 `OfflineLocal` 편의 경로에만 남긴다.
- 공식 mutation 경계에는 repository보다 command/query port를 먼저 둔다.
- UI는 `SaveProfile` 내부 shape에 직접 매달리지 않고 view DTO를 읽는다.

## 현재 구현 경계

### `SM.Meta`

- `SessionRealm`
- `SessionCapabilities`
- `ProfileView`, `InventoryView`, `LoadoutView`, `ArenaDashboardView`
- `ISessionRealmProvider`
- `ISessionCapabilityProvider`
- `IProfileQueryService`
- `IProfileCommandService`
- `IArenaQueryService`
- `IArenaCommandService`
- `IBattleAuthority`

이 레이어는 계약만 가진다.

### `SM.Unity`

- `SessionRealmCoordinator`
- `OfflineLocalSessionAdapter`
- `GameSessionRoot`
- Boot/Town/Reward controller/presenter의 start/local loop orchestration

이 레이어는 현재 slice의 composition root와 offline adapter를 가진다.

### `SM.Persistence.Abstractions` / `SM.Persistence.Json`

- `ISaveRepository`
- `SaveProfile`
- JSON fallback adapter

이 경계는 `OfflineLocalSessionAdapter` 뒤에만 둔다.

## offline adapter 규칙

- `OfflineLocalSessionAdapter`만 `ISaveRepository.LoadOrCreate/Save`를 호출한다.
- `GameSessionRoot`, presenter, controller, future online port는 repository를 직접 호출하지 않는다.
- `IProfileCommandService`에서 현재 연결하는 mutation은 다음 네 개만 허용한다.
  - `EquipItem`
  - `UnequipItem`
  - `EquipPermanentAugment`
  - `SelectPassiveBoard`
- `IArenaCommandService`, `IBattleAuthority`는 offline에서 unsupported 또는 preview-only 결과만 반환한다.

## bootstrap과 direct-play 규칙

- Boot는 start action 전까지 active session을 시작하지 않는다.
- Quick Battle은 offline-only tooling path이므로 auto-start로 `OfflineLocal`을 시작한다.
- `GameSessionRoot.EnsureInstance()`는 Boot가 아닌 scene에서만 `OfflineLocal` auto-start를 수행한다.

## future online / mock 규칙

- `OnlineMockAdapter`와 실제 server adapter는 다음 slice로 미룬다.
- future online adapter도 `SaveProfile` full object save를 허용하지 않는다.
- authoritative inventory, reward, rating, defense snapshot은 command handler를 통해서만 바뀌어야 한다.
- server-like id/seed/timestamp 발급은 mock/server adapter 책임으로 둔다.
