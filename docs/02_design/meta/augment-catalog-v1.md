# augment catalog v1

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-08
- 소스오브트루스: `docs/02_design/meta/augment-catalog-v1.md`
- 관련문서:
  - `docs/02_design/meta/augment-system.md`
  - `docs/02_design/meta/synergy-and-augment-taxonomy.md`
  - `docs/02_design/meta/economy-protection-contract.md`

## 목적

이 문서는 full augment catalog 후보와 current live subset의 범위를 기록한다.

## capacity vs live subset

- schema capacity: `80`
- v1 prototype temporary target: `24`
- current authored catalog: temporary `24` + permanent candidate `12`
- first playable live subset: temporary `24` + permanent equip slot `1`

## first playable temporary augments (24)

### HeroRewrite (5) — live

1. `augment_gold_barrage` ★
2. `augment_platinum_hinterland` ★
3. `augment_platinum_reckoning` ★
4. `augment_silver_hunt` ★
5. `augment_silver_ward` ★

### TacticalRewrite (7) — live

1. `augment_gold_bastion` ★
2. `augment_gold_haste` ★
3. `augment_gold_ward` ★
4. `augment_platinum_tenacity` ★
5. `augment_silver_guard` ★
6. `augment_silver_reach` ★
7. `augment_silver_stride` ★

### ScalingEngine (4) — live

1. `augment_gold_fury` ★
2. `augment_platinum_overrun` ★
3. `augment_platinum_surge` ★
4. `augment_platinum_wall` ★

### EconomyAndLoot (5) — live

1. `augment_gold_mending` ★
2. `augment_gold_pack` ★
3. `augment_platinum_clarity` ★
4. `augment_silver_clarity` ★
5. `augment_silver_focus` ★

### SynergyPact (3) — live

1. `augment_gold_pact` ★
2. `augment_platinum_catacomb` ★
3. `augment_silver_hex` ★

`★` = first playable live subset.

## permanent augment 후보

현재 authored permanent candidate는 12개다. first playable live slice는 `augment_perm_legacy_blade` 1개만 직접 노출하고, 나머지는 parking lot과 progression 후보로 둔다.

1. `augment_perm_legacy_blade` ★
2. `augment_perm_legacy_bone`
3. `augment_perm_legacy_chalice`
4. `augment_perm_legacy_crown`
5. `augment_perm_legacy_fang`
6. `augment_perm_legacy_grace`
7. `augment_perm_legacy_hide`
8. `augment_perm_legacy_lantern`
9. `augment_perm_legacy_oath`
10. `augment_perm_legacy_scope`
11. `augment_perm_legacy_signal`
12. `augment_perm_legacy_spur`

## v1 catalog reserve

V1 prototype current scope에는 temporary reserve를 두지 않는다. 다음 expansion에서는 Pindoc settled baseline인 floor/safety pool을 별도 Task에서 재산정한다.

## live subset rule

- first playable에서는 5 bucket 분포를 `HeroRewrite 5 / TacticalRewrite 7 / ScalingEngine 4 / EconomyAndLoot 5 / SynergyPact 3`으로 둔다.
- `SynergyPact`는 build bias tag를 반드시 가진다.
- `EconomyAndLoot`가 전부 빠지면 recovery lever가 사라지므로 금지한다.
- permanent augment는 live equip slot 1 기준을 유지하고, authored candidate 확장은 live slice 승격과 분리한다.
