# 현재 알려진 이슈

- 상태: active
- 최종수정일: 2026-04-08
- 단계: prototype

## 먼저 실행할 메뉴

- `SM/Setup/Prepare Observer Playable`

## 이슈

- Battle 연출은 readable observer 단계라 animation / VFX / camera polish가 제한적이다.
- default playable path의 canonical sample content lookup은 `Resources`만 정상 경로로 사용한다. editor validation/diagnostic lane에서는 fallback이 여전히 개입할 수 있다.
- Expedition은 고정 5-node linear authored track까지만 구현되어 있고 procedural map은 아직 없다.
- Town / Battle runtime binding은 scene asset 참조보다 live rebind 경로에 더 의존한다.
- Unity MCP console에는 환경에 따라 `UnityCliTools` 관련 로그가 남을 수 있다.
- Unity project lock이 있으면 `test-batch-fast`, playable bootstrap, smoke evidence가 stale 결과로 끝날 수 있다. 이 경우 fresh acceptance evidence로 채택하지 않는다.

## 우회 절차

- scene/UI 참조가 꼬였으면 `SM/Setup/Prepare Observer Playable`를 다시 실행한다.
- sample content가 비정상적이면 먼저 `SM/Setup/Ensure Sample Content`를 실행하고, 필요할 때만 `SM/Setup/Generate Sample Content`를 다시 실행한다.

## debug smoke lane 메모

- 빠른 Battle smoke가 필요하면 Town의 `Quick Battle (Smoke)` 또는 `SM/Quick Battle`를 사용한다.
- 원정 재개 확인은 Town의 `Resume Expedition`으로 검증한다.
- direct-scene play fallback과 runtime rebind는 개발자용 복구 인프라로만 취급한다.
- direct-scene fallback / runtime rebind 성공만으로 normal playable acceptance를 닫지 않는다.
- save corruption 대응, smoke persistence 격리, reward settlement resume 정책은 [runtime-hardening-contract.md](./runtime-hardening-contract.md)를 기준으로 본다.
