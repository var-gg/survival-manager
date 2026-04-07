# Quick Battle Smoke

- 상태: active
- 최종수정일: 2026-04-08
- 단계: prototype

## 목적

전투 테스트를 위한 원클릭 진입 경로와 반복 테스트 워크플로우를 정리한다.

## smoke lane 원칙

- Quick Battle은 canonical playable lane이 아니다.
- campaign/site progression, site clear, chapter clear 검증에는 사용하지 않는다.
- Town에서는 `Quick Battle (Smoke)` secondary CTA로만 노출한다.
- `Re-battle`, `Return Town` direct bypass는 smoke lane에서만 허용한다.
- smoke lane만 verbose runtime artifact와 raw timing bundle을 허용한다. normal playable lane은 checkpoint summary만 남긴다.

## 원클릭 전투 테스트

1. Unity 열기
2. `SM/Quick Battle` 메뉴 클릭

CLI mirror:

```powershell
pwsh -File tools/unity-bridge.ps1 quick-battle-smoke
```

기존 `pwsh -File tools/unity-bridge.ps1 bootstrap`는 같은 경로를 가리키는 deprecated alias다.

이 한 번의 메뉴 클릭으로 다음이 자동 수행된다:
- 로컬라이제이션 기반 에셋 확인
- 샘플 콘텐츠 확인/생성
- 콘텐츠 유효성 검증
- 씬 복구
- Quick Battle config 에셋 확인/생성
- 로컬 세이브 초기화
- Battle 씬 열기
- **자동 Play 진입**
- `OfflineLocal` 세션 자동 준비 후 Battle smoke 초기화

즉 Quick Battle actual path는 `Open Battle scene -> Enter Play Mode`이며, Boot/Town은 우회한다.

## persistence 격리 규칙

- `SM/Quick Battle` direct entry는 dedicated smoke namespace를 사용한다. 기본 profile이 `default`면 smoke save는 `default.smoke`에 기록된다.
- `SM/Quick Battle` auto-clear는 canonical save가 아니라 smoke namespace artifact만 지운다.
- Town의 `Quick Battle (Smoke)`는 canonical Town checkpoint를 먼저 만든 뒤 transient overlay로 Battle에 들어간다.
- Town smoke 중에는 canonical save write를 금지한다.
- Town smoke 종료 시에는 reward 적용 여부와 무관하게 disk에서 canonical profile을 다시 bind해서 overlay를 폐기한다.

## 전투 중 컨트롤

| 버튼 | 동작 |
| --- | --- |
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

## Town에서 smoke 실행

`SM/Setup/Prepare Observer Playable` 메뉴로 normal playable을 띄운 뒤 Town에서도 smoke를 실행할 수 있다.

1. `SM/Setup/Prepare Observer Playable`
2. `Boot.unity` Play
3. Town에서 secondary `Quick Battle (Smoke)`
4. Battle 종료 후 `Continue` 또는 direct `Return Town`
5. Reward를 택했다면 카드 선택
6. `Return Town`

이 경로는 Town UI smoke 확인용이며, `Start Expedition / Resume Expedition` acceptance를 대체하지 않는다.
또한 smoke 종료 후에는 canonical Town 상태가 disk 기준으로 복원되어야 한다.

## RC lane note

- Quick Battle smoke는 RC blocking floor다.
- 하지만 canonical newcomer lane이나 normal loop acceptance를 대체할 수는 없다.

## 기대 화면

- Battle: primitive actor, HP label/bar, 로그, 속도 버튼, pause, progress, Re-battle, Return Town
- Reward: 3지선다 카드, 요약/상태 텍스트

## 참고 문서

- normal playable acceptance: [docs/05_setup/local-runbook.md](./local-runbook.md)
- runtime hardening contract: [docs/06_production/runtime-hardening-contract.md](../06_production/runtime-hardening-contract.md)
