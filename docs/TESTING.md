# 테스트 하네스 가이드

이 문서는 프로젝트의 테스트 구조, 카테고리 분류, CLI 사용법, lint 정책을 정의한다.

## 테스트 어셈블리

| asmdef | 위치 | 용도 |
| --- | --- | --- |
| `SM.Tests.EditMode` | `Assets/Tests/EditMode/` | FastUnit + GuiSafe 테스트 (기본) |
| `SM.Tests.EditMode.Integration` | `Assets/Tests/EditMode/Integration/` | BatchOnly 테스트 (asset pipeline 필요) |
| `SM.Tests.PlayMode` | `Assets/Tests/PlayMode/` | PlayMode 테스트 |

## 테스트 카테고리

NUnit `[Category]` 어트리뷰트로 분류한다. 클래스 수준에 적용한다.

| 카테고리 | 의미 | GUI 안전 | batchmode 필수 |
| --- | --- | --- | --- |
| `FastUnit` | 순수 C# 로직, Unity asset API 없음 | O | X |
| `BatchOnly` | `RuntimeCombatContentLookup` / `Resources.LoadAll` / editor diagnostic fallback 사용 | X (freeze 위험) | O |

### FastUnit 테스트 작성 규칙

- `FakeCombatContentLookup`을 사용한다 (`ICombatContentLookup` 구현, asset 로딩 없음).
- `new RuntimeCombatContentLookup()`를 직접 호출하지 않는다.
- `[Test]` 어트리뷰트만 사용한다 (`[UnityTest]` 불필요).
- `[Category("FastUnit")]`를 클래스에 적용한다.

```csharp
[Category("FastUnit")]
public sealed class MyFastTests
{
    private static readonly FakeCombatContentLookup Lookup = new();

    [Test]
    public void MyTest()
    {
        var session = new GameSessionState(Lookup);
        // ...
    }
}
```

### BatchOnly 테스트 작성 규칙

- `new RuntimeCombatContentLookup()`를 사용하여 실제 에셋을 로드한다.
- GUI 에디터에서 실행하면 freeze될 수 있다. batchmode에서만 실행한다.
- `[Category("BatchOnly")]`를 클래스에 적용한다.

## ICombatContentLookup 인터페이스

`GameSessionState`와 `ContentTextResolver`는 구체 클래스 대신 `ICombatContentLookup` 인터페이스에 의존한다.

| 구현 | 위치 | 용도 |
| --- | --- | --- |
| `RuntimeCombatContentLookup` | SM.Unity | production — 기본은 `Resources.LoadAll`만 사용, editor recovery fallback은 명시적 opt-in diagnostic lane에서만 허용 |
| `FakeCombatContentLookup` | SM.Tests.EditMode | 테스트 — asset API 없이 빈/최소 데이터 반환 |

`FakeCombatContentLookup` 생성자 옵션:
- `snapshot`: 커스텀 `CombatContentSnapshot` (기본값: 빈 snapshot)
- `firstPlayableSlice`: `RefitItem` 등에서 사용하는 slice 데이터

## CLI 명령어

### canonical / smoke / recovery verbs

```powershell
pwsh -File tools/unity-bridge.ps1 prepare-playable
pwsh -File tools/unity-bridge.ps1 repair-scenes
pwsh -File tools/unity-bridge.ps1 ensure-localization
pwsh -File tools/unity-bridge.ps1 quick-battle-smoke
pwsh -File tools/unity-bridge.ps1 content-validate
pwsh -File tools/unity-bridge.ps1 balance-sweep-smoke
```

- `prepare-playable`이 canonical newcomer/setup lane이다.
- `quick-battle-smoke`는 debug smoke lane이다.
- `bootstrap` verb는 제거됐고, canonical CLI entry는 `prepare-playable` / `quick-battle-smoke` / `seed-content`다.

### GUI 에디터 기반 (unity-cli 필요)

```powershell
pwsh -File tools/unity-bridge.ps1 test-edit                    # 전체 EditMode
pwsh -File tools/unity-bridge.ps1 test-edit -TestFilter "SynergyReachabilityValidatorTests"
pwsh -File tools/unity-bridge.ps1 test-play                    # PlayMode
```

### Batchmode (독립 프로세스, GUI 불필요)

```powershell
pwsh -File tools/unity-bridge.ps1 test-batch-edit               # 전체 EditMode (batchmode)
pwsh -File tools/unity-bridge.ps1 test-batch-fast               # FastUnit 카테고리만
pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestCategory "BatchOnly"  # BatchOnly만
pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter "MySpecificTest"
```

### batchmode 주의사항

- **`-quit`는 `-runTests`와 결합하면 안 된다** — 테스트 완료 전 Unity가 종료될 수 있다.
- `-runSynchronously`는 EditMode 전용이며, `[UnityTest]`/`[UnitySetUp]`/`[UnityTearDown]`을 필터링한다.
- batchmode는 별도 Unity 프로세스를 실행하므로 GUI 에디터와 동시 실행하지 않는다.
- `tools/unity-bridge.ps1 test-batch-fast|test-batch-edit`는 stale `TestResults-Batch.xml`을 성공으로 재사용하지 않는다. fresh 결과가 없으면 project lock failure로 취급한다.

## Preflight Lint

커밋 전 또는 CI에서 실행한다:

```powershell
pwsh -File tools/test-harness-lint.ps1 -RepoRoot .
```

검사 항목:

1. **UnityEditor-in-runtime**: Runtime asmdef 소스에서 `using UnityEditor`가 `#if UNITY_EDITOR` 가드 없이 사용되면 실패.
2. **LoadAll-outside-BatchOnly**: `[Category("BatchOnly")]` 없는 테스트에서 `new RuntimeCombatContentLookup()` 또는 `Resources.LoadAll`을 호출하면 실패.
3. **quit-with-runTests**: 스크립트/문서에서 `-quit`와 `-runTests`를 같이 사용하면 실패.

추가 메모:
- 기본 playable/runtime 경로는 `new RuntimeCombatContentLookup()`의 default mode를 사용한다.
- editor sweep / file fallback이 필요한 검증은 `new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true)`를 명시적으로 사용하고 `BatchOnly`로 고정한다.
- `tools/smoke-check.ps1`는 runtime smoke가 아니라 repo structure preflight다.

## 에이전트 테스트 실행 순서

**`test-batch-fast`가 기본이다.** 모든 에이전트는 코드 변경 후 이 명령을 항상 실행한다.

```powershell
# 1. 기본 (항상 실행) — 57개 FastUnit, ~0.15초, freeze 없음
pwsh -File tools/unity-bridge.ps1 test-batch-fast

# 2. 커밋 전 lint (항상 실행)
pwsh -File tools/test-harness-lint.ps1 -RepoRoot .

# 3. 선택 — 에셋/콘텐츠 변경 시에만
pwsh -File tools/unity-bridge.ps1 test-batch-edit
```

주의:
- `test-batch-fast`와 `test-batch-edit`를 동시에 실행하면 프로젝트 잠금 충돌. 순차 실행한다.
- `test-batch-edit`는 전체 에셋 로딩으로 **10분+, 3GB+ 메모리**가 소모된다. 일상 작업에서는 `test-batch-fast`만 실행한다.

## RC wrapper

paid asset pass 직전 automated floor는 아래 wrapper로 묶는다.

```powershell
pwsh -File tools/pre-art-rc.ps1
```

- 실행 순서: docs preflight -> compile -> batch/edit/play/content -> Loop D shard -> observer report packet
- 출력: `Logs/release-floor/<timestamp>-<shortsha>/manifest.json`, `summary.md`, `town_observer_contract.json`, `battle_observer_contract.json`
- 수동 newcomer witness, normal loop, quick battle smoke, localization, save/load/recovery sign-off는 `summary.md` 체크리스트로 별도 남긴다.
