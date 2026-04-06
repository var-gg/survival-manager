# session realm과 공식 온라인 경계

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-06
- 소스오브트루스: `docs/02_design/meta/session-realm-and-official-online-boundary.md`
- 관련문서:
  - `docs/02_design/meta/pvp-boundary.md`
  - `docs/03_architecture/session-realm-authority-and-offline-online-ports.md`
  - `docs/04_decisions/adr-0020-session-realm-authority-boundary.md`

## 목적

이 문서는 online/offline을 단순 네트워크 상태가 아니라 session realm으로 정의하고,
플레이어 UX에서 어떤 선택과 제한으로 노출할지 고정한다.

## session realm 정의

### `OfflineLocal`

- 로컬 save와 로컬 session state가 truth다.
- 완전 오프라인 플레이를 허용한다.
- 조작 가능성을 감수한다.
- 공식 progression, 공식 inventory, 공식 reward, 공식 PvP, 공식 rating과 연결하지 않는다.

### `OnlineAuthoritative`

- 서버 DB와 서버 규칙이 truth다.
- 클라이언트는 인증 정보와 intent/command만 전송하고 결과 상태를 확정하지 않는다.
- 공식 progression, 공식 reward, 공식 inventory, 공식 PvP, 공식 rating은 이 realm에서만 연다.
- 현재 slice에서는 UX에만 노출하고 실제 진입은 열지 않는다.

## UX 규칙

- session realm 선택은 Boot에서 수행한다.
- realm 선택 전에는 Town으로 자동 진입하지 않는다.
- 한 번 시작한 런에서는 realm 전환을 허용하지 않는다.
- realm 전환이 필요하면 Session Menu로 돌아가 Boot에서 다시 선택한다.
- 현재 slice에서 `OnlineAuthoritative` 버튼은 비활성화하고 후속 패스 안내만 노출한다.

## 공식/비공식 기능 경계

- PvP는 `OnlineAuthoritative`에서만 열린다.
- official reward claim과 authoritative progress upload도 `OnlineAuthoritative`에서만 열린다.
- `OfflineLocal` reward는 로컬 progression에만 반영된다.
- local preview compile, local battle simulation, debug replay는 허용되지만 공식 settlement를 대체하지 않는다.

## 현재 slice 적용 메모

- Boot는 content/localization preflight 후 realm 선택 대기 화면으로 머문다.
- Quick Battle과 direct-scene play는 tooling 안정성을 위해 자동으로 `OfflineLocal`을 시작한다.
- Town은 현재 realm, capability, arena availability를 표시한다.
- Reward는 현재 reward가 local-only인지 여부를 명시적으로 보여준다.
