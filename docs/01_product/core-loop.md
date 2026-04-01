# 핵심 루프

- 상태: draft
- 최종수정일: 2026-03-29
- 단계: prototype

## 목적

이 문서는 MVP 핵심 루프와 장기 루프를 분리해 정의한다.
핵심은 한 run 안의 임시 선택과 run 밖의 영구 진척이 서로 다른 의미를 가지게 만드는 것이다.

## MVP 핵심 루프

1. town roster에서 후보를 확인하고 편성 방향을 정한다.
2. recruit / 장비 / trait 상태를 보고 expedition 8인을 구성한다.
3. 4인을 실제 전투 배치로 선택한다.
4. expedition 전투 또는 짧은 노드 체인을 진행한다.
5. auto-battle 결과를 본다.
6. gold / item / temporary augment 보상을 받는다.
7. temporary augment를 최초 선택하면 관련 permanent augment 후보가 해금된다.
8. 귀환 후 로스터, 장비, 다음 선택 방향을 재정비한다.

## MVP 루프에서 실제 구현할 의미

### 로스터 층

- town-held roster cap: 12
- expedition roster size: 8
- deployed battle squad: 4

이 구조는 전투 전에 이미 중요한 선택을 만들기 위한 것이다.

### 차별화 층

같은 archetype이라도 다음 세 축으로 역할이 달라져야 한다.

- trait roll
- equipment
- augment

즉 recruit 시점의 archetype은 빌드의 시작값일 뿐이다.

### 보상 층

- 전투 보상: gold / item / temporary augment
- expedition 종료 보상: permanent augment 관련 진척, Echo, gold

Echo는 소모성 잡화가 아니라, recruit/retrain RNG 복구를 담당하는 run economy의 핵심 재화로 본다.

## 장기 핵심 루프 방향

장기적으로는 다음이 들어갈 수 있다.

- 더 긴 expedition routing
- advanced crafting
- broader synergy families
- permanent augment branch 확장
- PVP용 별도 progression/balance

하지만 이들은 MVP를 증명한 뒤에만 확장한다.

## MVP 실패 조건

- 같은 archetype 유닛들이 실제로 비슷하게만 느껴진다.
- trait / 장비 / augment 차이가 역할 차이로 이어지지 않는다.
- temporary reward가 다음 선택을 기대하게 만들지 못한다.
- 귀환 후 다음 run 준비가 지루하거나 의미가 없다.
