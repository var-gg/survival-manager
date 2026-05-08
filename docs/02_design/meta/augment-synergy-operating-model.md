# augment와 synergy 운영 모델

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-08
- 소스오브트루스: `docs/02_design/meta/augment-synergy-operating-model.md`
- 관련문서:
  - `docs/02_design/meta/augment-system.md`
  - `docs/02_design/meta/synergy-and-augment-taxonomy.md`
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`

## 목적

이 문서는 synergy를 항상 존재하는 조합 문법으로, augment를 그 문법을 휘게 만드는 규칙으로 구분한다.

## 분리 규칙

- synergy는 race/class/tag count와 tier effect를 가진다.
- augment는 player-facing `HeroRewrite`, `TacticalRewrite`, `ScalingEngine`, `EconomyAndLoot`, `SynergyPact` bucket으로 제안 표면을 나눈다. runtime category는 여전히 `combat`, `synergy`, `economy_loot`, `run_utility` 같은 낮은 계층 분류를 보조로 쓴다.
- 영구 augment는 profile unlock + squad blueprint equip 구조를 따른다.

## offer 규칙

- 제안은 기본적으로 3지선다다.
- 동일 영구 augment와 동일 family 완전 중복은 hard exclude다.
- 인접 변형은 downweight 한다.
- 기본 분배는 on-board 1, flex/tactical 1, scaling/econ/synergy 1이다.
- `SynergyPact` bucket은 build bias tag를 반드시 가진다.
- protection tag와 build bias tag가 같은 의미를 중복 복제하면 안 된다.
