# 문서 하네스와 lifecycle

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `docs/00_governance/docs-harness.md`
- 관련문서:
  - `AGENTS.md`
  - `docs/index.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/00_governance/deprecated-docs-registry.md`
  - `docs/00_governance/source-of-truth-matrix.md`
  - `docs/04_decisions/adr-0017-docs-context-harness.md`

## 목적

이 문서는 에이전트가 Markdown 문서를 어떻게 읽고, 무엇을 기본 컨텍스트에서 제외하며, deprecated 문서를 어떤 lifecycle로 정리하는지 정의한다.
목표는 문서를 더 많이 읽게 만드는 것이 아니라, 더 적게 읽고도 더 정확하게 읽게 만드는 것이다.

## 비목표

- gameplay 설계나 구현 자체를 결정하지 않는다.
- task 진행 로그를 durable docs에 복사하지 않는다.
- prompts/skills를 durable knowledge로 승격하지 않는다.

## 기본 시작 컨텍스트

모든 문서 작업은 아래 순서에서 시작한다.

1. `AGENTS.md`
2. `docs/index.md`
3. 작업 대상 폴더의 `index.md`
4. 현재 작업 `tasks/<id>_<topic>/status.md`

이 체인을 읽은 뒤에도 필요한 경우에만 관련 active source를 추가로 연다.
기본 컨텍스트에 모든 Markdown 파일을 병합하지 않는다.

## 문서 역할 분리

- `docs/**`: durable knowledge
- `tasks/**`: live state, handoff, 실행 상태
- `prompts/**`, `.agents/skills/**`: agent routing asset
- `docs/04_decisions/**`: durable decision

문서 역할이 다르면 같은 주제를 공유해도 삭제 대상이 아니다.
대신 어떤 문서가 정책인지, 어떤 문서가 설치 절차인지, 어떤 문서가 live rollout 상태인지 분리해서 읽는다.

## 신뢰 계층

- A: `AGENTS.md`, ADR, active index, 현재 task `status.md`
- B: 현재 작업과 직접 관련된 active design/setup/architecture 문서
- C: `draft` 문서
- D: deprecated, archive, legacy, pointer, historical memo

D 계층은 기본 검색 대상에서 제외한다.
legacy archaeology가 명시된 경우에만 참고하고, active source로 인용하지 않는다.

## 온디맨드 확장 규칙

1. 시작 컨텍스트를 읽기 전에는 세부 문서를 열지 않는다.
2. 세부 문서는 현재 작업 범위와 직접 연결된 active source만 연다.
3. 겹치는 주제가 보이면 source-of-truth matrix를 먼저 확인한다.
4. 여러 문서를 넓게 합쳐 읽는 대신, 필요한 문서만 계층적으로 늦게 로드한다.

## deprecated lifecycle

deprecated 처리는 아래 순서를 따른다.

1. replacement 문서 또는 ADR을 확정한다.
2. active index에서 해당 문서를 제거한다.
3. `deprecated-docs-registry.md`에 `이전경로`, `대체문서`, `결정기록`, `폐기일`, `remove_by`, `조치`, `이유`를 기록한다.
4. 원본 deprecated 파일은 기본적으로 즉시 삭제한다.
5. 삭제를 늦춰야 하면 최대 7일 grace window만 허용한다.
6. grace 상태라도 active index와 기본 컨텍스트에서는 즉시 제외한다.

개별 deprecated 파일에 폐기 이유를 영구 누적하지 않는다.
폐기 이유는 replacement 문서, ADR, registry 중 하나에 흡수한다.

## active index 규칙

- active index에는 active/draft 문서만 남긴다.
- 실제 존재하지 않는 문서를 나열하지 않는다.
- deprecated pointer, archive link, tombstone link를 active index에 노출하지 않는다.
- central registry는 기준 문서 본문에서만 참조하고, start surface 역할의 index에서는 직접 링크하지 않는다.
- 각 항목은 목적 차이를 짧게 설명하되 본문을 복제하지 않는다.

## source-of-truth matrix 규칙

서로 다른 문서가 같은 주제를 역할 분담해서 다루면 matrix를 만든다.
matrix는 최소 아래 열을 가진다.

- 문서군
- 정책 / 결정
- setup / 운영
- live state
- routing asset
- 비고

삭제보다 역할 분리를 먼저 하고, 역할 분리 후에도 중복되는 durable 설명만 정리한다.

## 검증 루프

문서 구조, 수명주기, 언어 정책, index 체계가 바뀌면 아래 루프를 수행한다.

1. `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
2. touched-file gate면 `pwsh -File tools/docs-check.ps1 -RepoRoot . -Paths <changed-md>`
3. repo-wide debt 확인이 필요할 때만 `pwsh -File tools/docs-check.ps1 -RepoRoot .`
4. `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
5. 결과와 남은 리스크를 task `status.md`에 기록한다.

## 병렬 조사 원칙

문서 작업에서 병렬화가 필요하면 read-only audit에 한정한다.
deprecated inventory, language drift audit, index coverage audit처럼 읽기 중심 조사는 병렬화할 수 있다.
실제 쓰기와 최종 통합은 하나의 작업 단위에서 처리한다.
