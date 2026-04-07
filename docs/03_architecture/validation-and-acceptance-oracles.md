# Validation and acceptance oracles

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `docs/03_architecture/validation-and-acceptance-oracles.md`
- 관련문서:
  - `docs/03_architecture/content-authoring-and-balance-data.md`
  - `docs/03_architecture/testing-strategy.md`
  - `docs/03_architecture/unity-agent-harness-contract.md`
  - `docs/05_setup/unity-long-running-workloads.md`
  - `tasks/_templates/status.md`

## 목적

이 문서는 Unity task에서 `무엇이 done인가`를 compile 성공이 아니라 validator, targeted test, runtime smoke 조합으로 정의한다.

## validator-first 원칙

- 새 catalog, grammar, canonical id, tag family를 추가하면 validator를 먼저 확장한다.
- validator failure list 없이 구현부터 시작하지 않는다.
- sample content generation이나 manual authoring은 validator가 읽을 수 있는 상태가 된 뒤에만 진행한다.
- validator가 아직 새 schema를 모르면 해당 task는 아직 code-only preflight를 통과한 것이 아니다.

## acceptance matrix 템플릿

| 항목 | 요구 | 통과 기준 예시 | 상태 기록 위치 |
| --- | --- | --- | --- |
| compile | phase gate | 관련 asmdef compile error 0 | `status.md > Acceptance matrix` |
| validator | 계약 검증 | 새 catalog/grammar drift 0 | `status.md > Acceptance matrix`, report 경로 |
| targeted tests | 경계 검증 | 새 서비스 또는 진입점 대상 EditMode/PlayMode test 통과 | `status.md > Evidence` |
| asset integrity | authoring 검증 | sample content / generated asset 경로와 참조 무결성 확인 | `status.md > Evidence` |
| runtime smoke | 사용자 경로 검증 | 해당 기능이 실제 경로에서 한 번 동작 | `status.md > Evidence` |

## compile / validator / targeted tests / runtime smoke의 관계

- compile은 문을 여는 단계다.
- validator는 계약 drift를 잡는 단계다.
- targeted tests는 코드 경계를 검증하는 단계다.
- runtime smoke는 실제 사용자 경로를 확인하는 단계다.

네 단계는 서로 대체재가 아니다.
특히 compile 성공으로 validator와 runtime smoke를 생략할 수 없다.

## feature closure와 compile blocker closure의 차이

- compile blocker closure: 현재 편집이 다시 빌드되게 만들었다는 뜻
- feature closure: behavior contract가 validator, test, smoke까지 통과했다는 뜻

compile blocker를 해결한 직후에는 feature closure를 주장하지 않는다.
feature closure를 주장하려면 status 문서에 evidence가 있어야 한다.

## status.md에 남겨야 하는 evidence

- 실행한 명령
- 사용한 validator / test 이름
- 핵심 로그 또는 report 경로
- 미해결 blocker와 deferred 항목
- loop budget 초과 여부와 이유

evidence가 없으면 구두 보고만으로 task를 닫지 않는다.

## 운영 메모

- broad smoke보다 targeted oracle을 우선한다.
- runtime smoke가 아직 비싸면 validator와 targeted tests를 먼저 닫고, smoke를 별도 phase gate로 분리한다.
- child phase 문서마다 별도의 acceptance matrix를 갖는 것을 기본으로 한다.

## 장시간 oracle 분리 규칙

- targeted EditMode/PlayMode test는 경계 검증용으로 짧게 유지한다.
- multi-minute deterministic balance sweep, large artifact generation, 반복 scenario smoke는 일반 `test-edit`
  기본 경로에 섞지 않는다.
- 이런 oracle은 shardable manual lane 또는 custom CLI tool로 분리하고, task `status.md`에 evidence 경로를 남긴다.
- long-running suite를 default test lane에 넣어 Unity Test Runner callback을 장시간 점유하게 만들면,
  transport busy와 테스트 실패를 구분하기 어려워지므로 acceptance oracle로 보지 않는다.

## pre-art RC floor

pre-art floor에서는 아래를 같은 SHA blocking set으로 묶는다.

- docs preflight: `docs-policy-check`, repo structure `smoke-check`
- compile: `pwsh -File tools/unity-bridge.ps1 compile`
- targeted tests: `test-batch-fast`, `test-batch-edit`, `test-play`
- validator / asset integrity: `content-validate`, `balance-sweep-smoke`
- runtime/report evidence: `prepare-playable`, `report-town`, `report-battle`
- long-running shard evidence: `loopd-slice`, `loopd-purekit`, `loopd-systemic`, `loopd-runlite`

중요:

- `tools/smoke-check.ps1`는 runtime smoke가 아니라 repo structure preflight다.
- compile green만으로 RC floor를 통과했다고 보지 않는다.
- automated floor 결과는 `tools/pre-art-rc.ps1` packet과 task `status.md` 둘 다에 남긴다.
