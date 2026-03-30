# ADR-0017 문서 컨텍스트 하네스와 tombstone registry 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 결정일: 2026-03-31
- 소스오브트루스: `docs/04_decisions/adr-0017-docs-context-harness.md`
- 관련문서:
  - `AGENTS.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/00_governance/docs-harness.md`
  - `docs/00_governance/deprecated-docs-registry.md`
  - `docs/00_governance/source-of-truth-matrix.md`

## 문맥

저장소의 durable docs, live task state, agent routing asset이 모두 Markdown이라는 이유만으로 평면적으로 읽히면 오래된 문서와 영어 drift가 기본 컨텍스트에 재주입된다.
특히 active index가 deprecated pointer를 계속 노출하면, 폐기한 문서를 보존하는 수준이 아니라 오래된 기준을 active source 후보로 되살리는 문제가 생긴다.

## 결정

다음 문서 하네스 규칙을 저장소 표준으로 채택한다.

- 기본 시작 컨텍스트는 `AGENTS.md -> docs/index.md -> 관련 폴더 index.md -> 현재 task status.md`로 제한한다.
- durable docs, live task state, routing asset, ADR를 역할별로 분리해서 읽는다.
- deprecated 문서는 active index와 기본 검색 경로에서 즉시 제외한다.
- deprecated 사유는 중앙 tombstone registry와 replacement/ADR에 남기고, 원본 파일에는 영구 누적하지 않는다.
- 원본 deprecated 파일은 기본적으로 즉시 삭제하고, 필요한 경우에만 최대 7일 grace window를 둔다.
- 겹치는 문서군은 삭제보다 source-of-truth matrix로 역할 경계를 먼저 명시한다.
- deterministic enforcement는 PowerShell policy check와 CI를 1차 경로로 둔다.

## 결과

### 기대 효과

- 에이전트가 기본 컨텍스트에서 읽는 Markdown 양이 줄어도 정확도는 높아진다.
- deprecated 문서와 영어 drift가 active source로 재주입되는 경로를 끊을 수 있다.
- 같은 주제를 다루는 문서군을 삭제보다 역할 분리로 정리할 수 있다.

### 감수할 비용

- 문서 lifecycle을 바꿀 때 registry, index, task status를 같이 갱신해야 한다.
- active tree에 남겨 두는 deprecated 파일이 줄어들어 기록 방식이 더 엄격해진다.
- docs policy check를 유지보수해야 한다.

## 기각한 대안

- 모든 Markdown 파일을 기본 컨텍스트에 넓게 병합:
  - deprecated pointer와 language drift가 쉽게 재주입되어 문서 엔트로피가 커진다.
- 개별 deprecated 파일에 이유를 영구 보존:
  - tombstone 문서가 계속 늘어 active corpus를 오염시키고 index drift를 유발한다.
- deprecated pointer를 active index에 남겨 두기:
  - 역사 문서와 현재 기준 문서를 읽기 단계에서 구분하기 어려워진다.
