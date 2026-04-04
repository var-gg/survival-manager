# Quick Battle Smoke

- 상태: active
- 최종수정일: 2026-04-04
- 단계: prototype

## 목적

전투 테스트를 위한 원클릭 진입 경로와 반복 테스트 워크플로우를 정리한다.

## 원클릭 전투 테스트

1. Unity 열기
2. `SM/Quick Battle` 메뉴 클릭

이 한 번의 메뉴 클릭으로 다음이 자동 수행된다:
- 로컬라이제이션 기반 에셋 확인
- 샘플 콘텐츠 확인/생성
- 콘텐츠 유효성 검증
- 씬 복구
- Quick Battle config 에셋 확인/생성
- 로컬 세이브 초기화
- Boot 씬 열기 + **자동 Play 진입**
- Boot → Battle 씬 직행 (Town 스킵)

## 전투 중 컨트롤

| 버튼 | 동작 |
|---|---|
| x1 / x2 / x4 | 시뮬레이션 속도 조절 |
| Pause | 일시정지/재개 |
| F5 (키보드) | 같은 시드로 리플레이 (결정론적 동일 결과) |
| **Re-battle** | 새 시드로 재전투 (인스펙터 config 변경 반영) |
| **Return Town** | Town 씬으로 직접 복귀 (Reward 스킵) |
| Continue | Reward 씬으로 이동 |

## 인스펙터 config 편집

전투 구성은 `CombatSandboxConfig` ScriptableObject로 제어한다.

**경로**: `Assets/Resources/_Game/Content/Definitions/QuickBattle/quick_battle_default.asset`

**편집 가능 항목**:
- `EnemySlots`: 적 팀 구성 (아키타입, 앵커 위치, 특성)
- `AllyPosture` / `EnemyPosture`: 팀 포스처
- `Seed`: 전투 시드 (결정론성)

**적용 방식**: 인스펙터에서 변경 후 `Re-battle` 클릭 시 config가 다시 로드되어 새 전투에 반영된다. 전투 중 라이브 변경은 지원하지 않는다.

## 기존 전체 플로우 (Observer Playable)

`SM/Setup/Prepare Observer Playable` 메뉴로 Boot → Town → Battle → Reward 전체 플로우를 확인할 수 있다.

1. `SM/Setup/Prepare Observer Playable`
2. `Boot.unity` Play
3. Town에서 `Quick Battle`
4. Battle 종료 후 `Continue`
5. Reward에서 카드 선택
6. `Return Town`

## 기대 화면

- Battle: primitive actor, HP label/bar, 로그, 속도 버튼, pause, progress, Re-battle, Return Town
- Reward: 3지선다 카드, 요약/상태 텍스트
