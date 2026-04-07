# Operator First Playable Checklist

## 반드시 먼저 실행할 메뉴 1개

- `SM/Setup/Prepare Observer Playable`

- 상태: active
- 최종수정일: 2026-04-08
- 단계: prototype

## normal lane acceptance

1. Unity 열기
2. `SM/Setup/Prepare Observer Playable`
3. `Boot.unity` Play
4. Boot에서 `Start Local Run`
5. Boot one-line summary와 locale overlay가 보이는지 확인한다.
6. Town에서 `CampaignSummaryLabel`과 `DeployPreviewLabel`만으로 현재 준비 상태와 primary CTA를 5초 안에 읽을 수 있는지 본다.
7. Town에서 `Start Expedition`
8. Expedition에서 선택 route summary가 reward / risk / abandon consequence를 한 패널에 모아 보여 주는지 확인한다.
9. Battle에서 authored lane playback / smoke action group이 숨겨지고 종료 후 `Continue`만 primary인지 확인한다.
10. Reward에서 카드 1장 선택
11. applied delta와 continuation state가 summary에 남아 있는지 확인한다.
12. `Return Town`
13. Town에서 `Resume Expedition` 노출과 chapter/site 잠금을 확인한다.
14. boss 이후 extract node가 `Reward -> Town(close)`로 끝나는지 확인한다.

## smoke lane acceptance

1. Town에서 secondary `Quick Battle (Smoke)`를 누른다.
2. Battle에서 playback group과 `Debug / Smoke` group이 보이는지 확인한다.
3. `Rebattle (Debug)` / direct `Return to Town (Debug)`가 보이는지 확인한다.
4. Town 복귀 뒤 campaign/site progression이 바뀌지 않았는지 확인한다.

## 확인 항목

- Town에서 recruit 카드 4개와 `Start Expedition` 또는 `Resume Expedition`, warning utility `Return to Start`, debug group의 `Quick Battle (Smoke)`가 보인다.
- Town active UI에서 Quick Battle은 secondary/debug CTA이고, active run 중에는 비활성화된다.
- Expedition에서 selected route summary가 `type / planned reward / node effect / return consequence`를 보여 준다.
- authored Battle에서 primitive actor / HP / 로그 / progress가 보이고 smoke-only controls는 숨겨진다.
- smoke Battle에서만 pause / speed / replay / rebattle / direct return이 보인다.
- Reward에서 카드 선택 즉시 summary/status가 갱신되고 applied delta가 남는다.
- Town 복귀 후 normal lane은 resume 가능 상태를 유지하고, final extract 뒤에는 run이 닫힌다.
- Town / Expedition / Battle / Reward에서 locale hot-refresh 후 mixed-language가 남지 않는다.

## durable record

- RC packet과 automated floor는 `docs/06_production/pre-art-release-floor.md`를 따른다.
- live sign-off 결과는 `tasks/001_mvp_vertical_slice/status.md`와 함께 남긴다.
