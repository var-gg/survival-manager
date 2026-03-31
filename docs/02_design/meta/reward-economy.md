# 보상 경제

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/meta/reward-economy.md`
- 관련문서:
  - `docs/02_design/meta/drop-table-rarity-bracket-and-source-matrix.md`
  - `docs/02_design/meta/reward-protection-and-acquisition-loop.md`
  - `docs/02_design/meta/skill-acquisition-and-retrain.md`
  - `docs/03_architecture/drop-resolution-and-ledger-pipeline.md`

## 현재 reward 구조

- 보상은 `automatic battle drops`와 `operator-choice reward cards` 두 채널로 분리된다.
- Reward scene은 기존 3지선다 카드 3개를 계속 사용한다.
- 자동 드롭은 Reward scene 진입 전에 먼저 ledger와 inventory/currency에 반영된다.
- Reward scene은 자동 드롭 summary와 카드 선택 결과를 함께 보여 준다.

## 현재 구현 규칙

### automatic battle drops

- `reward_source_*`가 드롭 테이블과 rarity bracket을 소유한다.
- battle result 직후 source-tagged loot bundle을 계산한다.
- gold / crafting mat / item / skill manual / skill shard / trait token이 여기서 ledger로 들어간다.

### operator-choice reward cards

- gold: 즉시 재화 증가
- item: inventory 수 증가
- temporary augment: 현재 run augment 수 증가
- trait reroll: profile currency 즉시 증가
- permanent slot: profile permanent slot 수 즉시 증가

## 메모

- Quick Battle smoke도 Reward를 정상 통과한다.
- expedition node context에 따라 카드 풀이 달라진다.
- 자동 드롭도 같은 reward source와 seed를 기준으로 결정론적으로 계산된다.
- Reward card presentation은 operator-grade placeholder이며 정식 카드 UX는 다음 단계다.

## acquisition / protection 연결

- reward는 단순 power injection이 아니라 recovery lever를 같이 제공해야 한다.
- recruit가 RNG인 run에서는 retrain / reroll / economy card가 recovery channel이 된다.
- augment offer protection과 item reroll 보호 로직의 상세는 `reward-protection-and-acquisition-loop.md`를 따른다.
