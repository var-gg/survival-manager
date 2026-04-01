# Duplicate Handling Contract

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/meta/duplicate-handling-contract.md`
- 관련문서:
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/03_architecture/unit-economy-schema.md`

## 목적

Loop B 기준 duplicate를 `새 copy 추가`가 아니라 `Echo conversion`으로 닫아 roster identity와 recovery economy를 동시에 보호한다.

## Duplicate 판정

아래 중 하나면 duplicate다.

- 이미 roster가 같은 `UnitBlueprintId`를 보유한 상태에서 또 같은 blueprint를 획득
- event / reward / direct grant / 미래 meta reward가 같은 blueprint를 다시 지급

## V1 resolution

V1에서는 `ConvertToEcho`만 허용한다.

| tier | Echo |
| --- | --- |
| `Common` | `30` |
| `Rare` | `50` |
| `Epic` | `80` |

## Recruit와 duplicate의 관계

- 일반 recruit pool은 owned blueprint를 제외하므로 duplicate offer를 만들지 않는다.
- 외부 reward/direct grant만 duplicate path를 탈 수 있다.
- duplicate 처리 후 roster count는 늘지 않는다.
- duplicate 처리 결과는 toast/log로 명시해야 한다.

## UI / log requirements

- 어떤 unit이 duplicate였는지
- 왜 새 unit이 추가되지 않았는지
- Echo를 몇 얻었는지

## V1 forbidden

- duplicate second copy 보유
- duplicate star-up / awaken / imprint
- duplicate를 조용히 버리는 것

## Fallback rules

- recruit pool 단계에서 duplicate를 사전에 차단한다.
- 외부 grant path는 roster mutation 전에 duplicate resolver를 먼저 호출한다.
- resolver가 duplicate를 만나면 unit 추가 대신 Echo wallet만 갱신한다.

## 예시

### 예시 1. direct grant duplicate

- roster가 이미 `hunter`를 보유하고 있다.
- event가 `hunter`를 다시 지급하면 새 유닛은 추가되지 않고 tier 값에 맞는 Echo만 지급된다.

### 예시 2. recruit phase

- roster가 `hexer`를 보유 중이면 recruit pack 후보 풀에서 `hexer`는 제외된다.
- 따라서 일반 recruit UI에서 duplicate card가 뜨지 않는다.
