# launch floor 콘텐츠 매트릭스

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-02
- 소스오브트루스: `docs/02_design/systems/launch-floor-content-matrix.md`
- 관련문서:
  - `docs/02_design/systems/launch-content-scope-and-balance.md`
  - `docs/02_design/combat/resource-cadence-loadout.md`
  - `docs/02_design/deck/roster-archetype-launch-scope.md`
  - `docs/02_design/meta/synergy-family-catalog.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## 목적

이 문서는 paid launch floor에서 바로 authoring해야 하는 `12 core archetypes`를 파일 단위 기준으로 잠근다.
후속 팀이 archetype, skill, item, passive, synergy를 해석으로 채우지 않도록, launch floor의 조합과 Loop A loadout anchor를 한 장에서 고정한다.

## 고정 원칙

- `warden / guardian / slayer / raider / hunter / scout / priest / hexer` 기존 core id는 유지한다.
- 이번 패스에서 추가로 잠그는 core id는 `bulwark / reaver / marksman / shaman`이다.
- class canonical id는 `vanguard / duelist / ranger / mystic`를 유지한다.
- player-facing role family label은 `Vanguard / Striker / Ranger / Mystic`를 쓴다.
- `duelist`는 runtime/content canonical id이고 `Striker`는 UI/문서 라벨이다.
- 모든 archetype은 `BasicAttack / SignatureActive / FlexActive / SignaturePassive / FlexPassive / MobilityReaction` 6-slot topology를 만족한다.
- 아래 표는 `SignatureActive / FlexActive / SignaturePassive / FlexPassive` anchor를 고정하고, `BasicAttack`, `MobilityReaction`은 archetype locked asset으로 본다.

## synergy reachability

- `3 races x 4 classes`이므로 race당 4명, class당 3명이다.
- race synergy: `2 / 4` — race 올인(같은 race 4명)으로 4-piece 도달 가능.
- class synergy: `2 / 3` — class 올인(같은 class 3명)으로 3-piece 도달 가능. class 4-piece는 duplicate 금지 + class당 3명이므로 도달 불가능하다.

## core archetype 매트릭스

| archetype id | display name | race | class | scope kind | role family | primary weapon family | signature mechanic | expected synergy families | recommended team posture | Loop A slot anchors |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `warden` | **Iron Warden** | `human` | `vanguard` | `core` | `vanguard` | `shield` | 전열 고정과 protect radius 유지에 강한 기준 anchor | `synergy_human`, `synergy_vanguard` | `HoldLine` | `signatureActive=skill_power_strike`, `flexActive=skill_warden_utility`, `signaturePassive=skill_vanguard_passive_1`, `flexPassive=skill_vanguard_support_1` |
| `guardian` | **Crypt Guardian** | `undead` | `vanguard` | `core` | `vanguard` | `shield` | attrition형 protect-carry bunker | `synergy_undead`, `synergy_vanguard` | `ProtectCarry` | `signatureActive=skill_guardian_core`, `flexActive=skill_guardian_utility`, `signaturePassive=skill_vanguard_passive_2`, `flexPassive=skill_vanguard_support_2` |
| `bulwark` | **Fang Bulwark** | `beastkin` | `vanguard` | `core` | `vanguard` | `shield` | beastkin tempo를 전열 쪽으로 끌어오는 collapse anchor | `synergy_beastkin`, `synergy_vanguard` | `CollapseWeakSide` | `signatureActive=skill_bulwark_core`, `flexActive=skill_bulwark_utility`, `signaturePassive=skill_vanguard_passive_1`, `flexPassive=skill_vanguard_support_2` |
| `slayer` | **Oath Slayer** | `human` | `duelist` | `core` | `striker` | `blade` | execution 중심의 기준 melee finisher | `synergy_human`, `synergy_duelist` | `StandardAdvance` | `signatureActive=skill_slayer_core`, `flexActive=skill_slayer_utility`, `signaturePassive=skill_duelist_passive_1`, `flexPassive=skill_duelist_support_1` |
| `raider` | **Pack Raider** | `beastkin` | `duelist` | `core` | `striker` | `blade` | weak-side collapse와 exposed target dive | `synergy_beastkin`, `synergy_duelist` | `CollapseWeakSide` | `signatureActive=skill_raider_core`, `flexActive=skill_raider_utility`, `signaturePassive=skill_duelist_passive_2`, `flexPassive=skill_duelist_support_2` |
| `reaver` | **Grave Reaver** | `undead` | `duelist` | `core` | `striker` | `blade` | sustain pressure를 가진 all-in backline diver | `synergy_undead`, `synergy_duelist` | `AllInBackline` | `signatureActive=skill_reaver_core`, `flexActive=skill_reaver_utility`, `signaturePassive=skill_duelist_passive_1`, `flexPassive=skill_duelist_support_2` |
| `hunter` | **Longshot Hunter** | `human` | `ranger` | `core` | `ranger` | `bow` | 기준 exposed-target pick과 steady aim carry | `synergy_human`, `synergy_ranger` | `StandardAdvance` | `signatureActive=skill_precision_shot`, `flexActive=skill_hunter_utility`, `signaturePassive=skill_ranger_passive_1`, `flexPassive=skill_ranger_support_1` |
| `scout` | **Trail Scout** | `beastkin` | `ranger` | `core` | `ranger` | `bow` | split-shot와 tempo relocation으로 lane 붕괴 유도 | `synergy_beastkin`, `synergy_ranger` | `CollapseWeakSide` | `signatureActive=skill_scout_core`, `flexActive=skill_scout_utility`, `signaturePassive=skill_ranger_passive_2`, `flexPassive=skill_ranger_support_2` |
| `marksman` | **Dread Marksman** | `undead` | `ranger` | `core` | `ranger` | `bow` | protected carry 운영에서 가장 안정적인 long-lane 딜러 | `synergy_undead`, `synergy_ranger` | `ProtectCarry` | `signatureActive=skill_marksman_core`, `flexActive=skill_marksman_utility`, `signaturePassive=skill_ranger_passive_1`, `flexPassive=skill_ranger_support_1` |
| `priest` | **Dawn Priest** | `human` | `mystic` | `core` | `mystic` | `focus` | protect-carry sustain과 cleanse성 운영의 기준점 | `synergy_human`, `synergy_mystic` | `ProtectCarry` | `signatureActive=skill_priest_core`, `flexActive=skill_minor_heal`, `signaturePassive=skill_mystic_passive_1`, `flexPassive=skill_mystic_support_1` |
| `hexer` | **Grave Hexer** | `undead` | `mystic` | `core` | `mystic` | `focus` | attrition 제어와 curse-heal 혼합 backline support | `synergy_undead`, `synergy_mystic` | `AllInBackline` | `signatureActive=skill_hexer_core`, `flexActive=skill_hexer_utility`, `signaturePassive=skill_mystic_passive_2`, `flexPassive=skill_mystic_support_2` |
| `shaman` | **Storm Shaman** | `beastkin` | `mystic` | `core` | `mystic` | `focus` | beastkin tempo를 heal/control로 연결하는 flexible support | `synergy_beastkin`, `synergy_mystic` | `StandardAdvance` | `signatureActive=skill_shaman_core`, `flexActive=skill_shaman_utility`, `signaturePassive=skill_mystic_passive_1`, `flexPassive=skill_mystic_support_2` |

## specialist reserve 슬롯

이번 패스에서는 specialist asset을 authoring하지 않는다.
대신 safe target에서 꼭 채워야 할 `4 specialist slots`만 reserve로 잠그고, validator 목표는 `ScopeKind = Specialist` 4개 추가다.

| reserve slot | target class | reserved role | reserved purpose | validation target |
| --- | --- | --- | --- | --- |
| `reserve_vanguard_specialist` | `vanguard` | anti-dive anchor | frontline 예외 규칙 1종 | safe target에서 specialist 1/4 |
| `reserve_duelist_specialist` | `duelist` | execution diver | baseline striker와 다른 진입 규칙 1종 | safe target에서 specialist 2/4 |
| `reserve_ranger_specialist` | `ranger` | siege carry | range/spacing 예외 규칙 1종 | safe target에서 specialist 3/4 |
| `reserve_mystic_specialist` | `mystic` | control support | energy/control 예외 규칙 1종 | safe target에서 specialist 4/4 |

## launch-floor acceptance

- `3 races x 4 classes` 조합이 모두 위 표대로 존재해야 한다.
- 모든 archetype은 `ScopeKind`, `RoleFamilyTag`, `PrimaryWeaponFamilyTag`를 가진다.
- 모든 archetype은 6-slot loadout topology를 만족한다.
- archetype별 posture, synergy 기대값, Loop A slot anchor가 이 문서와 다르면 launch floor drift로 본다.
