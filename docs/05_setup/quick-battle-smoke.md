# Combat Sandbox And Quick Battle Smoke

- 상태: active
- 최종수정일: 2026-04-09
- 단계: prototype

## 목적

전투 테스트를 위한 direct sandbox lane과 Town integration smoke lane을 함께 정리한다.

## 사람이 기억할 메뉴

- `SM/Authoring/Combat Sandbox`
- `SM/Play/Combat Sandbox`
- `SM/Play/Full Loop`
- `Window/SM/Combat Sandbox`

## lane 원칙

- Combat Sandbox는 canonical playable lane이 아니다.
- campaign/site progression, site clear, chapter clear 검증에는 사용하지 않는다.
- direct sandbox와 Town smoke는 역할을 섞지 않는다.
- Town에서는 `Quick Battle (Smoke)` secondary CTA로만 노출한다.
- smoke lane만 verbose runtime artifact와 raw timing bundle을 허용한다. normal playable lane은 checkpoint summary만 남긴다.

## direct Combat Sandbox 진입

1. Unity 열기
2. `SM/Play/Combat Sandbox` 메뉴 클릭

CLI mirror:

```powershell
pwsh -File tools/unity-bridge.ps1 quick-battle-smoke
```

이 경로는 fail-fast preflight 뒤에 아래만 수행한다:
- active sandbox handoff 로드
- canonical content readiness 확인
- Battle scene contract 확인
- direct sandbox compile 가능 여부 확인
- Battle 씬 열기
- **자동 Play 진입**
- `OfflineLocal` 세션 자동 준비 후 direct Combat Sandbox 초기화

repair, localization foundation, scene reinstall, sample content generation은 자동으로 돌지 않는다.
실패 시에는 `SM/Internal/Recovery/*`, `SM/Internal/Content/*`, `SM/Internal/Validation/*`로 명시적으로 복구한다.

- `combat_sandbox_active.asset` Inspector가 `None (Mono Script)`로 보이면 `SM/Internal/Recovery/Refresh Combat Sandbox Authoring Assets`를 먼저 실행한다.
- refresh 뒤에도 동일하면 Unity를 한 번 재시작한 뒤 asset을 다시 선택한다.

## daily 전투 테스트 루프

1. `SM/Authoring/Combat Sandbox`로 active handoff Inspector를 연다.
2. `Left Team`, `Right Team`, `Execution`을 수정한다.
3. `Compile Preview`로 조합과 preview를 확인한다.
4. 필요하면 `Push Active`로 저장한다.
5. 바로 확인할 때는 `Push Active + Play`를 누른다.
6. runtime만 다시 보고 싶을 때는 `SM/Play/Combat Sandbox`를 쓴다.
7. preset 탐색, variant clone, result/history 비교가 필요할 때만 `Window/SM/Combat Sandbox`를 연다.

## persistence 격리 규칙

- `SM/Play/Combat Sandbox` direct entry는 dedicated smoke namespace를 사용한다. 기본 profile이 `default`면 smoke save는 `default.smoke`에 기록된다.
- direct sandbox auto-clear는 canonical save가 아니라 smoke namespace artifact만 지운다.
- Town의 `Quick Battle (Smoke)`는 canonical Town checkpoint를 먼저 만든 뒤 transient overlay로 Battle에 들어간다.
- Town smoke 중에는 canonical save write를 금지한다.
- Town smoke 종료 시에는 reward 적용 여부와 무관하게 disk에서 canonical profile을 다시 bind해서 overlay를 폐기한다.

## 버튼 / 행동 노출 규칙

### direct Combat Sandbox

| 구분 | 항목 |
| --- | --- |
| 보임 | `x1 / x2 / x4`, `Pause`, `Replay Same Seed`, `New Seed`, `Exit Sandbox`, settings, debug overlay, 결과 요약 |
| 숨김 | `Continue`, Reward settlement CTA, `Return to Town (Debug)`, canonical progression 설명 |
| 의미 | active preset + deterministic seed를 유지한 pure battle authoring lane |

### Town `Quick Battle (Smoke)`

| 구분 | 항목 |
| --- | --- |
| 보임 | `x1 / x2 / x4`, `Pause`, `Replay`, `Rebattle (Debug)`, `Continue`, `Return to Town (Debug)` |
| 숨김 | remote source 전환, preset library 편집, batch authoring, heavy source/build authoring |
| 의미 | 현재 Town 상태를 들고 들어가는 integration smoke lane |

### Full Loop 에만 남기는 행동

| 항목 | 이유 |
| --- | --- |
| chapter/site progression | canonical run 의미 보존 |
| Expedition route selection | authored loop 검증 |
| Reward settlement의 실제 진행 의미 | sandbox와 분리 |
| extract close semantics | full loop acceptance 전용 |

### inspector / window 전용 작업

| 항목 | 위치 |
| --- | --- |
| team/loadout/tactic/passive/seed/layout 직접 편집 | `SM/Authoring/Combat Sandbox` -> `CombatSandboxConfig` Inspector |
| `Compile Preview`, `Push Active`, `Push Active + Play` | `CombatSandboxConfig` Inspector |
| `Run Single`, `Run Batch`, `Run Side Swap` | `CombatSandboxConfig` Inspector |
| preset library 탐색 / search / tag filter / favorites / recent | `Window/SM/Combat Sandbox` |
| `Set Active`, `Save As New`, history/result compare | `Window/SM/Combat Sandbox` |

## authoring surface

주 authoring surface는 `CombatSandboxConfig` Inspector다.

- Inspector:
  - active handoff 직접 편집
  - compile preview
  - single/batch/side-swap 실행
  - `Push Active`, `Push Active + Play`
- `Window/SM/Combat Sandbox`:
  - scenario library: search, tag filter, favorites, recent
  - preview/history/result compare
  - `Set Active`
  - `Save As New`로 scenario clone 후 variant 생성

## asset / preset 구조

authoring library:

- `Assets/_Game/Authoring/CombatSandbox/Scenarios/*.asset`
- `Assets/_Game/Authoring/CombatSandbox/TeamPresets/*.asset`
- `Assets/_Game/Authoring/CombatSandbox/BuildOverrides/*.asset`
- `Assets/_Game/Authoring/CombatSandbox/ExecutionPresets/*.asset`

runtime active handoff:

**경로**: `Assets/_Game/Authoring/CombatSandbox/combat_sandbox_active.asset`

active handoff는 editor direct sandbox가 읽는 단일 authoring asset이고, Inspector가 primary editing surface다.
window/library path는 scenario asset을 active handoff로 sync하는 secondary surface다.

핵심 authoring 개념:

- `CombatSandboxScenarioAsset`
- `TeamLoadoutPresetAsset`
- `UnitBuildOverrideAsset`
- `CombatSandboxExecutionPreset`

지원 source mode:

- `CurrentLocalProfile`
- `AuthoredSyntheticTeam`
- `SavedSnapshotAsset`
- `SnapshotJson`
- `RemoteDeckRef` stub

`RemoteDeckRef`는 현재 live 호출을 하지 않고, deck reference + cached snapshot seam만 남긴다.

## Town에서 smoke 실행

`SM/Play/Full Loop` 메뉴로 normal playable을 띄운 뒤 Town에서도 smoke를 실행할 수 있다.

1. `SM/Play/Full Loop`
2. `Boot.unity` Play
3. Town에서 secondary `Quick Battle (Smoke)`
4. Battle 종료 후 `Continue` 또는 direct `Return to Town (Debug)`
5. Reward를 택했다면 카드 선택
6. `Return Town`

이 경로는 Town UI smoke 확인용이며, `Start Expedition / Resume Expedition` acceptance를 대체하지 않는다.
또한 smoke 종료 후에는 canonical Town 상태가 disk 기준으로 복원되어야 한다.

## RC lane note

- Quick Battle smoke는 RC blocking floor다.
- 하지만 canonical newcomer lane이나 normal loop acceptance를 대체할 수는 없다.

## 참고 문서

- normal playable acceptance: [docs/05_setup/local-runbook.md](./local-runbook.md)
- sandbox authoring 구조: [docs/03_architecture/editor-sandbox-tooling.md](../03_architecture/editor-sandbox-tooling.md)
- runtime hardening contract: [docs/06_production/runtime-hardening-contract.md](../06_production/runtime-hardening-contract.md)
