# 스킬 키워드, support modifier, 무기 제한

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/combat/skill-keywords-support-modifiers-and-weapon-restrictions.md`
- 관련문서:
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
  - `docs/02_design/meta/equipment-family-and-crafting-depth.md`
  - `docs/03_architecture/skill-tag-catalog-and-compatibility-resolution.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`

## 목적

이 문서는 current 4-slot compile contract 위에서 launch floor keyword catalog와 support modifier floor를 고정한다.
rule의 중심은 하드코딩 예외가 아니라 stable tag catalog다.

## canonical keyword catalog

### delivery / geometry

- `melee`
- `projectile`
- `aoe`
- `zone`
- `trap`
- `aura`

### function

- `strike`
- `burst`
- `heal`
- `shield`
- `dash`
- `guard`
- `mark`
- `cleanse`

### status / finish

- `burn`
- `bleed`
- `wound`
- `sunder`
- `slow`
- `silence`
- `execute`
- `pierce`
- `chain`

runtime asset에서는 위 키워드를 `tag_*` stable tag로 materialize한다.

## support modifier floor

| support id | include tags | exclude tags | required weapon | required class |
| --- | --- | --- | --- | --- |
| `support_brutal` | `strike`, `burst` | 없음 | 없음 | 없음 |
| `support_swift` | `projectile`, `dash` | 없음 | 없음 | 없음 |
| `support_piercing` | `projectile`, `pierce` | `heal`, `shield` | `bow` | 없음 |
| `support_echo` | `burst`, `aura` | 없음 | 없음 | 없음 |
| `support_lingering` | `zone`, `aoe` | 없음 | 없음 | 없음 |
| `support_purifying` | `heal`, `shield`, `cleanse` | `bleed`, `execute` | 없음 | `mystic` |
| `support_guarded` | `guard`, `shield` | 없음 | `shield` | `vanguard` |
| `support_executioner` | `strike`, `burst`, `execute` | `heal`, `shield` | `blade` | `duelist` |
| `support_longshot` | `projectile`, `mark` | 없음 | `bow` | `ranger` |
| `support_siphon` | `burst`, `burn` | 없음 | `focus` | `mystic` |
| `support_anchored` | `shield`, `guard`, `aura` | `dash` | `shield` | `vanguard` |
| `support_hunter_mark` | `mark`, `projectile` | `heal` | `bow` | `ranger` |

## weapon family restriction floor

### launch floor canonical family

- `shield`
- `blade`
- `bow`
- `focus`

### safe target expansion

- `greatblade`
- `polearm`

`greatblade`, `polearm`은 stable tag에만 먼저 올리고, launch floor content count에는 포함하지 않는다.

## class / weapon identity

| weapon family | 주 사용자 | 핵심 keyword |
| --- | --- | --- |
| `shield` | `vanguard` | `guard`, `shield`, `aura`, `mark` |
| `blade` | `duelist` | `strike`, `dash`, `bleed`, `execute` |
| `bow` | `ranger` | `projectile`, `trap`, `pierce`, `mark` |
| `focus` | `mystic` | `burst`, `heal`, `cleanse`, `zone` |

## 운영 규칙

- compatibility는 `include tags`, `exclude tags`, `required weapon tags`, `required class tags` 조합으로만 푼다.
- `support` slot은 계속 하나만 유지한다.
- `LoadoutCompiler`는 normalized tag 집합을 compile output과 hash에 보존한다.
- `SkillDefinitionAsset` authoring은 stable tag catalog 밖의 ad-hoc string을 허용하지 않는다.

## deferred

- summon, totem, stealth, ricochet 같은 추가 keyword family
- launch floor 밖 weapon family 실전 authoring
- support slot 다중 장착
