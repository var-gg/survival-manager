# Steam Display And Input Policy

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-09
- 소스오브트루스: `docs/07_release/display-and-input-policy.md`
- 관련문서:
  - `docs/05_setup/local-runbook.md`
  - `docs/06_production/pre-art-release-floor.md`
  - `docs/03_architecture/unity-scene-flow.md`
  - `tasks/018_combat_sandbox_recentering/status.md`

## 목적

이 문서는 Steam 1차 런칭 기준으로 display/input 정책을 고정한다.
이번 범위는 데스크톱 런칭 기준을 닫는 것이며, 모바일은 이후 확장을 막지 않는 seam만 유지한다.

## 해상도 계약

- 최소 지원: `1280x720`
- 기준 설계 해상도: `1920x1080`
- 추가 검증:
  - `1920x1200`
  - `2560x1440`
  - `3440x1440`

위 해상도에서 HUD, overlay, modal, Battle/Town/Expedition/Reward runtime panel이 잘리지 않고 핵심 CTA를 유지해야 한다.

## UI scaling 계약

- shared `PanelSettings`와 `RuntimePanelHost` fallback reference resolution은 `1920x1080`으로 고정한다.
- 기본 scale mode는 `ScaleWithScreenSize`를 사용한다.
- safe area는 이번 슬라이스에서 실제 모바일 UX를 구현하지 않고, wrapper/adapter seam만 유지한다.
- scene self-healing으로 canvas/panel을 런타임 생성하지 않는다. scene asset과 internal recovery lane이 UI contract를 소유한다.

## 입력 계약

- shipped control baseline은 mouse/keyboard다.
- input abstraction은 유지하되, 모바일 전용 touch gesture/UX는 이번 범위에 넣지 않는다.
- `SM/전투테스트`와 `SM/전체테스트` 모두 동일한 데스크톱 입력 기본 계약을 공유한다.

## mobile future-proof 범위

- 허용:
  - safe-area adapter seam
  - HUD/view state 분리
  - input abstraction/interface 유지
- 제외:
  - 모바일 성능 튜닝
  - 터치 UX 완성
  - 모바일 전용 레이아웃 재배치
  - 플랫폼별 세부 분기 최적화

## RC 확인 항목

- `1280x720`, `1920x1080`, `1920x1200`, `2560x1440`, `3440x1440`에서 UI 확인
- locale flip 시 label overflow와 clipped panel 없음 확인
- `SM/전투테스트` direct lane과 `SM/전체테스트`에서 동일한 기준 해상도 정책 유지 확인
- internal recovery 실행 후에도 panel reference resolution drift가 재도입되지 않았는지 확인
