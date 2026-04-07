# Economy Protection Contract

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `docs/02_design/meta/economy-protection-contract.md`
- 관련문서:
  - `docs/02_design/meta/recruitment-contract.md`
  - `docs/02_design/meta/retrain-contract.md`
  - `docs/02_design/meta/duplicate-handling-contract.md`
  - `docs/03_architecture/unit-economy-schema.md`

## 목적

Loop B 기준 run economy를 `Gold = 외부 파워 획득`, `Echo = RNG 복구 / build tuning`으로 분리하고,
buildcraft closure에서도 no-dead-reward 장치를 유지한다.

## Wallet split

| currency | 용도 |
| --- | --- |
| `Gold` | recruit / refresh |
| `Echo` | scout / retrain / refit / duplicate recovery |

## V1 allowed

- recruit와 refresh는 `Gold`를 사용한다.
- retrain과 scout는 `Echo`를 사용한다.
- refit은 `15 Echo` 고정, single-affix reroll only로 제한한다.
- duplicate는 항상 `Echo`로 변환된다.
- dismiss는 recruit gold와 retrain echo의 `50%`를 각각 반환한다.
- recruit phase마다 무료 refresh `1회`가 제공된다.
- passive board swap과 permanent augment swap은 V1에서 무료다.
- normal reward path는 `Gold / Echo / Item / TemporaryAugment`만 지급한다.
- permanent progression은 reward card가 아니라 `summary unlock event`로 처리한다.

## V1 forbidden

- `Gold`와 `Echo` 직접 환전
- duplicate를 second copy, star-up, awaken, imprint로 쓰는 것
- duplicate가 0가치 결과가 되는 것
- last roster unit dismiss
- dismiss 중 item 파괴
- passive / permanent 실험에 별도 tax를 부과하는 것
- normal playable lane에서 `TraitLockToken`, `TraitPurgeToken`, `EmberDust`, `EchoCrystal`, `BossSigil`, `SkillManual`, `SkillShard`를 live wallet rail처럼 노출하는 것

## No dead reward invariants

- on-plan 후보가 있는데 pack 전체가 off-plan이면 안 된다.
- retrain이 current/previous 반복만 내놓아 체감상 무의미한 결과가 되면 안 된다.
- duplicate는 항상 `Echo` 가치로 바뀌어야 한다.
- permanent는 slot 카드가 아니라 temp engagement로 열리는 candidate여야 한다.
- passive node invalidation은 prerequisite / exclusion / cap rule을 위반한 채 숨겨지면 안 된다.

## Controlled recovery toolkit

| 도구 | 비용 | 역할 |
| --- | --- | --- |
| free refresh | `0 Gold` | recruit phase당 1회 기본 복구 |
| paid refresh | `2/4/6 Gold` | 추가 offer 확인 |
| scout | `35 Echo` | 다음 on-plan 슬롯 방향성 부여 |
| retrain | `30~90 Echo` | owned unit flex 보정 |
| refit | `15 Echo` | 장비 affix light tuning |
| dismiss | `50%` refund | 잘못 들인 투자 회수 |

## Dismiss contract

- dismiss는 recruit phase 또는 camp phase equivalent에서만 허용한다.
- 현재 vertical slice에서는 `Town`을 그 phase로 간주한다.
- dismiss는 `recruitGoldPaid * 0.5`, `retrainEchoPaid * 0.5`를 돌려준다.
- gear는 inventory로 회수하고 파괴하지 않는다.
- summon/deployable에는 적용하지 않는다.

## Affordability UX

recruit 화면과 retrain 화면은 아래를 표시해야 한다.

- 현재 wallet `Gold`, `Echo`
- 각 offer / retrain cost
- free refresh 잔여
- Rare/Epic pity progress
- scout 사용 가능 여부

Town build-management lane은 여기에 더해 아래를 같이 보여 준다.

- refit / dismiss / retrain cost 또는 refund preview
- 현재 equipped permanent augment
- 현재 passive board와 selected node summary

## 예시

### 예시 1. recruit miss 복구

- player가 두 번 연속 off-plan에 가까운 pack을 봤다.
- 무료 refresh와 scout를 쓰면 Gold를 태우지 않고도 다음 `OnPlan` 슬롯 방향을 조정할 수 있다.

### 예시 2. 잘못 영입한 유닛 정리

- 희귀 유닛을 `10 Gold`에 영입하고 retrain에 `20 Echo`를 썼다.
- dismiss 시 `5 Gold + 10 Echo`를 회수하고 장비는 inventory로 돌아간다.

### 예시 3. permanent progression dead reward 제거

- 이번 run에서 temp augment를 처음 집었다.
- 같은 family permanent candidate가 이미 known이 아니면 Town 복귀 시 candidate로 해금된다.
- slot 증가 카드가 따로 뜨지 않으므로 dead pick 없이 다음 run thesis 선택 압력만 남긴다.
