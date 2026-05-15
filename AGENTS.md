# AGENTS.md

## Pindoc

<!-- pindoc:register-separation:v1 BEGIN -->
### Register 분리 — 사용자 대화와 Pindoc artifact

Pindoc artifact 본문(Context / Decision / Rationale / Alternatives / Consequences 같은 섹션)은 구조화된 register에 속해서 표·bullet·ADR 스타일 축약이 자연스럽다. 반면 사용자 대면 응답은 추론 흐름이 연결된 산문 register에 속한다. "Alt A(...) · B(...)" 같은 축약, 괄호 안 한 단어 기각 이유, 중점(·)으로 나열한 짧은 구절이 artifact 본문에서 대화로 역류하지 않게 한다. 애매할 때는 응답을 두세 문장 산문으로 다시 쓰며 추론을 압축이 아닌 서술로 노출한다.
<!-- pindoc:register-separation:v1 END -->

이 문서는 이 저장소에서 작업하는 사람과 에이전트가 공통으로 따를 최소 운영 원칙만 정의한다.

## 저장소 목적

- Unity 기반 게임 프로젝트의 루트형 저장소를 유지한다.
- 현재 목표는 **목각인형 수준 playable vertical slice**를 빠르게 검증하는 것이다.
- 저장소 phase는 `skeleton`이 아니라 `prototype`이다.

## 구현 허용 범위

- 게임 구현 파일: `Assets/_Game/**`
- 테스트 파일: `Assets/Tests/**`
- 문서/운영 파일: `docs/**`, `tools/**`, 기타 루트 메타파일

## 작업 우선순위

1. 실제 구현 상태와 문서를 맞춘다.
2. 문서 목적을 분리한다.
3. 변경 이유를 기록한다.
4. 플레이어블 vertical slice에 직접 기여하는 작업을 우선한다.

## 디렉터리 책임

- `docs/`: repo 운영, 기술 구조, 코드/콘텐츠 계약, setup 문서
- `prompts/`: 자동화 및 에이전트 프롬프트
- `tools/`: 개발 보조 도구
- `Assets/`: Unity 에셋과 게임 코드
- `Packages/`: 패키지 선언 파일
- `ProjectSettings/`: Unity 설정 파일

## 금지 사항

- 루트 구조를 임의로 변경하지 않는다.
- 목적이 다른 내용을 한 문서에 섞지 않는다.
- 캐시, 임시 산출물, 개인 환경 파일을 커밋하지 않는다.
- 구현 범위 밖에 게임 로직을 흩뿌리지 않는다.
- `static` mutable global state로 세션 truth를 저장하지 않는다. Enter Play Mode Options(Domain/Scene Reload 스킵)가 활성화되어 있어 Play 진입 시 static 필드가 초기화되지 않는다.

## 현재 단계 규칙

- 지속 문서는 한국어로 유지한다.
- 파일명, 코드, API 식별자는 영어를 유지한다.
- product vision, MVP 범위, 게임기획, 창작/narrative/lore/visual design의 기본 source-of-truth는 Pindoc Wiki다. repo Markdown은 코드 직결 계약, 하네스, setup, 현재 task handoff에 한정해 active source로 쓴다.
- 구현 변경 시 관련 인덱스와 기준 문서를 함께 갱신한다.
- 플레이어블 vertical slice 검증을 막지 않는 방향으로 설계/구현/운영 문서를 정리한다.
- 기본은 file-first다.
- 기본 git 운영은 `main` 직행이다.
- 신규 개발 단계에서는 과도한 작업 브랜치 분기, 운영 조직처럼 쪼개진 브랜치 운용을 기본값으로 삼지 않는다.
- 작업한 내용은 그때그때 적절한 한글 commit message로 정리하고 `main`으로 push하는 것을 기본 정책으로 본다.
- Unity 확인, compile, smoke, aggregate report는 `pwsh -File tools/unity-bridge.ps1 <verb>`를 먼저 사용한다.
- scene/prefab/component/package 구조 편집이나 typed guardrail이 중요한 경우에만 MCP를 사용한다.
- trivial inspect 때문에 MCP tool catalog를 먼저 훑지 않는다.

## 의사결정 기록 위치 (ADR vs pindoc Decision)

본 저장소의 의사결정 기록은 두 곳으로 분리되어 있다. 잘못된 위치 선택은 retcon 잠금 또는 검색 누락을 유발하므로 결정 유형에 따라 위치를 결정한다.

### pindoc Decision/Analysis (기본)

신규 의사결정의 **기본 위치**다. content 결정, narrative 결정, governance soft policy, 운영 규칙, product vision/MVP 범위, 사용자 검토가 필요한 모든 결정은 pindoc Decision으로 직접 작성한다.

도구: `mcp__pindoc__pindoc_artifact_propose` (`type: Decision` 또는 `type: Analysis`).

작성 패턴:

1. (선택) 1차 brainstorm을 `Analysis` (`completeness: draft`)로 publish하여 사용자 검토 surface 제공
2. 사용자 컨펌 후 `Decision` (`completeness: settled`)로 발행 또는 Analysis를 settled 승급
3. 본문 H2 필수 섹션: TL;DR / Context / Decision / Rationale / Alternatives considered / Consequences

### git ADR (`docs/04_decisions/`)

코드 직결 architecture 결정만. asmdef 경계, dependency direction, persistence schema, build pipeline, asset pipeline 등 pindoc 다운/이전 영향 없이 항상 읽혀야 하는 결정.

기준: 코드 변경 PR 검토 시 ADR 본문이 반드시 함께 읽혀야 하는 결정만 ADR. 그 외는 pindoc.

### 결정 위치 매트릭스

| 결정 유형 | 위치 |
| --- | --- |
| asmdef/asmref 경계, dependency direction | git ADR |
| 코드 layer/runtime architecture | git ADR |
| persistence schema, save migration | git ADR |
| asset pipeline, build pipeline | git ADR |
| narrative 메커니즘, character lore, 세계관 | pindoc Decision/Analysis |
| product vision, MVP 범위, design pillars | pindoc Decision/Analysis |
| governance soft policy (review checklist 등) | pindoc Decision |
| 운영 워크플로 (sub-agent skills, harness 정책) | pindoc Decision |

### supersede 처리

- pindoc Decision의 supersede는 `supersede_of` 필드 사용
- git ADR의 supersede는 ADR 본문 frontmatter에 `상태: superseded` + 후속 결정 URL 명시
- pindoc Decision이 git ADR을 supersede할 수도 있음 (ADR-0024 케이스 — git ADR 본문에 후속 pindoc Decision URL 명시)

본 정책의 baseline: `pindoc://decision-doc-harness-pindoc-migration`.

## 변경 라우팅 Quick Reference

## Editor-free closure scope

- 현재 닫힌 범위는 `SM.Core`, `SM.Combat`, `SM.Meta`, `SM.Meta.Serialization`, `SM.Persistence.Abstractions` 같은 pure asmdef boundary와 `SM.Tests.FastUnit` 전용 asmdef의 `FastUnit` editor-free/resource-free/authored-object-free lane이다. pure asmdef 참조는 `BuildBoundaryGuardFastTests` exact allowlist로 고정한다.
- `SM.Unity`, `SM.Unity.ContentConversion`, `RuntimeCombatContentLookup`, `NarrativeRuntimeBootstrap`, `GameSessionRuntimeBootstrapProvider`, `GameSessionState` production constructor, UI/controller/scene/prefab authoring은 repo-wide pure boundary 안쪽으로 들어온 것이 아니다. 이들은 boundary adapter 또는 `BatchOnly`/PlayMode/editor-required lane으로 라우팅한다.
- `SM.Unity.ContentConversion`은 현재 별도 asmdef가 아니라 `SM.Unity` 내부 authored-to-runtime converter 폴더 경계다. 이 폴더는 public API, session/persistence/UI ownership, 임의 asset loading을 갖지 않으며 `ContentDefinitionRegistry`만 resource/editor fallback choke point를 소유한다.
- 따라서 “editor-free boundary closed”라고 쓸 때는 항상 `pure asmdef + FastUnit lane` 범위로 한정한다. repo 전체, `SM.Unity`, authored content, UI loop까지 완전 분리됐다는 뜻으로 쓰지 않는다.

| 변경 유형 | 첫 위치 | 기본 검증 | 승격 기준 |
| --- | --- | --- | --- |
| 전투 규칙/수치/판정 | `Assets/_Game/Scripts/Runtime/Combat/**` | `test-batch-fast` | authored content나 scene 확인이 필요하면 BatchOnly/content lane |
| reward/passive/loot/progression 규칙 | `Assets/_Game/Scripts/Runtime/Meta/**` | `test-batch-fast` | session 적용이나 UI 표시가 필요하면 `SM.Unity.Session` |
| narrative runtime spec/service | `SM.Meta` pure story/spec model | `test-batch-fast` | authored definition 변환이 필요하면 `SM.Unity.ContentConversion` |
| authored content schema/definition | `Assets/_Game/Scripts/Runtime/Content/**` | `content-validate` 또는 BatchOnly focused | `SM.Meta` 직접 참조 금지, adapter 경유 |
| authored -> runtime 변환 | `Assets/_Game/Scripts/Runtime/Unity/ContentConversion/**` | BatchOnly focused + `test-harness-lint` | 내부 converter 경계만 유지, `Resources.Load*`는 registry choke point 안에만 유지 |
| session flow/facade | `Assets/_Game/Scripts/Runtime/Unity/Session/**`, `GameSessionState` facade | `test-batch-fast` + focused session tests | public facade 변경이나 UI migration이 필요하면 중단 후 task 분리 |
| UI/presentation/controller | `Assets/_Game/Scripts/Runtime/Unity/**` UI/presenter 경계 | focused EditMode 또는 PlayMode smoke | gameplay truth가 필요하면 `SM.Meta`/`SM.Combat`로 내려서 분리 |
| persistence contract/serializer | `SM.Persistence.Abstractions`, `SM.Persistence.Json` | `test-batch-fast` + persistence focused | save migration/compatibility가 필요하면 ADR/task 기록 |
| docs/harness/guard | `docs/**`, `tasks/**`, `tools/**`, `Assets/Tests/**` | docs scripts, lint, focused guard | 구조 정책이 바뀌면 architecture docs와 task status 동시 갱신 |

## 테스트 하네스 규칙

- 상세 가이드: `docs/TESTING.md`

### 에이전트 기본 테스트 명령 (필수)

**모든 에이전트는 코드 변경 후 아래 명령을 기본으로 실행한다:**

```powershell
pwsh -File tools/unity-bridge.ps1 test-batch-fast   # FastUnit 카테고리만 실행, 테스트 수와 시간은 변동 가능
```

- 이 명령은 Unity 에디터 없이 독립 실행된다.
- freeze 위험이 없고, GUI 모드에서도 안전하다.
- **이 명령이 기본이다.** 전체 EditMode(`test-batch-edit`)는 필요할 때만 실행한다.

### 에이전트 검증 순서

1. `test-batch-fast` — FastUnit 테스트 (기본, 항상 실행)
2. `tools/test-harness-lint.ps1` — preflight lint (커밋 전 필수)
3. `test-batch-edit` — 전체 EditMode 포함 BatchOnly (선택, 에셋 변경 시)

### 테스트 카테고리 분류

- 테스트 class는 class-level `[Category("FastUnit")]` 또는 `[Category("BatchOnly")]`로 분류한다. 장시간 Loop D balance/telemetry 수동 검증만 `[Category("ManualLoopD")]` 예외를 허용한다. symmetric mirror 4v4 timeout/draw policy 관찰도 FastUnit closure가 아니라 `ManualLoopD` 추적 대상이다.
- `FastUnit` 테스트는 `Assets/Tests/EditMode/FastUnit/**`의 `SM.Tests.FastUnit` asmdef에 둔다. 이 asmdef는 EditMode 실행을 위해 `Editor` platform target을 쓰지만 `SM.Editor`와 editor-only package를 참조하지 않는다.
- `FastUnit` 테스트는 editor-free/resource-free/authored-object-free lane이다. `FakeCombatContentLookup`, `GameSessionTestFactory`, pure `CombatContentSnapshot`/spec fixture를 사용하고, test class 본문에서 `ScriptableObject.CreateInstance`, `SM.Content.Definitions`, `Resources.Load*`, `new RuntimeCombatContentLookup()`, public `new GameSessionState(...)`를 직접 호출하지 않는다. alias, static import, wrapper method로 숨기는 형태도 금지한다.
- `SM.Editor.Validation` 등 editor assembly를 직접 검증하는 테스트는 빠르더라도 `BatchOnly`로 분류한다.
- `BatchOnly` 테스트는 authored Unity object, `RuntimeCombatContentLookup`, `Resources.LoadAll`, editor diagnostic fallback을 검증할 때 사용하고 batchmode에서만 실행한다 (GUI 에디터에서 freeze 위험).
- `GameSessionState`/`ContentTextResolver`는 `ICombatContentLookup` 인터페이스에 의존한다.
- `-quit`는 `-runTests`와 결합하면 안 된다.
- 두 batchmode 명령을 동시에 실행하면 프로젝트 잠금 충돌이 발생한다. 순차 실행한다.

## Unity 에디터 자동화 안정성

- compile 후 5초 대기 후 다음 명령을 실행한다.
- 에디터 freeze 감지 시: `tools/focus-unity.ps1`로 포커스 시도 → 실패 시 강제 종료 → 재실행 → `tools/wait-unity-ready.ps1`로 복구 확인.
- 강제 복구 예산: 최대 2회. 초과 시 현재 루프를 멈추고 사용자에게 보고한다.
- `ScriptableObject.CreateInstance`, `RuntimeCombatContentLookup`, public `GameSessionState` 생성자를 테스트에서 직접 사용하면 Unity object lifecycle 또는 `Resources.LoadAll` 경로가 트리거될 수 있다. FastUnit에서는 pure fixture, `FakeCombatContentLookup`, `GameSessionTestFactory`를 사용한다.

## 문서 하네스 규칙

- `docs/**`, `prompts/**`, `.agents/skills/**`, `tasks/**`, `tools/docs*.ps1`, `tools/smoke-check.ps1`, `.github/workflows/**`를 건드리는 작업이면 먼저 `$docs-maintainer`를 사용한다.
- 문서 구조, 문서 수명주기, deprecated 정리, 언어 정책, index 체계가 바뀌는 작업이면 task 문서를 먼저 만들고 `status.md`를 handoff 기준으로 유지한다.
- 기본 시작 컨텍스트는 `AGENTS.md` -> `docs/index.md` -> 관련 폴더 `index.md` -> 현재 task `status.md`로 제한한다. 모든 Markdown 파일을 한 번에 읽지 않는다.
- 게임기획/제품/창작 방향을 찾을 때는 repo의 오래된 `docs/01_product/**` 또는 creative `docs/02_design/**` 사본을 복원해 읽지 말고 Pindoc context/search를 먼저 사용한다.
- `status: deprecated` 문서와 index의 deprecated pointer는 active source로 쓰지 않는다. replacement, ADR (또는 pindoc Decision), registry를 우선한다.
- 문서를 수정했으면 같은 작업 단위에서 관련 `index.md`, 관련문서 링크, task `status.md`, 검증 스크립트를 같이 갱신한다.
- `docs/**`와 human-facing task/status 보고는 한국어 본문과 한국어 메타데이터를 유지한다. 파일명, 코드, 명령어, API 식별자는 영어를 유지한다.

## 코드 구조 하네스 규칙

- `Assets/_Game/**`, `Assets/Tests/**`, `Assets/**/*.asmdef`, `Assets/_Game/Scripts/Editor/**`를 건드리는 작업이면 먼저 `$code-structure-guard`를 사용한다.
- 새 asmdef, 새 public abstraction, validator/report writer/loader/pass 추가, 큰 파일 확장, `Manager`/`Helper`/`Util`/`Common` 이름 도입, `static` mutable state 추가, `MonoBehaviour` 책임 확대, content/runtime/persistence truth 혼합이 보이면 구현 전에 구조 검토를 먼저 통과시킨다.
- 코드 구조 변경의 기본 시작 컨텍스트는 `AGENTS.md` -> `docs/index.md` -> `docs/03_architecture/index.md` -> `docs/03_architecture/coding-principles.md` -> `docs/03_architecture/dependency-direction.md` -> `docs/00_governance/implementation-review-checklist.md` -> 현재 task `status.md` 순서를 따른다.
- `MonoBehaviour`, `ScriptableObject`, scene 책임이 걸린 변경이면 `docs/03_architecture/unity-boundaries.md`를 추가로 연다.
- asmdef 경계, persistence ownership, runtime source-of-truth가 걸린 변경이면 `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`를 추가로 연다.
- 코드 변경이 문서 구조나 기준 문서까지 건드리면 `$code-structure-guard`로 구조 리뷰를 먼저 수행하고, 문서 갱신 단계에서 `$docs-maintainer`를 이어서 사용한다.
- 구조 변경인데 관련 `docs/03_architecture/**`, `docs/00_governance/**`, ADR (또는 해당 영역의 pindoc Decision) 갱신이 빠져 있으면 완료로 보지 않는다.

@PINDOC.md
