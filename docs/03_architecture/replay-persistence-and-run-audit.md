# replay persistence와 run audit

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/03_architecture/replay-persistence-and-run-audit.md`
- 관련문서:
  - `docs/03_architecture/persistence-strategy.md`
  - `docs/03_architecture/persistence-schema.md`
  - `docs/04_decisions/adr-0015-build-compile-audit-pipeline.md`

## 목적

이 문서는 active run 저장, match header/blob, reward/inventory ledger, suspicion flag를 persistence 계약으로 정리한다.

## 저장 규칙

- active run은 node progress, temporary augment, pending reward, deploy lineup, compile version/hash를 저장한다.
- match header는 조회와 비교용 관계형 필드를 가진다.
- match blob은 input digest, event stream, keyframe digest를 가진다.
- reward와 inventory는 append-only ledger다.

## adapter 규칙

- `SM.Persistence.Abstractions`는 save contract만 가진다.
- JSON과 Postgres adapter는 같은 모델을 직렬화할 수 있어야 한다.
- combat는 persistence DTO를 직접 참조하지 않는다.
