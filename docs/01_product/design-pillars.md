# 디자인 원칙

- 상태: draft
- 최종수정일: 2026-03-31
- 단계: prototype

## 목적

이 문서는 `survival-manager`의 MVP 결정을 제약해야 하는 디자인 원칙을 정의한다.
각 원칙은 장르 혼합을 넓게 약속하기보다, 플레이 가능한 vertical slice에 집중하도록 돕는 기준이어야 한다.

## 원칙 1. Setup이 전투 결과를 결정해야 한다

플레이어는 전투가 시작되기 전에 이미 결과의 방향이 정해진다고 느껴야 한다.
formation, class 조합, race synergy, 준비 선택이 전투 중 직접 조작보다 더 중요해야 한다.

### MVP 의미

MVP에서는 작지만 의미 있는 배치, roster, synergy 선택이 있어야 한다.

### 장기 방향

이후 버전에서 더 풍부한 tactical scripting이나 깊은 formation rule을 추가할 수 있지만, MVP는 그런 확장에 의존하지 않는다.

## 원칙 2. Auto-battle은 끝까지 읽혀야 한다

전투는 3D auto-battle로 보이더라도, 내부 로직은 먼저 단순하고 검증 가능해야 한다.
플레이어는 조합과 준비에서 어떤 원인과 결과가 나왔는지 이해할 수 있어야 한다.

### MVP 의미

MVP는 화려함이나 simulation 깊이보다, 명확한 resolution logic을 우선한다.

### 장기 방향

readability가 유지되는 범위에서만 encounter complexity와 presentation density를 늘린다.

## 원칙 3. Synergy는 실제 roster pressure를 만들어야 한다

race와 class 조합은 의미 있는 roster-building 결정을 만들어야 한다.
플레이어는 개별 유닛 강함뿐 아니라, 조합 자체를 신경 써야 한다.

### MVP 의미

고정된 3개 race와 4개 class만으로도 눈에 띄는 synergy tradeoff가 나와야 한다.

### 장기 방향

추가 race, class, edge-case interaction은 기본 synergy 문법이 먼저 작동할 때만 확장한다.

## 원칙 4. Expedition과 Return은 실제 의미가 있어야 한다

게임은 단순히 전투 하나로 끝나면 안 된다.
플레이어는 squad를 내보내고, run을 버티고, town으로 돌아와 결과와 다음 선택을 맞닥뜨리는 압박을 느껴야 한다.

### MVP 의미

단순화되어도 expedition/return loop가 화면과 선택으로 드러나야 한다.

### 장기 방향

루프가 검증된 뒤에만 더 긴 run 구조, event, risk layer를 추가한다.

## 원칙 5. 넓은 약속보다 좁은 완성이 우선이다

프로젝트는 큰 약속보다, 작더라도 실제로 돌아가는 루프를 우선해야 한다.
목각인형 수준 playable slice를 강화하지 않는 기능은 대체로 뒤로 미룬다.

### MVP 의미

고정 수치와 명시적 비목표는 임시 제안이 아니라 디자인 원칙의 일부다.

### 장기 방향

미래 확장은 MVP 루프가 실제로 검증된 뒤에만 허용한다.

## 열린 질문

- 실제 구현 압력이 커졌을 때 가장 먼저 흔들릴 가능성이 높은 원칙은 무엇인가?
- 3개 race와 4개 class만으로 의미 있는 roster synergy 깊이를 만들려면 어느 정도면 충분한가?
- 원칙 4를 만족시키는 최소 expedition 구조는 무엇인가?
- 그럴듯해 보이지만 원칙 5를 약화시키는 장기 아이디어는 무엇인가?
