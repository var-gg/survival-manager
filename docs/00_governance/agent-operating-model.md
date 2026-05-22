# Codex 에이전트 운영 모델

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-22
- 소스오브트루스: `docs/00_governance/agent-operating-model.md`
- 관련문서:
  - `AGENTS.md`
  - `PINDOC.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/00_governance/task-execution-pattern.md`
  - `docs/00_governance/discord-handoff-format.md`
- 적용범위: Codex 전용

## 목적

이 문서는 Codex가 `survival-manager` 저장소에서 작업할 때 따라야 할 세부 운영 모델을 정의한다.
상위 비가역 규칙은 `AGENTS.md`에 두고, 이 문서는 그 규칙을 실제 작업 절차로 풀어쓴다.

## Pindoc 우선 운영

이 저장소의 기획·설계·창작·의사결정 source-of-truth는 Pindoc Wiki이고, Codex도 Claude Code와 동일하게 이를 따른다. 운영 프로토콜 본체는 루트 `PINDOC.md`에 있다.

- **세션 시작 시 `PINDOC.md`를 직접 읽는다.** `AGENTS.md` 끝의 `@PINDOC.md`는 `@import`를 전개하는 클라이언트용 로딩 경로다. Codex는 import를 전개하지 않으므로 `PINDOC.md` 파일 자체를 열어 읽어야 한다. 이 단계를 건너뛰면 write surface 규칙을 못 보고 repo Markdown으로 빠진다.
- **write surface를 먼저 분류한다.** 기획·설계·narrative/lore·UX·의사결정·다중세션 task는 pindoc artifact(`mcp__pindoc__pindoc_artifact_propose`)로 만든다. 이 범주를 repo Markdown으로 새로 만들지 않는다. 코드 직결 계약·테스트/문서 하네스·setup·`index.md`·코드 ADR만 repo Markdown으로 남긴다.
- **의사결정은 `AGENTS.md`의 결정 매트릭스를 따른다.** asmdef 경계·dependency direction·persistence schema·build/asset pipeline 같은 코드 직결 architecture만 git ADR(`docs/04_decisions/`)이다. narrative·product·governance soft policy·운영 워크플로 결정은 pindoc Decision/Analysis다.
- **다중세션 작업 추적은 pindoc Task artifact다.** `tasks/<id>_<topic>/` 마크다운 4-파일 패턴은 legacy다. 신규 작업은 `PINDOC.md` Task lifecycle을 따른다. 자세한 절차는 `docs/00_governance/task-execution-pattern.md`.
- pindoc artifact 작성 전 Pre-flight Check(`area.list` 확인, `context.for_task`/`artifact.search` 영수증)를 적용한다. 사용자 검토가 필요한 결정은 `completeness: settled`로 발행해 Reader에서 보이게 한다.

## 핵심 운영 규칙

### 1. 문서와 변경을 같이 움직인다

- 구조, 정책, 작업 흐름이 바뀌면 관련 문서를 같은 작업 단위에서 갱신한다.
- 문서 갱신이 빠졌다면 완료로 보고하지 않는다.
- 구현과 문서 중 하나만 바뀐 상태를 정상 상태로 취급하지 않는다.

### 2. 요청 범위를 임의로 넓히지 않는다

- 요청과 직접 무관한 리팩터링을 끼워 넣지 않는다.
- 구조 정책 문서화가 필요한 최소 인접 수정은 허용한다.
- 후속 제안이 있어도 자동으로 다음 작업까지 시작하지 않는다.

### 3. 벤더 경계를 침범하지 않는다

- `Assets/ThirdParty/**` 원본은 직접 수정하지 않는다.
- 필요한 확장은 프로젝트 소유 경로에서 감싸거나 연결한다.
- 벤더 패치가 꼭 필요하면 문서와 승인 경로를 먼저 확인한다.

### 4. 보고와 기록을 분리하지 않는다

- 사람에게 전달하는 진행/완료 보고는 한국어로 작성한다.
- 중요한 다중 세션 작업은 pindoc Task artifact에 남긴다. `tasks/` 마크다운을 새로 만들지 않는다.
- Discord 보고는 해당 pindoc Task / artifact 상태와 모순되지 않게 유지한다.

### 5. 구조 결정은 추적 가능해야 한다

- 의존 방향, asmdef 경계, 저장 규칙, Unity 경계 같은 코드 직결 architecture 결정은 git ADR(`docs/04_decisions/`)로 남긴다.
- narrative·product·governance soft policy·운영 워크플로 결정은 pindoc Decision/Analysis로 남긴다. 분류 기준은 `AGENTS.md`의 결정 매트릭스를 따른다.
- 구조 정책 문서와 결정 기록(ADR 또는 pindoc Decision)이 충돌하면 충돌을 바로 해소한다.

### 6. 기본 git 흐름은 direct-to-main이다

- 현재 저장소 phase는 `prototype`이며, 기본 git 흐름은 `main` 직행이다.
- 작업 브랜치를 만드는 행위 자체를 금지하지는 않지만, 운영 조직처럼 과도하게 쪼개진 브랜치 흐름을 기본값으로 두지 않는다.
- 구현이 끝난 작업은 그때그때 적절한 한글 commit message로 정리하고 `main`까지 push하는 것을 우선한다.
- 별도 브랜치가 이미 열려 있어도, 완료 시점에는 불필요하게 오래 유지하지 말고 `main` 반영을 기본 종료 조건으로 본다.

## 문서 갱신이 필수인 변화

- 저장소 폴더 구조 변경
- asmdef 또는 namespace 경계 변경
- Boot/scene 흐름 변경
- persistence 정책 변경
- 에이전트 작업 절차 변경
- 보고 형식 변경
- 벤더 에셋 처리 정책 변경

## 완료 기준

다음이 모두 맞아야 작업이 정리된 것으로 본다.

- 요청된 변경이 반영됨
- 직접 영향받는 문서가 같이 갱신됨
- 불필요한 범위 확장이 없음
- `Assets/ThirdParty/**` 경계를 침범하지 않음
- 남은 리스크나 후속 결정이 명시됨
