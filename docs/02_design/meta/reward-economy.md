# 보상 경제

- 상태: active
- 최종수정일: 2026-03-30
- 단계: prototype

## 현재 operator 범위

- Reward scene은 3지선다 카드 3개를 실제 적용한다.
- 현재 효과 축은 gold / item / temporary augment / trait reroll / permanent slot이다.
- 카드 선택 직후 summary / status / 저장 상태가 즉시 갱신된다.

## 현재 구현 규칙

- gold: 즉시 재화 증가
- item: inventory 수 증가
- temporary augment: 현재 run augment 수 증가
- trait reroll: profile currency 즉시 증가
- permanent slot: profile permanent slot 수 즉시 증가

## 메모

- Quick Battle smoke도 Reward를 정상 통과한다.
- expedition node context에 따라 카드 풀이 달라진다.
- Reward card presentation은 operator-grade placeholder이며 정식 카드 UX는 다음 단계다.
