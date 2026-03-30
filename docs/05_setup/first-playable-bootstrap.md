# First Playable Bootstrap

## 반드시 먼저 실행할 메뉴 1개

- `SM/Bootstrap/Prepare Observer Playable`

- 상태: active
- 최종수정일: 2026-03-31
- 단계: prototype

## 목적

first playable 준비를 한 번의 명령으로 끝낸다.

## 사용자가 실제로 해야 할 일

1. Unity 열기
2. `SM/Bootstrap/Prepare Observer Playable`
3. `Boot.unity` Play

## clean clone 최초 실행 절차

1. Unity `6000.4.0f1`로 프로젝트를 연다.
2. 스크립트 컴파일 완료를 기다린다.
3. `SM/Bootstrap/Prepare Observer Playable` 실행
4. localization foundation / minimum bootstrap content / validation / scene rebuild / build settings 보정 수행
5. `Boot.unity` 자동 오픈 확인
6. Play 클릭
7. Town 진입 확인
8. `Debug Start` 클릭
9. `Next Battle` 클릭

## 메뉴 실행 경로

- `SM/Bootstrap/Prepare Observer Playable`

## 선택형 Unity CLI executeMethod

- `SM.Editor.Bootstrap.FirstPlayableBootstrap.PrepareObserverPlayable`

예시:

```bash
Unity.exe -batchmode -projectPath "A:\projects\game\survival-manager" -executeMethod SM.Editor.Bootstrap.FirstPlayableBootstrap.PrepareObserverPlayable -quit
```

## 성공 시 기대 결과

- minimum bootstrap content 보장
- content validation 통과
- first playable scenes rebuild/repair 완료
- build settings 보정 완료
- `Boot.unity` 오픈
- Console에 `이제 Boot scene에서 Play를 누르라` 로그

## 실패 시 확인

- Console에서 `[ObserverPlayable]` prefix 로그 확인
- 실패 지점이 애매하면 메뉴를 한 번 더 실행하지 말고 compile error / missing package부터 확인
