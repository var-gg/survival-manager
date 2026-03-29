# Current Known Issues

## 반드시 먼저 실행할 메뉴 1개

- `SM/Bootstrap/Prepare Observer Playable`

- 상태: active
- 최종수정일: 2026-03-30
- phase: prototype

## known issues

- Battle animation은 observer-grade라 hit timing과 HP text가 완전히 cinematic하지는 않다.
- Expedition은 고정 5노드 branching까지만 구현되어 있고 procedural map은 아직 없다.
- Quick Battle은 smoke loop라 Expedition 진행도와 독립적으로 돈다.
- Town / Battle custom controller는 현재 scene asset 재오픈 시 live rebind에 의존한다. `SM/Bootstrap/Prepare Observer Playable` 또는 scene reopen 이후 binder가 다시 붙인다.
- Unity MCP console에는 현재 `Assets/_Game/Scripts/Editor/UnityCliTools/**`의 `UnityCliConnector` 의존 로그가 남아 있을 수 있다.

## workaround

- scene/UI 참조가 꼬였으면 `SM/Bootstrap/Prepare Observer Playable`를 다시 실행한다.
- sample content가 비정상적이면 `SM/Seed/Generate Sample Content`를 다시 실행한다.
- Town에서 첫 전투 화면만 확인하려면 `Quick Battle`을 사용한다.
- Expedition progress를 이어 보려면 Town에서 `Debug Start`를 다시 눌러 Expedition scene으로 복귀한다.
