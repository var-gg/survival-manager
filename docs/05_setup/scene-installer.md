# Scene Installer

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

marker-only 씬을 수동 drag&drop으로 고치지 않고, editor automation으로 재현 가능하게 복구한다.

## 메뉴

- `SM/Bootstrap/Rebuild First Playable Scenes`

## 보장 범위

이 메뉴는 다음 씬을 열고 필요한 root object / controller / UI object를 생성 또는 복구한다.

- `Boot`
- `Town`
- `Expedition`
- `Battle`
- `Reward`

또한 Build Settings 씬 순서를 아래로 보정한다.

1. `Boot`
2. `Town`
3. `Expedition`
4. `Battle`
5. `Reward`

## 설치 원칙

- 반복 실행 시 중복 생성 대신 repair/update
- controller serialized reference는 editor script에서 다시 연결
- pushed scene asset이 marker-only여도 메뉴 실행으로 playable scaffold를 복구 가능하게 유지

## 권장 순서

1. `SM/Bootstrap/Ensure Sample Content`
2. `SM/Bootstrap/Rebuild First Playable Scenes`
3. 필요 시 `SM/Validation/Validate Content Definitions`
4. `Boot.unity` 열기
5. Play
