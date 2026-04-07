# 전장과 카메라

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `docs/02_design/combat/battlefield-and-camera.md`
- 관련문서:
  - `docs/02_design/combat/deployment-and-anchors.md`
  - `docs/02_design/combat/realtime-simulation-model.md`
  - `docs/02_design/combat/combat-readability.md`
  - `docs/02_design/combat/battle-playback-contract.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/02_design/ui/battle-observer-ui.md`

## 목적

이 문서는 prototype 전투에서 플레이어가 실제로 보게 되는 전장 구도와 카메라 기준을 고정한다.
목표는 4v4 auto-skirmish를 읽기 쉬운 관전자 화면으로 유지하면서, 배치의 의미와 전투 중 움직임을 동시에 살리는 것이다.

## 전장 기준

- 전장은 항상 좌측 아군 vs 우측 적군 구도로 시작한다.
- side마다 `3x2` 배치 앵커를 가진다.
- 배치 페이즈에서는 유닛을 앵커에 올린다.
- 전투가 시작되면 유닛은 앵커에 고정되지 않고 연속 좌표계 위에서 이동한다.
- `top / center / bottom`은 lane 의미를 제공하지만, 전투 중 실제 이동은 off-grid다.
- MVP 스케일은 `4v4`를 유지한다.

## 앵커와 공간의 역할 분리

- 앵커는 spawn 위치, home 위치, leash 기준점, 초기 배치 의미를 만든다.
- 실제 교전 위치는 `CombatVector2` 연속 좌표를 기준으로 계산한다.
- 배치의 의미는 남기되, 전투 표현은 "제자리에 서서 숫자만 교환하는" 모델로 되돌리지 않는다.
- scene graph에서는 `BattleStageRoot`와 `BattlePresentationRoot`를 분리한다.
- actor wrapper는 `BattlePresentationRoot` 하위에만 생성되고, `BattleStageRoot`는 decor/readability floor만 가진다.

## 카메라 기준

- 카메라는 약한 사선 구도를 유지한다.
- 화면 하나에서 양 팀, 체력바, 현재 타깃, windup 상태를 동시에 읽을 수 있어야 한다.
- 전투 중 카메라는 유닛 하나를 과하게 따라가지 않는다.
- 현재 Battle scene prototype은 manual observer 카메라를 유지하되, suggested framing만 추가한다.

## 카메라 제어

- `BattleCameraController`가 카메라 공간 이동을 전담한다.
- 카메라 회전은 고정(`Euler(33, -12, 0)`)이며 변경하지 않는다.

### 입력 매핑

| 입력 | 동작 |
| --- | --- |
| 우클릭 드래그 / 중버튼 드래그 | 카메라 패닝 |
| 휠 스크롤 | 줌 인/아웃 |
| WASD / 방향키 | 키보드 패닝 |
| 화면 가장자리 커서 (20px) | edge scrolling |

### 바운드

- `BattlefieldLayout.Default` 기준으로 양 팀 BackRow + margin까지만 허용한다.
- X: [-8, 8], Z: [-4, 4] (배치 좌표 + 약 3유닛 여유)
- 줌 높이: [4, 12] (기본 7.8)
- 바운드를 벗어나는 이동은 clamp한다.

### 줌

- 줌 시 커서 방향으로 약간 이동한다 (zoom-to-cursor).
- `SmoothDamp`로 부드러운 전환을 유지한다.

### suggested framing

- `Bootstrap`: 전투 시작 / replay-to-zero 직후 alive unit bounds + deployment anchor union을 한 번 fit한다.
- `Passive`: manual input이 일정 시간 없을 때만 alive unit envelope를 천천히 recenter / refit한다.
- `ManualHold`: pan / zoom / drag / keyboard input 후 `3초` 동안 자동 보정을 멈춘다.
- suggested frame는 `BattleScreenController`가 계산하고 `BattleCameraController`가 수용한다.
- single-target follow, cinematic cut, screen shake는 금지한다.
- wrapper의 `CameraFocusTarget`은 actor-local 안정점일 뿐, 카메라 authoritative truth가 되지 않는다.

### 유닛 클릭 선택

- normal lane에서 좌클릭 actor pick을 허용한다.
- 선택은 camera drag와 구분되어야 하며, blocking UI 위 포인터에서는 동작하지 않는다.

- `BattleCameraController.IsDragging`과 UI block predicate로 클릭/드래그를 구분한다.
- 선택은 world collider보다 presentation pick(radius 기반 head anchor screen distance)에 의존해도 된다.

## 비목표

- TFT식 board-locked hex 전장
- LoL 전체 맵, lane objective, minion wave, tower
- NavMesh 중심 자유 이동
- 자유 회전 관전 카메라

## 현재 구현 메모

- Battle scene은 capsule actor를 사용한다.
- 카메라는 `BattleCameraController`가 초기화하고, 없으면 fallback으로 직접 설정한다.
- anchor plate / lane guide 같은 readability surface를 전장 장식과 분리한다.
- selected unit의 home anchor 의미는 전투 시작 직후 사라지지 않아야 한다.
- 전장 가독성 예산은 `docs/02_design/combat/combat-readability.md`를 따른다.
