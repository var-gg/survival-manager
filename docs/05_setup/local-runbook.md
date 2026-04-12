# 로컬 실행 런북

- 상태: active
- 최종수정일: 2026-04-09
- 단계: prototype

## 먼저 실행할 메뉴

- `SM/전체테스트`
- 필요 시 `SM/Internal/Recovery/Ensure Localization Foundation`

## 빠른 실행 절차

1. Unity editor `6000.4.0f1`로 프로젝트를 연다.
2. `SM/전체테스트`를 실행한다.
3. `Boot.unity`가 자동으로 열렸는지 확인한다.
4. Play 한다.
5. Boot에서 `Start Local Run`을 누른다.
6. Boot 우측 상단 language overlay에서 `ko`/`en` 전환이 되는지 확인한다.
7. Boot status가 `Town -> Expedition -> Battle -> Reward` local loop를 한 줄로 설명하고, top-level 기억 대상이 `Full Loop`와 `Combat Sandbox` 두 개뿐인지 확인한다.

## optional CLI fast lane

- canonical setup: `pwsh -File tools/unity-bridge.ps1 prepare-playable`
- localization recovery: `pwsh -File tools/unity-bridge.ps1 ensure-localization`
- scene recovery: `pwsh -File tools/unity-bridge.ps1 repair-scenes`
- debug smoke: `pwsh -File tools/unity-bridge.ps1 quick-battle-smoke`

## normal playable lane

1. Town에서 `CampaignSummaryLabel`이 chapter/site, lock 상태, pending reward 또는 active run 상태를 설명하는지 확인한다.
2. Town에서 `DeployPreviewLabel`이 squad 인원, deploy, posture, primary CTA 상태를 보여 주고 `StatusLabel`은 transient feedback만 담당하는지 본다.
3. Town 하단 액션에서 `Quick Battle (Smoke)`가 secondary/debug group에만 남고, primary mental model은 `Start Expedition` / `Resume Expedition`인지 확인한다.
4. active run이 없으면 `Start Expedition`을 누른다.
5. Expedition에서 선택 route summary가 `type -> planned reward -> node effect / risk -> return to town consequence` 순서로 읽히는지 본다.
6. battle node면 `Enter Battle`, settlement node면 `Resolve Settlement`로 CTA가 바뀌는지 확인한다.
7. authored Battle에서는 playback/smoke action group이 숨겨지고 종료 후 `Continue`만 primary로 남는지 본다.
8. Reward에서 카드 1장 선택 후 applied delta와 continuation state가 summary에 남아 있는지 확인한다.
9. `Return to Town`으로 복귀한다.
10. Town에서 `Resume Expedition`으로 같은 site track을 재개한다.
11. boss 뒤 extract node는 전투 없이 `Reward -> Town(close)`로 마무리한다.

## locale hot-refresh lane

1. Town, Expedition, Battle, Reward 각 화면에서 `ko -> en -> ko` 전환을 수행한다.
2. panel title, summary, button label, tooltip, help strip, selected unit metadata가 scene reload 없이 즉시 바뀌는지 확인한다.
3. mixed-language text가 남아 있으면 실패로 본다.

## save / recovery contract

- canonical acceptance checkpoint는 `Town exit -> Battle resolved -> Reward settled -> Return-to-Start` 순서로 본다.
- reward card를 고른 직후의 `RewardApplied` 저장은 durability anchor로 유지하지만, newcomer acceptance 보고서에서는 위 4개 checkpoint를 기준으로 적는다.
- Town `Load`는 idle profile recovery 전용이다. `CurrentScene == Town`, active expedition 없음, pending reward settlement 없음, quick battle smoke overlay 아님 조건을 모두 만족할 때만 허용한다.
- existing save가 손상됐으면 `primary verify -> backup verify -> quarantine/fail` 순서로 처리한다. 기존 파일이 있었는데 검증에 실패한 경우를 `missing create`로 흡수하지 않는다.

## restart injection 기준

- merge candidate normal playable smoke에서는 최소 세 지점에서 재기동 주입을 확인한다.
- Town checkpoint 직후
- Battle result 확정 직후 Reward 진입 전
- Reward settlement 완료 직후 Town 복귀 전

## 원정 재개 경로

1. Town에서 `Start Expedition`
2. 첫 battle과 Reward를 한 번 완료한 뒤 Town으로 복귀한다.
3. Town에서 chapter/site selector가 잠겼는지 확인한다.
4. 다시 Town에서 `Resume Expedition`을 눌러 진행 중 원정을 재개한다.
5. Quick Battle은 active run이 있을 때 비활성화되어야 한다.

## combat sandbox / smoke lane

- `SM/전투테스트`와 `pwsh -File tools/unity-bridge.ps1 quick-battle-smoke`는 pure battle direct lane이다.
- Town의 `Quick Battle (Smoke)`는 현재 Town 빌드를 들고 들어가는 integration smoke다.
- normal playable acceptance에는 두 경로 모두 사용하지 않는다.
- direct sandbox에서는 `Replay Same Seed`, `New Seed`, `Exit Sandbox`만 보이고 Reward/Town progression CTA는 숨겨져야 한다.
- Town smoke에서는 `Continue`, `Return to Town (Debug)`가 유지되고, canonical Town 상태 복구 계약을 따라야 한다.
- heavy preset authoring, batch, source 전환은 Inspector 또는 `Window/SM/Combat Sandbox`에서 수행한다.

## 현재 계약 경로

- canonical sample content root: `Assets/Resources/_Game/Content/Definitions/**`
- scene repair source of truth: `SM/Internal/Recovery/Repair First Playable Scenes`
- full-loop preflight + launch: `SM/전체테스트`
- localization foundation source of truth: `SM/Internal/Recovery/Ensure Localization Foundation`
- canonical content recovery: `SM/Internal/Content/Ensure Sample Content`
- release-floor packet wrapper: `pwsh -File tools/pre-art-rc.ps1`
- default playable content load path: `Resources.LoadAll(...)` only
- editor sweep / file fallback: explicit diagnostic lane only
- `GameBootstrap`는 normal playable path에서 editor filesystem fallback을 보지 않는다.

## 참고 문서

- 최신 playable 상태: [tasks/001_mvp_vertical_slice/status.md](../../tasks/001_mvp_vertical_slice/status.md)
- 현재 알려진 이슈: [docs/06_production/current-known-issues.md](../06_production/current-known-issues.md)
- 런타임 hardening 계약: [docs/06_production/runtime-hardening-contract.md](../06_production/runtime-hardening-contract.md)
