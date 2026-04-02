# augment catalog v1

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-02
- 소스오브트루스: `docs/02_design/meta/augment-catalog-v1.md`
- 관련문서:
  - `docs/02_design/meta/augment-system.md`
  - `docs/02_design/meta/synergy-and-augment-taxonomy.md`
  - `docs/02_design/meta/economy-protection-contract.md`

## 목적

이 문서는 full augment catalog 후보와 current live subset의 범위를 기록한다.

## capacity vs live subset

- schema capacity: `80`
- v1 catalog target: `30`
- first playable live subset: temporary `12` + permanent `4` = `16`

## first playable temporary augments (12)

### NeutralCombat (3) — live

1. `Frontline Doctrine` ★
2. `Kiting Manual` ★
3. `Battlefield Momentum` ★

### EconomyRoster (3) — live

4. `Efficient Training` ★
5. `Backup Crew` ★
6. `Salvager` ★

### SynergyLinked (3) — live

7. `Banner of Vanguard` ★
8. `Duel Protocol` ★
9. `Arcane Loop` ★

### WildcardRisk (3) — live

10. `Blood Price` ★
11. `Glass Arsenal` ★
12. `Oath of Attrition` ★

`★` = first playable live subset.

## first playable permanent augments (4) — live

permanent augment는 team posture 강화형으로 고정한다.

1. `Citadel Doctrine` → HoldLine ★
2. `Guardian Detail` → ProtectCarry ★
3. `Breakthrough Orders` → CollapseWeakSide ★
4. `Night Hunt Mandate` → AllInBackline ★

## v1 catalog reserve (v1 target 30까지의 나머지)

### NeutralCombat reserve

- `Rapid Recovery`
- `Shock Entry`
- `Last Breath`
- `Precision Burst`
- `Thick Skin`
- `Controlled Aggression`
- `Arc Dampener`
- `Sharpened Steel`
- `Prepared Ground`

### EconomyRoster reserve

- `Black Market`
- `Veteran Banner`
- `Lucky Find`

### SynergyLinked reserve

- `Hunter's Routine`
- `Blood Trail`
- `Grim Harvest`
- `Wall of Iron`
- `Forged Allies`

### WildcardRisk reserve

- `Unstable Portals`

## live subset rule

- first playable에서는 every bucket에서 3개씩 live subset에 둔다.
- `NeutralCombat`과 `EconomyRoster`가 전부 빠지면 recovery lever가 사라지므로 금지한다.
- permanent augment는 4개 team posture type을 모두 커버한다.
