# CLAUDE.md

이 파일은 Claude Code가 이 저장소에서 작업할 때 따를 운영 기준을 정의한다.
상위 공통 규칙은 `AGENTS.md`에 있으며, 이 문서는 그 규칙을 Claude Code 환경에 맞게 보완한다.

## 최우선 규칙

- **`AGENTS.md`를 먼저 읽는다.** 이 문서와 충돌하면 `AGENTS.md`가 우선한다.
- 저장소 phase는 `prototype`이다. 목표는 목각인형 수준 playable vertical slice 검증이다.
- 지속 문서는 한국어, 파일명/코드/API 식별자는 영어를 유지한다.

## 시작 컨텍스트 순서

작업 시작 시 아래 순서로 읽되, 필요한 것만 on-demand로 연다.

1. `AGENTS.md`
2. `docs/index.md`
3. 관련 폴더 `index.md`
4. 현재 task `status.md` (있으면)

모든 Markdown 파일을 한 번에 읽지 않는다.
product vision, MVP 범위, 게임기획, narrative/lore/visual design은 Pindoc Wiki를 먼저 본다. repo Markdown은 코드 직결 계약, 하네스, setup, 현재 task handoff를 우선한다.

## 구현 허용 범위

- 게임 구현: `Assets/_Game/**`
- 테스트: `Assets/Tests/**`
- 문서/운영: `docs/**`, `tools/**`, 루트 메타파일

## 금지 사항

- `Assets/ThirdParty/**` 원본을 직접 수정하지 않는다.
- 루트 구조를 임의로 변경하지 않는다.
- `static` mutable global state로 세션 truth를 저장하지 않는다.
- `BattleManager`, `GameManager`, `CommonHelper` 같은 god file을 만들지 않는다.
- 이유 없는 interface/abstract class를 추가하지 않는다.
- presentation 계층이 battle truth나 save truth를 직접 생성하지 않는다.
- 캐시, 임시 산출물, 개인 환경 파일을 커밋하지 않는다.

## Git 운영

- 기본 흐름은 `main` 직행이다. 과도한 브랜치 분기를 기본값으로 삼지 않는다.
- 커밋 메시지는 한글로 적절히 정리한다.
- 작업 완료 후 push 여부는 사용자에게 확인한다.

## 워크트리와 Unity 제약

**워크트리에서는 Unity 에디터를 사용할 수 없다.**
Unity 에디터는 메인 프로젝트 경로(`A:\projects\game\survival-manager`)에서만 실행된다.
워크트리에서 `unity-bridge.ps1 compile`, `test-edit`, `test-play` 등을 실행하면 "no Unity instance found" 오류가 발생한다.
컴파일/테스트 검증이 필요하면 **변경 사항을 main에 머지한 후** 메인 프로젝트에서 실행한다.
워크트리에서 실행 가능한 검증: `test-harness-lint.ps1`, `docs-policy-check.ps1`, `docs-check.ps1`.

## Unity 도구 사용

### 필수: 에디터 포그라운드 전환

**unity-bridge.ps1, unity-cli, MCP 등 Unity 에디터와 통신하는 모든 명령 실행 전에 반드시 `focus-unity.ps1`을 먼저 실행한다.**
사용자가 컴퓨터를 사용하면 에디터가 백그라운드로 밀리고, OS가 메시지 펌프 우선순위를 낮춰 에디터가 freeze되거나 커넥터가 응답하지 않는다.

```powershell
pwsh -File tools/focus-unity.ps1            # 반드시 먼저 실행
```text

### MCP 서버 복구

MCP 서버가 죽어있으면 ("No Session" 또는 MCP 도구 응답 없음) 아래 명령으로 복구한다:

```powershell
C:\Users\curioustore\.local\bin\uvx.exe --prerelease explicit --from "mcpforunityserver>=0.0.0a0" mcp-for-unity --transport http --http-url http://127.0.0.1:43157 --project-scoped-tools
```

### unity-bridge.ps1 (기본 경로)

Unity 확인, compile, smoke, report는 wrapper를 먼저 사용한다.

```powershell
pwsh -File tools/unity-bridge.ps1 status    # 에디터 상태 확인
pwsh -File tools/unity-bridge.ps1 compile   # 컴파일
pwsh -File tools/unity-bridge.ps1 console   # 콘솔 로그
pwsh -File tools/unity-bridge.ps1 clear-console
pwsh -File tools/unity-bridge.ps1 test-edit # EditMode 테스트
pwsh -File tools/unity-bridge.ps1 test-play # PlayMode 테스트
pwsh -File tools/unity-bridge.ps1 bootstrap # 부트스트랩
pwsh -File tools/unity-bridge.ps1 seed-content  # 샘플 콘텐츠 생성
pwsh -File tools/unity-bridge.ps1 smoke-observer # 스모크 옵저버
```text

### MCP 사용 기준

- scene/prefab/component/package 구조 편집이나 typed guardrail이 중요한 경우에만 MCP를 사용한다.
- trivial inspect 때문에 MCP tool catalog를 먼저 훑지 않는다.
- Unity MCP 엔드포인트: `http://127.0.0.1:43157/mcp`
- MCP는 editor state 가속 용도로만 사용하고 런타임 의존성은 만들지 않는다.

### 검증 스크립트

```powershell
pwsh -File tools/docs-policy-check.ps1 -RepoRoot .
pwsh -File tools/docs-check.ps1 -RepoRoot .
pwsh -File tools/smoke-check.ps1 -RepoRoot .
```

## 코드 구조 규칙

새 asmdef, 새 public abstraction, validator/report writer/loader/pass 추가, 큰 파일 확장,
`Manager`/`Helper`/`Util`/`Common` 이름 도입, `static` mutable state 추가,
`MonoBehaviour` 책임 확대, content/runtime/persistence truth 혼합이 보이면
구현 전에 구조 검토를 먼저 한다.

### 참조 문서 (필요 시 읽기)

- 코딩 원칙: `docs/03_architecture/coding-principles.md`
- 의존 방향: `docs/03_architecture/dependency-direction.md`
- Unity 경계: `docs/03_architecture/unity-boundaries.md`
- asmdef/persistence 소유권: `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
- 구현 검수 체크리스트: `docs/00_governance/implementation-review-checklist.md`

### 어셈블리 의존 방향 요약

| 어셈블리 | 허용 의존 |
| --- | --- |
| `SM.Core` | 없음 |
| `SM.Content` | `SM.Core` |
| `SM.Combat` | `SM.Core` |
| `SM.Meta` | `SM.Core`, `SM.Content`, `SM.Combat` |
| `SM.Persistence.Abstractions` | `SM.Core`, `SM.Content`, `SM.Meta` |
| `SM.Unity` | Core, Content, Combat, Meta, Persistence.Abstractions, Persistence.Json |
| `SM.Editor` | Core, Content, Combat, Meta, Persistence.Abstractions, Unity |
| `SM.Tests` | 대상 시나리오에 필요한 runtime asmdef |

역방향 의존, 순환 참조는 금지한다.

### 변경 분류 규칙

변경이 `SM.Core`, `SM.Content`, `SM.Combat`, `SM.Meta`, `SM.Persistence.*`, `SM.Unity`, `SM.Editor`, `SM.Tests` 중 어디에 속하는지 먼저 분류한다. 분류가 둘 이상이면 책임 분리를 먼저 제안한다.

## 문서 하네스 규칙

`docs/**`, `prompts/**`, `.agents/skills/**`, `tasks/**`, `tools/docs*.ps1`, `tools/smoke-check.ps1`을 건드리는 작업이면 아래를 따른다.

1. 문서를 수정하면 같은 작업 단위에서 관련 `index.md`, 관련문서 링크를 같이 갱신한다.
2. `status: deprecated` 문서와 index의 deprecated pointer는 active source로 쓰지 않는다.
3. active index에는 active/draft 문서만 남긴다.
4. 문서 구조/정책이 바뀌면 검증 스크립트를 수행한다.

### 문서 역할 분류

- `docs/**`: repo 운영/기술/코드 계약 durable knowledge
- `tasks/**`: live state / handoff
- `prompts/**`, `.agents/skills/**`: agent routing asset
- `docs/04_decisions/**`: durable decision (코드 직결 architecture만)
- pindoc Decision/Analysis: content/governance/narrative/product 결정 (기본 위치)

### 의사결정 위치 정책

`AGENTS.md`의 "의사결정 기록 위치 (ADR vs pindoc Decision)" 절을 반드시 따른다. 핵심 요약:

- **신규 결정 기본 위치**: pindoc Decision/Analysis (`mcp__pindoc__pindoc_artifact_propose`)
- **git ADR 잔류 대상**: 코드 직결 architecture 결정만 (asmdef 경계, persistence schema, build/asset pipeline, runtime architecture)
- **content/governance/narrative/product 결정**: 항상 pindoc 우선
- **1차 brainstorm**: Analysis (`completeness: draft`)로 publish하여 사용자 검토 surface 제공 → 컨펌 후 settled 승급
- **supersede**: pindoc은 `supersede_of` 필드, git ADR은 frontmatter `상태: superseded` + 후속 결정 URL

본 정책 baseline: `pindoc://decision-doc-harness-pindoc-migration`. ADR-0024는 본 정책에 의해 supersede 처리됨.

## Unity 반복 루프 예산

Unity 작업에서 같은 종류의 루프가 반복되면 멈추고 진단한다.

- compile-fix loop: 최대 2회
- `console 읽기 -> refresh -> console 읽기` 반복: 최대 1회
- blind asset authoring 재시도: 최대 1회
- 에디터 강제 복구: 최대 2회 (`AGENTS.md` "Unity 에디터 자동화 안정성" 절차 따름)

budget 초과 시 현재 루프를 멈추고 에러를 요약하여 사용자에게 보고한다.

### compile → test 필수 간격

`AGENTS.md`의 "compile → 후속 작업 간격" 규칙을 반드시 따른다.
compile 직후 test-edit을 바로 호출하지 않는다.

```text
focus-unity → compile → sleep 5s → status (ready 확인) → focus-unity → test-edit
```

### 에디터 멈춤 자율 대응

unity-bridge 명령이 장시간 응답 없으면 아래를 순서대로 수행한다.

1. 별도 Bash 호출로 `Get-Process Unity` → `Responding` 확인
2. `Responding = False`이면 `AGENTS.md` 강제 복구 절차 실행 (Stop-Process → 종료 확인 → Start-Process로 에디터 재시작)
3. 에디터 기동 대기 후 `status`로 ready 확인 (10초 간격, 최대 3분)
4. 재시작 후 compile부터 재개
5. 복구 2회 초과 시 사용자에게 보고하고 중단

**핵심**: 에디터 응답을 무한 대기하지 않는다. 기준 시간을 초과하면 능동적으로 진단한다.

## compile green은 done이 아니다

done은 아래를 모두 포함한다.

- compile error 0
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast` 통과 (기본, 항상)
- `pwsh -File tools/test-harness-lint.ps1` 통과 (커밋 전)
- 관련 validator가 새 변경을 잡는다
- 관련 EditMode/PlayMode test가 최소 하나 존재한다
- 실제 사용자 경로가 한 번은 확인된다
- 어떤 명령과 로그로 확인했는지 기록한다

## Codex 앱과의 공존

이 저장소는 Codex 앱도 현역으로 사용한다.

- `.codex/` 디렉터리는 Codex 전용이다. 건드리지 않는다.
- `.agents/skills/` 디렉터리는 Codex 스킬 정의다. 내용을 참고하되 파일을 수정하지 않는다.
- `tasks/` 폴더의 task 문서는 Codex 워크플로우다. 읽기 참고용으로만 사용한다.
- Codex용 거버넌스 문서(`docs/00_governance/agent-operating-model.md`, `discord-handoff-format.md`)는 Codex 전용 적용범위다.

## 요청 범위 규칙

- 요청과 직접 무관한 리팩터링을 끼워 넣지 않는다.
- 후속 제안이 있어도 자동으로 다음 작업까지 시작하지 않는다.
- 구조 정책 문서화가 필요한 최소 인접 수정은 허용한다.
