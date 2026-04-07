# 현재 알려진 이슈

- 상태: active
- 최종수정일: 2026-04-07
- 단계: prototype

## 먼저 실행할 메뉴

- `SM/Setup/Prepare Observer Playable`

## 이슈

- Battle 연출은 readable observer 단계라 animation / VFX / camera polish가 제한적이다.
- default playable path의 canonical sample content lookup은 `Resources`만 정상 경로로 사용한다. editor validation/diagnostic lane에서는 fallback이 여전히 개입할 수 있다.
- Expedition은 고정 5노드 branching까지만 구현되어 있고 procedural map은 아직 없다.
- Town / Battle runtime binding은 scene asset 참조보다 live rebind 경로에 더 의존한다.
- Unity MCP console에는 환경에 따라 `UnityCliTools` 관련 로그가 남을 수 있다.

## 우회 절차

- scene/UI 참조가 꼬였으면 `SM/Setup/Prepare Observer Playable`를 다시 실행한다.
- sample content가 비정상적이면 먼저 `SM/Setup/Ensure Sample Content`를 실행하고, 필요할 때만 `SM/Setup/Generate Sample Content`를 다시 실행한다.
- Town에서 가장 빠르게 전투 확인이 필요하면 `Quick Battle`을 사용한다.
- 원정 진행을 이어서 확인하려면 Town에서 `Debug Start`를 다시 눌러 Expedition scene으로 복귀한다.
