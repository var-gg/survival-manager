# ADR-0009 persistence 경계 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 결정일: 2026-03-29
- 소스오브트루스: `docs/04_decisions/adr-0009-persistence-boundary.md`
- 관련문서:
  - `docs/03_architecture/persistence-strategy.md`
  - `docs/03_architecture/data-model.md`
  - `docs/03_architecture/dependency-direction.md`

## 문맥

hero roster, expedition, reward, 장기 진행을 저장해야 하지만, save 모델을 콘텐츠 자산이나 production DB 전제와 섞으면 MVP가 쉽게 경직된다.
저장 truth를 분리해 두어야 로컬 플레이와 후속 adapter 확장이 가능하다.

## 결정

다음 persistence 경계를 채택한다.

- gameplay definition은 Unity asset으로 유지한다.
- runtime state와 save state는 별도 persistence model로 다룬다.
- DB는 선택적 외부 adapter이며 core assumption이 아니다.
- MVP는 direct production DB access를 전제하지 않는다.

## 결과

### 기대 효과

- save state가 portable하고 local-first로 유지된다.
- 콘텐츠 자산을 runtime/save truth가 오염시키지 않는다.
- 후속 persistence adapter 추가가 쉬워진다.

### 감수할 비용

- stable id와 migration 규율이 필요하다.
- definition과 save model 사이에 shape duplication이 생길 수 있다.

## 후속

- save model은 instance/progress 중심으로 설계한다.
- DB 세부는 domain logic 안으로 새지 않게 한다.
