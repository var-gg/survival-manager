# 문서 거버넌스

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/00_governance/docs-governance.md`
- 관련문서:
  - `docs/index.md`
  - `docs/00_governance/docs-harness.md`
  - `docs/00_governance/deprecated-docs-registry.md`
  - `docs/00_governance/source-of-truth-matrix.md`
  - `docs/00_governance/index.md`
  - `docs/00_governance/naming-conventions.md`
  - `docs/04_decisions/adr-0001-docs-architecture.md`
  - `docs/04_decisions/adr-0017-docs-context-harness.md`

## 목적

이 문서는 `survival-manager` 저장소의 지속 문서를 어떻게 만들고, 어디에 두고, 어떤 기준으로 갱신하는지 정의한다.
목표는 사람이든 AI든 문서 체계를 흔들지 않고 같은 기준으로 문서를 유지하게 만드는 것이다.

## 비목표

- 게임 디자인 자체를 결정하지 않는다.
- 구현 세부를 대신 설명하지 않는다.
- 임시 메모나 세션 로그의 저장 규칙을 정의하지 않는다.

## 문서 배치 기준

- `docs/00_governance/`: 운영 규칙, 체크리스트, 문서 정책, 에이전트 운영 절차
- `docs/01_product/`: 제품 목표, 범위, 비목표, 타깃 사용자
- `docs/02_design/`: 시스템/전투/UI/메타 디자인
- `docs/03_architecture/`: 기술 구조, 코딩 경계, 의존 방향, 데이터/런타임 분리
- `docs/04_decisions/`: durable decision을 기록하는 ADR
- `docs/05_setup/`: 도구 설치, 환경 구성, 로컬 실행 절차
- `docs/06_production/`: 운영 체크, 플레이테스트, 현행 이슈
- `docs/07_release/`: 릴리스 기준 문서

## 문서 역할 분리

같은 주제를 다루더라도 문서 역할은 아래처럼 분리한다.

- `docs/**`: durable knowledge
- `tasks/**`: live state, handoff, 실행 상태
- `prompts/**`, `.agents/skills/**`: agent routing asset
- `docs/04_decisions/**`: durable decision

live 진행 상황을 durable docs에 섞지 않는다.
durable 정책을 task 메모로 대체하지 않는다.

## 한 문서 한 목적 규칙

1. 문서 하나는 하나의 질문에 답해야 한다.
2. 같은 주제를 서로 다른 문서에 반복 설명하지 않는다.
3. 구조 원칙과 검수 절차를 같은 문서에 섞지 않는다.
4. 결정이 durable해지면 설명 문서가 아니라 ADR로 승격한다.
5. 기존 문서와 목적이 겹치면 새 문서를 늘리기 전에 분리 기준을 먼저 본문에 적는다.

## 문서 메타데이터 규칙

지속 문서는 제목을 H1으로 두고, 바로 아래에 최소 메타데이터를 둔다.
정책/절차/결정 문서는 아래 항목을 생략하지 않는다.

- 상태: `draft`, `active`, `deprecated`
- 소유자
- 최종수정일: `YYYY-MM-DD`
- 소스오브트루스
- 관련문서

필요할 때만 아래 확장 항목을 추가한다.

- 적용범위: 특정 에이전트 또는 특정 운영 주체에만 적용될 때
- 결정일: ADR에서 결정 시점을 별도로 강조할 때

현재 저장소는 Codex 전용 문서라도 영어 메타데이터 예외를 채택하지 않는다.
Codex 전용임을 드러내야 할 때는 `적용범위: Codex 전용`을 사용한다.

## 언어 규칙

1. 지속 문서는 한국어를 기본 언어로 쓴다.
2. 파일명, 코드, API 식별자, asmdef 이름, namespace 이름은 영어를 유지한다.
3. 문서 안의 코드 블록, 명령어, 경로, 식별자는 번역하지 않는다.
4. Codex 전용 문서도 본문과 메타데이터는 한국어를 기본으로 유지한다.

## 문서 수정 시 동시 갱신 규칙

문서를 수정할 때는 본문만 바꾸지 않는다. 아래 항목을 같은 작업 단위에서 확인한다.

1. 해당 폴더의 `index.md`
2. 문서의 `관련문서`
3. 들어오는 링크와 나가는 링크
4. 같은 주제를 설명하는 기준 문서
5. 구현 상태와 문서 서술의 충돌 여부

구조나 정책이 바뀌었는데 인덱스와 링크가 남아 있으면 완료로 보지 않는다.

## 에이전트 컨텍스트 규칙

1. 기본 시작 컨텍스트는 `AGENTS.md -> docs/index.md -> 관련 폴더 index.md -> 현재 task status.md` 순서를 따른다.
2. 모든 Markdown 파일을 기본 컨텍스트에 병합하지 않는다.
3. 세부 문서는 시작 컨텍스트를 읽은 뒤 범위와 직접 관련된 active source만 on-demand로 연다.
4. deprecated 문서, archive, legacy 메모, routing asset은 기본 durable corpus에 섞지 않는다.

## 충돌 해소 우선순위

1. 이미 채택된 ADR이 있으면 ADR을 우선 기준으로 본다.
2. 실제 구현이 문서보다 앞서 있으면 문서를 즉시 갱신하거나 상태를 낮춘다.
3. 서로 다른 문서가 같은 규칙을 다르게 말하면 소스오브트루스 문서 하나로 통일한다.
4. 해석이 갈리는 구조 변화는 임의 판단으로 숨기지 말고 ADR 또는 task 문서에 남긴다.

## 인덱스 운영 규칙

- 각 폴더의 `index.md`는 탐색 문서다.
- 인덱스는 실제 존재하는 문서만 나열한다.
- active index에는 active/draft 문서만 남긴다.
- deprecated pointer를 active index에 두지 않는다.
- 인덱스는 각 문서의 목적 차이를 짧게 설명해야 한다.
- 인덱스가 상세 정책을 복제하면 안 된다. 상세 내용은 개별 문서로 보낸다.

## 문서 수명주기 규칙

1. `status: deprecated`는 grace 상태가 아니라 제거 준비 상태로 본다.
2. deprecated 처리 시 replacement 문서 또는 ADR을 먼저 확정한다.
3. 같은 작업 단위에서 active index에서 제거하고 중앙 registry를 갱신한다.
4. 원본 deprecated 파일은 기본적으로 즉시 삭제한다.
5. 즉시 삭제가 위험하면 최대 7일 grace window를 둘 수 있지만, 이 경우에도 active index에서 즉시 제거하고 registry에 `remove_by`를 기록한다.
6. grace window가 끝나면 archive 또는 remove 중 하나를 완료하고 registry만 남긴다.

## source-of-truth matrix 규칙

하나의 주제를 여러 문서가 역할 분담해서 다루면 삭제보다 역할 표를 먼저 만든다.
이때 정책, setup, live state, routing asset, 결정 기록의 책임 경계를 `source-of-truth-matrix.md`에 남긴다.

## 검증 규칙

문서 체계 변경은 최소 아래 검증을 같이 수행한다.

1. `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
2. `pwsh -File tools/docs-check.ps1 -RepoRoot .`
3. `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
