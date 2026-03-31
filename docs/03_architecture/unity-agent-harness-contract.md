# Unity agent harness contract

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/03_architecture/unity-agent-harness-contract.md`
- 관련문서:
  - `docs/03_architecture/unity-editor-iteration-and-asset-authoring.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/03_architecture/validation-and-acceptance-oracles.md`
  - `docs/03_architecture/unity-mcp-tooling-contract.md`
  - `docs/00_governance/task-execution-pattern.md`
  - `tasks/004_launch_floor_catalog_closure/status.md`

## 문서 목적

이 문서는 Unity 기반 저장소에서 에이전트가 큰 작업을 어떻게 shape하고, 무엇을 done으로 판정하며, 어떤 반복 루프에서 중단해야 하는지 정의한다.
핵심은 `무엇을 만들지`가 아니라 `어떻게 닫을지`를 고정하는 것이다.

## 왜 Unity에서는 harness contract가 필요한가

- Unity에서는 작은 수정도 `Asset Database Refresh`, script compile, domain reload, serialized object 복원 비용으로 증폭된다.
- code edit, asset authoring, validator, play mode를 한 루프에 섞으면 사용자는 `라인 하나 고치고 다시 compile`처럼 보지만 실제 비용은 훨씬 크다.
- 따라서 Unity repo에서는 모델 품질보다 task shaping, acceptance oracle, editor state, tooling surface가 더 큰 성패 요인이다.

## 적용 범위

- 새로운 catalog / grammar / runtime source-of-truth / persistence contract / asmdef 경계를 건드리는 작업
- `tasks/<id>_<topic>/` 아래 실행 문서를 사용하는 다중 세션 작업
- compile, refresh, read console, asset generation이 반복 루프로 번질 수 있는 Unity 작업

## task shaping 원칙

### 1. 한 sprint/task는 하나의 authoritative migration axis만 가진다

- 한 task 안에서 동시에 닫을 수 있는 축은 하나다.
- 대표 axis 예시는 다음 중 하나다.
  - content definition + lookup snapshot
  - sample content generation + validator
  - runtime entry source-of-truth 교체
  - persistence contract 추가 또는 교체
  - asmdef refactor
- 아래 조합을 한 sprint에 같이 묶지 않는다.
  - asmdef boundary 변경 + persistence ownership 변경
  - runtime source-of-truth 변경 + asset generator 변경
  - validator 확장 + sample content 대량 authoring + runtime scene 진입점 교체

### 2. oversized umbrella task는 parent tracker로만 남긴다

- umbrella task는 전체 migration을 추적할 수 있다.
- 하지만 parent task 자체는 compile green 하나로 닫지 않는다.
- 실제 closure는 child phase 문서에서 개별 oracle로 닫는다.

### 3. compile green은 done이 아니다

- compile green은 phase gate일 뿐 최종 acceptance가 아니다.
- done은 validator, targeted test, runtime path oracle, evidence 기록까지 포함한다.

## authoritative boundary 규칙

- 모든 `spec.md`는 authoritative boundary를 먼저 적는다.
- boundary에는 아래를 최소 포함한다.
  - 이번 sprint가 교체할 source-of-truth
  - 이번 sprint가 건드리지 않을 축
  - asmdef / persistence 영향 여부
- 작업 도중 새 boundary가 발견되면 즉시 `status.md`에 기록하고 별도 sprint로 분리한다.
- 구현 중간에 asmdef 책임을 다시 자르는 일은 refactor sprint로 승격해야 하며, feature sprint 안에서 자연스럽게 끼워 넣지 않는다.

## done / acceptance oracle 정의

| 항목 | 역할 | done 판정 기준 |
| --- | --- | --- |
| compile | phase gate | compile error 0, 현재 변경 경계가 열리는지 확인 |
| validator | 계약 검증 | 새 catalog/grammar를 validator가 먼저 읽고 drift를 실패로 잡는다 |
| targeted tests | 경계 검증 | 새 기능을 직접 검증하는 EditMode/PlayMode test가 최소 하나 이상 존재한다 |
| runtime path smoke | 진입점 검증 | 실제 사용자가 타는 핵심 경로가 한 번은 확인된다 |
| status evidence | handoff | 어떤 명령과 로그로 위 기준을 확인했는지 `status.md`에 남긴다 |

compile만 통과하고 validator나 runtime path가 비어 있으면 task는 done이 아니다.

## loop budget과 escalation 규칙

### 기본 budget

- 같은 종류의 compile-fix loop: 최대 2회
- 같은 종류의 `Read Console -> Refresh Unity -> Read Console` 반복: 최대 1회
- blind asset authoring 재시도: 최대 1회
- asmdef boundary 변경: feature sprint 안에서는 0회, 별도 refactor sprint에서만 허용

### budget 초과 시 escalation

1. 현재 micro-loop를 멈춘다.
2. `capture_unity_error_digest` 수준의 요약을 남긴다.
3. `status.md`의 `Loop budget consumed`와 `Remaining blockers`를 갱신한다.
4. task를 split하거나 rollback / escape hatch를 적용한다.
5. 같은 세션에서 무의식적으로 다시 같은 루프를 반복하지 않는다.

## anti-pattern 예시

- compile blocker를 고친 뒤 바로 asset generator를 돌리고, 실패하면 다시 compile로 돌아가는 혼합 루프
- validator 없이 sample content부터 대량 생성한 뒤 나중에 drift를 발견하는 순서
- Play Mode인지 확인하지 않고 generator, reserialize, menu execution을 호출하는 것
- asmdef cycle이나 persistence ownership 문제를 구현 절반 이후에야 발견하는 것
- low-level tool 조합으로 editor state machine을 직접 운전하는 것

## current repo symptom에 대한 적용 예시

### asmdef 경계 재절단

- 증상: 구현 중간에 `SM.Meta`와 `SM.Persistence.Abstractions` 관계를 다시 잘라야 했다.
- 계약 적용 시: preflight에서 asmdef impact를 먼저 적고, feature sprint와 refactor sprint를 분리했어야 한다.

### generator의 Play Mode / reserialize 예외

- 증상: `SampleSeedGenerator`와 editor authoring loop가 Play Mode, refresh, serialize 제약과 충돌했다.
- 계약 적용 시: `ensure_edit_mode` preflight와 asset phase 분리가 먼저 있었어야 한다.

### validator 후행 확장

- 증상: `RequiredClassTags`, catalog validator, child asset integrity 문제가 구현 뒤늦게 드러났다.
- 계약 적용 시: 새 catalog/grammar를 읽는 validator failure list를 먼저 만든 뒤 code-only phase로 들어갔어야 한다.

### ArenaSimulationService persistence 결합 제거

- 증상: arena scaffold가 persistence ownership 문제를 feature sprint 안에서 드러냈다.
- 계약 적용 시: arena scaffold는 async service scaffold와 persistence record 변경을 별도 axis로 나눴어야 한다.
