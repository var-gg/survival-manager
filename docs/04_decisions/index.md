# 의사결정 문서 인덱스

- 상태: active
- 최종수정일: 2026-03-29

`04_decisions/`는 저장소와 프로토타입 구현에 영향을 주는 ADR을 모아 둔다.

## ADR 목록

- `adr-0001-docs-architecture.md`: 문서 구조와 운영 원칙
- `adr-0002-prototype-phase.md`: 저장소 phase를 prototype으로 전환
- `adr-0002-unity-project-structure.md`: Unity 프로젝트 루트 구조 결정
- `adr-0003-content-pipeline-boundary.md`: 콘텐츠 파이프라인 경계
- `adr-0003-data-driven-content.md`: 데이터 주도형 콘텐츠 정의 방향
- `adr-0004-combat-sim-boundary.md`: 전투 시뮬레이션 경계
- `adr-0004-thirdparty-asset-policy.md`: 서드파티 에셋 수정 정책
- `adr-0005-editor-bridge-policy.md`: 에디터 브리지 정책
- `adr-0005-persistence-boundary.md`: persistence 경계
- `adr-0006-local-postgres-policy.md`: local Postgres 정책

## 주의

현재 `adr-0002`~`adr-0005` 구간에는 번호 충돌이 존재한다.
이번 단계에서는 누락 없이 인덱싱만 수행하며, 번호 정규화는 별도 작업으로 다룬다.
