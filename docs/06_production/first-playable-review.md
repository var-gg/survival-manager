# 첫 플레이어블 스냅샷

- 상태: active
- 최종수정일: 2026-04-08
- 단계: prototype

## 먼저 실행할 메뉴

- `SM/Play/Full Loop`

## 한 줄 요약

- 현재 prototype의 공개 playable 경계는 `Boot -> Town -> Expedition -> Battle -> Reward -> Town` 단일 local/authored loop다.
- canonical lane은 `Town -> Expedition -> Battle -> Reward -> Town`이고, 사람이 기억하는 top-level entry는 `Full Loop`와 `Combat Sandbox` 두 개다.
- Town `Quick Battle (Smoke)`는 integration smoke, direct `Combat Sandbox`는 pure battle lane이다.

## normal playable lane

- Boot에서 `Start Local Run`으로 진입한다.
- Town에서는 chapter/site 선택, recruit, squad, deploy, save/load, `Start Expedition` 또는 `Resume Expedition`을 primary surface로 본다.
- Reward 이후에는 Town으로 복귀하고, active run이면 `Resume Expedition`으로 같은 site track을 이어 간다.
- boss 뒤 extract settlement도 반드시 Reward를 거쳐 Town에서 run close를 확정한다.

## debug smoke lane

- Town의 `Quick Battle (Smoke)`
- `SM/Play/Combat Sandbox`
- direct-scene play fallback
- runtime rebind/repair

위 경로는 smoke/복구 인프라다. README 본문과 active UI의 본선 경로로 취급하지 않는다.

## live 상태 문서

- 최신 상태, 리스크, 다음 우선순위는 [tasks/001_mvp_vertical_slice/status.md](../../tasks/001_mvp_vertical_slice/status.md)를 기준으로 본다.
- release floor와 packet 규칙은 [docs/06_production/pre-art-release-floor.md](./pre-art-release-floor.md)를 기준으로 본다.
