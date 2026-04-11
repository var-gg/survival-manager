# AGENTS.md

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

- `docs/`: 설계, 결정, 운영 문서
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
- 구현 변경 시 관련 인덱스와 기준 문서를 함께 갱신한다.
- 플레이어블 vertical slice 검증을 막지 않는 방향으로 설계/구현/운영 문서를 정리한다.
- 기본은 file-first다.
- 기본 git 운영은 `main` 직행이다.
- 신규 개발 단계에서는 과도한 작업 브랜치 분기, 운영 조직처럼 쪼개진 브랜치 운용을 기본값으로 삼지 않는다.
- 작업한 내용은 그때그때 적절한 한글 commit message로 정리하고 `main`으로 push하는 것을 기본 정책으로 본다.
- Unity 확인, compile, smoke, aggregate report는 `pwsh -File tools/unity-bridge.ps1 <verb>`를 먼저 사용한다.
- scene/prefab/component/package 구조 편집이나 typed guardrail이 중요한 경우에만 MCP를 사용한다.
- trivial inspect 때문에 MCP tool catalog를 먼저 훑지 않는다.

## 테스트 하네스 규칙

- 상세 가이드: `docs/TESTING.md`

### 에이전트 기본 테스트 명령 (필수)

**모든 에이전트는 코드 변경 후 아래 명령을 기본으로 실행한다:**

```powershell
pwsh -File tools/unity-bridge.ps1 test-batch-fast   # FastUnit 57개, ~0.15초
```

- 이 명령은 Unity 에디터 없이 독립 실행된다.
- freeze 위험이 없고, GUI 모드에서도 안전하다.
- **이 명령이 기본이다.** 전체 EditMode(`test-batch-edit`)는 필요할 때만 실행한다.

### 에이전트 검증 순서

1. `test-batch-fast` — FastUnit 테스트 (기본, 항상 실행)
2. `tools/test-harness-lint.ps1` — preflight lint 3종 (커밋 전 필수)
3. `test-batch-edit` — 전체 EditMode 포함 BatchOnly (선택, 에셋 변경 시)

### 테스트 카테고리 분류

- 테스트는 `[Category("FastUnit")]` 또는 `[Category("BatchOnly")]`로 분류한다.
- `FastUnit` 테스트는 `FakeCombatContentLookup`을 사용하고, `new RuntimeCombatContentLookup()`를 직접 호출하지 않는다.
- `BatchOnly` 테스트는 batchmode에서만 실행한다 (GUI 에디터에서 freeze 위험).
- `GameSessionState`/`ContentTextResolver`는 `ICombatContentLookup` 인터페이스에 의존한다.
- `-quit`는 `-runTests`와 결합하면 안 된다.
- 두 batchmode 명령을 동시에 실행하면 프로젝트 잠금 충돌이 발생한다. 순차 실행한다.

## Unity 에디터 자동화 안정성

- compile 후 5초 대기 후 다음 명령을 실행한다.
- 에디터 freeze 감지 시: `tools/focus-unity.ps1`로 포커스 시도 → 실패 시 강제 종료 → 재실행 → `tools/wait-unity-ready.ps1`로 복구 확인.
- 강제 복구 예산: 최대 2회. 초과 시 현재 루프를 멈추고 사용자에게 보고한다.
- `RuntimeCombatContentLookup`을 테스트에서 직접 생성하면 `Resources.LoadAll`이 트리거되어 GUI 에디터가 freeze될 수 있다. `FakeCombatContentLookup`을 사용한다.

## 문서 하네스 규칙

- `docs/**`, `prompts/**`, `.agents/skills/**`, `tasks/**`, `tools/docs*.ps1`, `tools/smoke-check.ps1`, `.github/workflows/**`를 건드리는 작업이면 먼저 `$docs-maintainer`를 사용한다.
- 문서 구조, 문서 수명주기, deprecated 정리, 언어 정책, index 체계가 바뀌는 작업이면 task 문서를 먼저 만들고 `status.md`를 handoff 기준으로 유지한다.
- 기본 시작 컨텍스트는 `AGENTS.md` -> `docs/index.md` -> 관련 폴더 `index.md` -> 현재 task `status.md`로 제한한다. 모든 Markdown 파일을 한 번에 읽지 않는다.
- `status: deprecated` 문서와 index의 deprecated pointer는 active source로 쓰지 않는다. replacement, ADR, registry를 우선한다.
- 문서를 수정했으면 같은 작업 단위에서 관련 `index.md`, 관련문서 링크, task `status.md`, 검증 스크립트를 같이 갱신한다.
- `docs/**`와 human-facing task/status 보고는 한국어 본문과 한국어 메타데이터를 유지한다. 파일명, 코드, 명령어, API 식별자는 영어를 유지한다.

## 코드 구조 하네스 규칙

- `Assets/_Game/**`, `Assets/Tests/**`, `Assets/**/*.asmdef`, `Assets/_Game/Scripts/Editor/**`를 건드리는 작업이면 먼저 `$code-structure-guard`를 사용한다.
- 새 asmdef, 새 public abstraction, validator/report writer/loader/pass 추가, 큰 파일 확장, `Manager`/`Helper`/`Util`/`Common` 이름 도입, `static` mutable state 추가, `MonoBehaviour` 책임 확대, content/runtime/persistence truth 혼합이 보이면 구현 전에 구조 검토를 먼저 통과시킨다.
- 코드 구조 변경의 기본 시작 컨텍스트는 `AGENTS.md` -> `docs/index.md` -> `docs/03_architecture/index.md` -> `docs/03_architecture/coding-principles.md` -> `docs/03_architecture/dependency-direction.md` -> `docs/00_governance/implementation-review-checklist.md` -> 현재 task `status.md` 순서를 따른다.
- `MonoBehaviour`, `ScriptableObject`, scene 책임이 걸린 변경이면 `docs/03_architecture/unity-boundaries.md`를 추가로 연다.
- asmdef 경계, persistence ownership, runtime source-of-truth가 걸린 변경이면 `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`를 추가로 연다.
- 코드 변경이 문서 구조나 기준 문서까지 건드리면 `$code-structure-guard`로 구조 리뷰를 먼저 수행하고, 문서 갱신 단계에서 `$docs-maintainer`를 이어서 사용한다.
- 구조 변경인데 관련 `docs/03_architecture/**`, `docs/00_governance/**`, ADR 갱신이 빠져 있으면 완료로 보지 않는다.
