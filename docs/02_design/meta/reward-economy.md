# 보상 경제

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 expedition 보상이 어떤 가치 축으로 흘러야 하는지 정의한다.
핵심은 즉시 강해지는 보상과 장기적으로 빌드를 바꾸는 보상을 분리하는 것이다.

## 핵심 가치 축

### 즉시 가치

- gold
- item
- temporary augment

### 장기 가치

- permanent augment 후보 해금/획득
- trait reroll 희소 재화

## 경제 규칙

### 전투 보상

전투 보상은 주로 아래에서 구성한다.

- gold
- item
- temporary augment 3-choice

### expedition 종료 보상

expedition 종료 보상은 주로 아래를 담당한다.

- permanent augment 관련 진척
- trait reroll 재화
- gold

### 희소 재화 가치 규칙

trait reroll 재화는 **permanent augment와 동급 가치의 희소 재화**다.
이는 잡화성 currency가 아니라, 장기 빌드 방향을 바꾸는 귀중한 자원이다.

### temporary -> permanent 연결 규칙

run 중 temporary augment 최초 선택은 관련 permanent augment 후보 해금으로 이어질 수 있다.
따라서 temporary augment 보상은 즉시 전투력뿐 아니라 장기 메타 선택까지 건드린다.

## MVP 구현 범위

- gold 보상
- item 보상
- temporary augment 보상
- expedition 종료 시 permanent augment 관련 진척
- trait reroll 희소 재화

## 장기 규칙

- material economy
- event reward modifier
- difficulty reward scaling 확장
- PVP 전용 보상 체계

## PVP 경계

PVP는 비-MVP다.
PVP가 도입되더라도 run 중 temporary augment는 적용하지 않고, permanent augment만 적용한다.

## 밸런스 기준

- gold가 모든 문제를 해결하면 안 된다.
- temporary augment가 항상 정답이면 item이 죽는다.
- permanent augment와 trait reroll 재화가 너무 흔하면 장기 가치 축이 붕괴한다.
- 보상 편차가 너무 크면 전략보다 운에 좌우된다.
