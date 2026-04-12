# First Playable Bootstrap

## 반드시 먼저 실행할 메뉴 1개

- `SM/전체테스트`

- 상태: active
- 최종수정일: 2026-04-09
- 단계: prototype

## 목적

full loop newcomer lane의 preflight와 Boot 진입을 한 번의 명령으로 끝낸다.

## 사용자가 실제로 해야 할 일

1. Unity 열기
2. `SM/전체테스트`
3. `Boot.unity` Play

## clean clone 최초 실행 절차

1. Unity `6000.4.0f1`로 프로젝트를 연다.
2. 스크립트 컴파일 완료를 기다린다.
3. `SM/전체테스트` 실행
4. canonical sample content readiness와 Boot scene contract를 fail-fast preflight로 확인한다.
5. `Boot.unity` 자동 오픈 확인
6. Play 클릭
7. Boot에서 `Start Local Run` 확인
8. Town 진입 확인
9. `Start Expedition` 클릭
10. `Next Battle` 클릭

## 메뉴 실행 경로

- `SM/전체테스트`

## 선택형 Unity CLI executeMethod

- `SM.Editor.Bootstrap.FirstPlayableBootstrap.PrepareObserverPlayable`

예시:

```bash
Unity.exe -batchmode -projectPath "A:\projects\game\survival-manager" -executeMethod SM.Editor.Bootstrap.FirstPlayableBootstrap.PrepareObserverPlayable -quit
```

## 성공 시 기대 결과

- canonical sample content readiness 확인
- Boot scene contract 확인
- `Boot.unity` 오픈
- Console에 `이제 Boot scene에서 Play를 누르라` 로그

## 실패 시 확인

- Console에서 `[FullLoop]` prefix 로그 확인
- validation issue는 `SM/Internal/Validation/Validate Canonical Content` 또는 `SM/Internal/Validation/Validate Content Definitions`로 strict 확인
- scene/UI 문제는 `SM/Internal/Recovery/Repair First Playable Scenes`로 명시 복구
- localization foundation 문제는 `SM/Internal/Recovery/Ensure Localization Foundation`로 복구
- 실패 지점이 애매하면 메뉴를 한 번 더 실행하지 말고 compile error / missing package부터 확인
