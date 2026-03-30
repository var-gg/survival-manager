# ADR-0015 build-compile-audit 파이프라인 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 결정일: 2026-03-30
- 소스오브트루스: `docs/04_decisions/adr-0015-build-compile-audit-pipeline.md`
- 관련문서:
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`
  - `docs/03_architecture/replay-persistence-and-run-audit.md`
  - `docs/02_design/systems/squad-blueprint-and-build-ownership.md`

## 문맥

기존 prototype은 hero identity, run overlay, battle input, reward mutation이 여러 파일과 layer에 흩어져 있었다.
이 구조는 PvP, 운영 audit, run resume, build 검증을 모두 어렵게 만든다.

## 결정

다음 파이프라인을 저장소의 기준으로 채택한다.

- 콘텐츠 정의는 `SM.Content`가 소유한다.
- 영구 성장과 run overlay는 `SM.Meta`가 소유한다.
- 전투 입력 생성은 `LoadoutCompiler`가 단일 책임으로 수행한다.
- 전투 해상은 `SM.Combat`의 deterministic simulation이 수행한다.
- replay, ledger, suspicion flag는 persistence contract로 별도 저장한다.

## 결과

### 기대 효과

- 같은 build 입력은 같은 compile hash와 같은 전투 결과 검증 경로를 가진다.
- active run resume와 reward audit를 같은 모델에서 다룰 수 있다.
- Unity scene script가 save truth나 combat truth를 직접 만들지 않게 된다.

### 감수할 비용

- 상태 모델 수가 늘어난다.
- migration 동안 compatibility wrapper가 필요하다.
