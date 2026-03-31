# reward protection and acquisition loop

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/meta/reward-protection-and-acquisition-loop.md`
- 관련문서:
  - `docs/02_design/meta/reward-economy.md`
  - `docs/02_design/meta/skill-acquisition-and-retrain.md`
  - `docs/02_design/meta/augment-synergy-operating-model.md`
  - `docs/03_architecture/sim-sweep-and-balance-kpis.md`

## 목적

이 문서는 RNG 층을 늘리는 대신 복구 수단을 같이 주는 운영 계약을 정의한다.

## 경제 레버

- recruit cost
- reroll cost
- retrain cost
- item reforge / recombine cost
- rare currency sink
- item drop frequency
- 원하는 빌드로 수렴하는 평균 stage 수

## 보호 로직

- recruit가 RNG면 skill flex retrain은 부분 통제 가능해야 한다.
- item이 RNG면 augment offer는 완전 랜덤으로만 주지 않는다.
- synergy가 핵심이면 `count +1`, protection offer, pivot offer를 소량 둔다.

## canonical acquisition loop

1. recruit로 core identity와 flex variance 확보
2. battle / node reward로 item, augment, retrain currency 획득
3. reroll / retrain / reforge로 partial recovery
4. 다음 recruit 또는 reward에서 build 방향 보정

## offer distribution

- temporary augment: 3-choice
- item card: immediate power or future conversion
- retrain currency: recovery lever
- economy card: 다음 node 기대치 조정

## protection policy

- 같은 시스템에서 RNG와 recovery를 동시에 hard-lock하지 않는다.
- 나쁜 roll은 stage 1~2개 안에 partial correction 수단이 보여야 한다.
- protection은 정답 보장 장치가 아니라 run salvage 장치다.
