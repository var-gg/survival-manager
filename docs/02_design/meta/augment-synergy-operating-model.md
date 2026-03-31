# augment와 synergy 운영 모델

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/meta/augment-synergy-operating-model.md`
- 관련문서:
  - `docs/02_design/meta/augment-system.md`
  - `docs/02_design/meta/synergy-and-augment-taxonomy.md`
  - `docs/02_design/meta/reward-protection-and-acquisition-loop.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`

## 목적

이 문서는 synergy를 항상 존재하는 조합 문법으로, augment를 그 문법을 휘게 만드는 규칙으로 구분한다.

## 분리 규칙

- synergy는 race/class/tag count와 tier effect를 가진다.
- augment는 `combat`, `synergy`, `economy_loot`, `run_utility` 네 family로 분류한다.
- 영구 augment는 profile unlock + squad blueprint equip 구조를 따른다.

## offer 규칙

- 제안은 기본적으로 3지선다다.
- 동일 영구 augment와 동일 family 완전 중복은 hard exclude다.
- 인접 변형은 downweight 한다.
- 기본 분배는 on-board 1, flex 1, wildcard/econ 1이다.
- `SynergyLinked` bucket은 build bias tag를 반드시 가진다.
- protection tag와 build bias tag가 같은 의미를 중복 복제하면 안 된다.
