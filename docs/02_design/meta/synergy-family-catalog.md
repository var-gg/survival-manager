# synergy family 카탈로그

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/meta/synergy-family-catalog.md`
- 관련문서:
  - `docs/02_design/meta/synergy-breakpoints-and-soft-counters.md`
  - `docs/02_design/meta/augment-synergy-operating-model.md`
  - `docs/02_design/meta/synergy-and-augment-taxonomy.md`
  - `docs/02_design/systems/launch-floor-content-matrix.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## 목적

이 문서는 launch floor에서 authoring할 `7 synergy families`의 exact payload를 고정한다.
`2 / 3 / 4` breakpoint 의미를 문장 수준이 아니라 수치와 대상 기준으로 잠가서, content authoring과 sim sweep가 같은 기준을 보게 한다.

현재 live subset은 7 family를 유지한다.
schema capacity 상의 추가 family 후보는 `synergy-and-augment-taxonomy.md`에서만 논의하고, 이 문서는 committed exact payload만 소유한다.

## 공통 규칙

- counted tag는 authored content의 canonical tag를 그대로 쓴다.
- affected target은 기본적으로 `ally teamwide`다.
- breakpoint는 `2 / 3 / 4`만 허용한다.
- runtime은 달성한 breakpoint package를 누적 적용한다.
  - 예: 4-piece면 `2 + 3 + 4` package를 전부 얻는다.
- 같은 family는 동일 threshold package를 한 번만 적용한다.
- soft counter 기준은 `docs/02_design/meta/synergy-breakpoints-and-soft-counters.md`를 따른다.

## family 카탈로그

### `synergy_human`

- counted tag: `human`
- affected targets: `ally teamwide`
- tags: `baseline`, `flex`, `sustain`
- 2 breakpoint: `phys_power +1`
- 3 breakpoint: `phys_power +2`, `heal_power +1`
- 4 breakpoint: `phys_power +3`, `attack_speed +0.08`
- stacking notes: 기본 화력 위에 sustain을 얹는 cumulative family다.
- prohibited overlap cases:
  - human 4-piece와 동일한 `phys_power + attack_speed` 묶음을 item unique나 augment 단일 source로 복제하지 않는다.
  - human family를 `always-on generic best choice`로 만들 정도의 무조건적 defensive layer를 추가하지 않는다.

### `synergy_beastkin`

- counted tag: `beastkin`
- affected targets: `ally teamwide`
- tags: `tempo`, `collapse`, `crit`
- 2 breakpoint: `attack_speed +0.08`
- 3 breakpoint: `attack_speed +0.14`, `move_speed +0.05`
- 4 breakpoint: `attack_speed +0.20`, `move_speed +0.08`, `crit_chance +0.02`
- stacking notes: tempo 진입과 finishing 확률을 동시에 누적한다.
- prohibited overlap cases:
  - beastkin 4-piece 위에 같은 run에서 unconditional global haste를 또 얹는 설계는 금지한다.
  - 2-piece만으로 `backline all-in` 정답 조합이 되게 만드는 보정은 금지한다.

### `synergy_undead`

- counted tag: `undead`
- affected targets: `ally teamwide`
- tags: `attrition`, `frontline`, `tenacity`
- 2 breakpoint: `max_health +2`
- 3 breakpoint: `max_health +3`, `resist +0.6`
- 4 breakpoint: `max_health +4`, `resist +1`, `tenacity +0.2`
- stacking notes: 긴 교전에서 가치가 커지는 cumulative attrition family다.
- prohibited overlap cases:
  - undead 4-piece와 동일한 `max_health + resist + tenacity` 번들을 permanent augment 1개로 재현하지 않는다.
  - vanguard defensive stack과 결합해 hard wall이 되지 않도록 additional protect rule을 family 안에 넣지 않는다.

### `synergy_vanguard`

- counted tag: `vanguard`
- affected targets: `ally teamwide`
- tags: `frontline`, `protect`, `anchor`
- 2 breakpoint: `armor +1`
- 3 breakpoint: `armor +1.5`, `protect_radius +0.2`
- 4 breakpoint: `armor +2`, `max_health +3`, `protect_radius +0.35`
- stacking notes: 보호 반경과 front durability가 함께 누적된다.
- prohibited overlap cases:
  - vanguard family 안에서 taunt redirect나 hard crowd control을 breakpoint reward로 직접 넣지 않는다.
  - undead와 합쳐도 front-only auto-win이 되지 않도록 protect radius 외의 별도 damage redirect를 추가하지 않는다.

### `synergy_duelist`

- counted tag: `duelist`
- affected targets: `ally teamwide`
- tags: `burst`, `execution`, `pressure`
- 2 breakpoint: `phys_power +1`
- 3 breakpoint: `phys_power +1.5`, `crit_chance +0.02`
- 4 breakpoint: `phys_power +2`, `crit_chance +0.03`, `phys_pen +1`
- stacking notes: burst와 execution 지표가 함께 누적되는 offensive family다.
- prohibited overlap cases:
  - duelist 4-piece 위에 같은 축의 `crit + phys_pen` burst rule을 support skill 하나로 추가하지 않는다.
  - 2-piece만으로 squishy backline kill-check가 확정되도록 base damage budget을 올리지 않는다.

### `synergy_ranger`

- counted tag: `ranger`
- affected targets: `ally teamwide`
- tags: `backline`, `lane`, `crit`
- 2 breakpoint: `attack_speed +0.08`
- 3 breakpoint: `attack_speed +0.12`, `attack_range +0.15`
- 4 breakpoint: `attack_speed +0.16`, `attack_range +0.22`, `crit_multiplier +0.12`
- stacking notes: lane control과 finishing quality를 같이 누적한다.
- prohibited overlap cases:
  - ranger family에 free reposition이나 untargetable 성격의 rule을 넣지 않는다.
  - marksman류 protected carry comp가 answerless가 되지 않도록 hard peel bonus를 family payload에 넣지 않는다.

### `synergy_mystic`

- counted tag: `mystic`
- affected targets: `ally teamwide`
- tags: `support`, `control`, `cooldown`
- 2 breakpoint: `heal_power +1`
- 3 breakpoint: `heal_power +1.5`, `mag_power +1`
- 4 breakpoint: `heal_power +2`, `mag_power +1.5`, `cooldown_recovery +0.06`
- stacking notes: sustain과 spell throughput이 함께 누적된다.
- prohibited overlap cases:
  - mystic 4-piece 위에 전원 광역 무조건 회복 rule을 같은 tier에서 추가하지 않는다.
  - cooldown recovery를 augment와 passive로 동시에 과증폭해 cast loop가 무한에 가까워지게 만들지 않는다.

## overlap 운영 규칙

- 같은 팀이 race family와 class family를 함께 달성하는 것은 허용한다.
- 다만 한 family의 4-piece identity를 다른 source가 통째로 복제하면 안 된다.
- item / passive / augment는 family의 방향을 보조할 수 있지만, family payload 자체를 다시 한 번 제공하면 overlap violation으로 본다.
