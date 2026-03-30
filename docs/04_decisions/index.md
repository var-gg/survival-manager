# 의사결정 문서 인덱스

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/04_decisions/index.md`
- 관련문서:
  - `docs/index.md`
  - `docs/00_governance/docs-governance.md`

## 목적

`04_decisions/`는 저장소와 prototype 구현에 durable하게 영향을 주는 ADR을 모아 둔다.

## ADR 목록

- `adr-0001-docs-architecture.md`: 문서 체계와 거버넌스 구조
- `adr-0002-prototype-phase.md`: prototype 단계 채택
- `adr-0003-unity-project-structure.md`: Unity 프로젝트 구조 채택
- `adr-0004-content-pipeline-boundary.md`: 콘텐츠 파이프라인 경계 채택
- `adr-0005-data-driven-content.md`: 데이터 주도 콘텐츠 방향 채택
- `adr-0006-combat-sim-boundary.md`: 전투 시뮬레이션 경계 채택
- `adr-0007-thirdparty-asset-policy.md`: 서드파티 에셋 정책 채택
- `adr-0008-editor-bridge-policy.md`: 에디터 브리지 정책 채택
- `adr-0009-persistence-boundary.md`: persistence 경계 채택
- `adr-0010-local-postgres-policy.md`: 로컬 Postgres 정책 채택
- `adr-0011-mcp-adoption-policy.md`: MCP 도입 정책
- `adr-0012-code-structure-and-dependency-policy.md`: 코드 구조와 의존 정책 채택
- `adr-0013-unity-cli-hybrid-lane.md`: Unity CLI hybrid lane 채택
- `adr-0014-grid-deployment-continuous-combat.md`: grid 배치 + continuous combat 채택
- `adr-0015-build-compile-audit-pipeline.md`: build-compile-audit 파이프라인 채택

## 운영 메모

- ADR 번호는 중복 없이 증가한다.
- 구조/정책이 durable하게 바뀌면 관련 기준 문서와 ADR을 함께 갱신한다.
