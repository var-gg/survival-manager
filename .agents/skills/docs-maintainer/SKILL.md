---
name: docs-maintainer
description: 저장소 문서의 생성, 정리, 인덱스 동기화, lifecycle 정리, 검증 루프를 수행한다.
---

# docs-maintainer

## 목적

이 스킬은 저장소 문서를 만들거나 고칠 때, 문서가 실제 구현·정책·인덱스와 함께 움직이도록 유지하는 데 사용한다.
특히 docs harness, deprecated 정리, 한국어 durable docs 정책, index 동기화, validation loop를 같이 처리하는 작업에 적용한다.

## 반드시 사용하는 경우

- `docs/**`, `prompts/**`, `.agents/skills/**`, `tasks/**`를 수정하는 작업
- 문서 구조, 수명주기, 언어 정책, index 체계, deprecated 정리를 바꾸는 작업
- 구현 변경 후 관련 문서, 관련문서 링크, index를 같이 맞추는 작업
- source-of-truth matrix가 필요한 문서군을 정리하는 작업

## 적용 대상

- 새 저장소 문서 작성
- 기존 문서 갱신
- broken/outdated link 수정
- 폴더 `index.md`와 문서 map 갱신
- governance, setup, architecture, production 문서 정리
- deprecated 문서 lifecycle 정리와 registry 갱신

## 적용 제외

- gameplay 구현 자체
- 문서 목적 없는 복잡한 자동화 스크립트 작성
- 문서화 목적 없이 Unity project configuration만 바꾸는 일
- `Assets/ThirdParty` 원본 수정
- 승인 없는 제품 방향 결정

## 작업 절차

### 1. 시작 컨텍스트를 작게 잡는다

문서 작업은 아래 순서로 시작한다.

1. `AGENTS.md`
2. `docs/index.md`
3. 관련 폴더 `index.md`
4. 현재 task `status.md`

필요한 세부 문서는 이 체인을 읽은 뒤 범위와 직접 연결된 active source만 on-demand로 연다.
모든 Markdown 파일을 기본 컨텍스트에 병합하지 않는다.

### 2. 문서 역할을 먼저 분류한다

- `docs/**`: durable knowledge
- `tasks/**`: live state / handoff
- `prompts/**`, `.agents/skills/**`: agent routing asset
- `docs/04_decisions/**`: durable decision

같은 주제를 다뤄도 역할이 다르면 삭제보다 역할 분리를 우선한다.

### 3. source-of-truth 우선순위를 확인한다

- A: `AGENTS.md`, ADR, active index, 현재 task `status.md`
- B: 현재 작업과 직접 관련된 active design/setup/architecture 문서
- C: `draft` 문서
- D: deprecated, archive, legacy, historical memo

`D` 계층은 기본 검색 대상에서 제외한다.

### 4. 문서와 index를 같이 움직인다

문서를 수정할 때는 아래를 같은 작업 단위에서 확인한다.

- 대상 문서 본문
- 해당 폴더 `index.md`
- `관련문서` 링크
- 같은 주제를 다루는 기준 문서
- 필요 시 task `status.md`

### 5. deprecated lifecycle을 따른다

deprecated 처리 시 아래 순서를 지킨다.

1. replacement 문서 또는 ADR 확정
2. active index에서 제거
3. `docs/00_governance/deprecated-docs-registry.md` 갱신
4. 원본 deprecated 파일 삭제

원본 파일에 폐기 이유를 영구 누적하지 않는다.

### 6. 한국어 durable docs 정책을 지킨다

- `docs/**`, `AGENTS.md`, human-facing `tasks/**` 본문/메타데이터는 한국어를 기본으로 유지한다.
- 파일명, 코드, 명령어, API 식별자는 영어를 유지한다.
- routing asset은 durable docs와 별도 분류하지만, 사람에게 읽히는 본문은 가능하면 한국어를 유지한다.

### 7. validation loop를 수행한다

문서 구조나 정책이 바뀌면 최소 아래를 수행한다.

```powershell
pwsh -File tools/docs-policy-check.ps1 -RepoRoot .
pwsh -File tools/docs-check.ps1 -RepoRoot .
pwsh -File tools/smoke-check.ps1 -RepoRoot .
```

병렬화가 필요하면 deprecated inventory, language audit, index coverage audit 같은 read-only 조사에 한정한다.
실제 쓰기와 최종 통합은 한 작업 단위에서 처리한다.

### 8. handoff 체크리스트를 강제한다

task 실행 문서를 닫기 전에는 아래를 모두 확인한다.

- `spec.md`에 최소 `Goal`, `Authoritative boundary`, `In scope`, `Out of scope`, `asmdef impact`, `persistence impact`, `validator / test oracle`, `done definition`, `deferred`가 있다.
- `plan.md`에 최소 `Preflight`, `Phase 1 code-only`, `Phase 2 asset authoring`, `Phase 3 validation`, `rollback / escape hatch`, `tool usage plan`, `loop budget`이 있다.
- `implement.md`는 phase별 요약, deviation, blockers, diagnostics, `why this loop happened`를 남기고 미시 `compile -> refresh -> console` 로그를 그대로 나열하지 않는다.
- `status.md`는 `Current state`, `Acceptance matrix`, `Evidence`, `Remaining blockers`, `Deferred / debug-only`, `Loop budget consumed`, `Handoff notes`를 가진다.
- 위 필수 섹션이 하나라도 비어 있거나 누락되면 handoff-ready로 올리지 않는다.
- Unity migration task라면 `compile green`만으로 닫지 않고 validator, targeted test, runtime path oracle 근거까지 남긴다.
- oversized umbrella task는 parent status만으로 닫지 않고 child phase 문서로 split closure한다.

### 9. index 동기화를 mandatory로 본다

다음 범주를 건드렸으면 관련 index를 같은 작업 단위에서 함께 갱신한다.

- `docs/**` 전반 구조 변경: `docs/index.md`
- design/architecture 역할 경계 변경: `docs/02_design/index.md`, `docs/03_architecture/index.md`
- task template / task contract 변경: `docs/00_governance/task-execution-pattern.md`
- source-of-truth 역할이 바뀐 경우: `docs/00_governance/source-of-truth-matrix.md`

index sync가 빠진 문서 작업은 완료로 보지 않는다.

## 가드레일

- 문서 변경은 실제 저장소 상태와 맞아야 한다.
- repository-relative 링크와 안정적인 구조를 우선한다.
- 구조나 정책이 바뀌면 관련 문서를 같은 작업 단위에서 갱신한다.
- `tools/docs*.ps1`, `tools/smoke-check.ps1`, `.github/workflows/**`처럼 하네스 enforcement를 바꾸는 작업도 문서 하네스 변경으로 취급한다.
- active index에 deprecated pointer를 남기지 않는다.
- active index에서 `deprecated-docs-registry.md`를 직접 노출하지 않는다.
- `Assets/ThirdParty` 원본은 수정하지 않는다.
- Unity repo 운영 계약 문서는 선언문이 아니라 preflight, 금지사항, acceptance, escalation까지 내려와야 한다.
- asset phase와 code phase를 섞은 task는 문서에서 split plan을 먼저 남긴다.

## 기대 산출물

- 새로 작성되거나 갱신된 Markdown 문서
- 정리된 index, 관련문서 링크, source-of-truth 표
- registry 기반 deprecated cleanup
- 검증 결과가 남은 task `status.md`
- 다음 턴 에이전트가 바로 재사용할 수 있는 task template와 운영 계약
