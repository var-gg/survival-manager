# skill catalog v1

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/combat/skill-catalog-v1.md`
- 관련문서:
  - `docs/02_design/combat/skill-authoring-schema.md`
  - `docs/02_design/meta/retrain-contract.md`
  - `docs/03_architecture/content-seed-assets.md`

## 목적

이 문서는 full skill catalog 후보와 role packet 기준 seed set을 기록한다.

## capacity vs live subset

- schema capacity: `80~100`
- v1 catalog target: `42`
- current live subset target:
  - active `12~18`
  - passive `6~10`

## guardian packet

- `Shield Slam` — `SingleTargetStrike`, `Melee`, `Guard`, `Control`
- `Guard Stance` — `SelfBuffStance`, `Guard`, `Recovery`
- `Interpose` — `BlinkRepositionUtility`, `Protect`, `Mobility`
- `Bastion Pulse` — `AllyBuffAuraPulse`, `Guard`, `Utility`
- `Hold the Line` — `OnHitTaken`, `Guard`, `Sustain`
- `Brace Step` — `BlinkRepositionUtility`, `Evade`, `Protect`

## bruiser packet

- `Heavy Swing` — `SingleTargetStrike`, `Melee`, `Burst`
- `Cleaving Rush` — `DashStrikeLunge`, `Dash`, `AoE`
- `Blood Roar` — `SelfBuffStance`, `Burst`, `Recovery`
- `Execution Chop` — `SingleTargetStrike`, `Execute`, `Melee`
- `Relentless` — `OnKill`, `Burst`, `Sustain`
- `Combat Dash` — `DashStrikeLunge`, `Engage`, `Mobility`

## duelist packet

- `Marking Thrust` — `SingleTargetStrike`, `Mark`, `Burst`
- `Feint Step` — `BlinkRepositionUtility`, `Evade`, `Mobility`
- `Riposte` — `OnBlock`, `Burst`, `Control`
- `Finisher` — `SingleTargetStrike`, `Execute`, `Burst`
- `Opening Exploiter` — `WhileCondition`, `Execute`, `Burst`
- `Side Dash` — `DashStrikeLunge`, `Engage`, `MaintainRange`

## ranger packet

- `Quick Shot` — `ProjectileShot`, `Projectile`, `Sustained`
- `Volley` — `MultiShotVolley`, `Projectile`, `AoE`
- `Pinning Shot` — `ProjectileShot`, `Projectile`, `Control`
- `Kill Zone` — `GroundArea`, `Projectile`, `Utility`
- `Kiting Instinct` — `OnHitTaken`, `MaintainRange`, `Mobility`
- `Back Roll` — `BlinkRepositionUtility`, `Evade`, `MaintainRange`

## arcanist packet

- `Arc Bolt` — `ProjectileShot`, `Lightning`, `Burst`
- `Frost Pulse` — `NovaPulse`, `Cold`, `Control`
- `Chain Spark` — `BeamRay`, `Lightning`, `Burst`
- `Arcane Ward` — `ShieldBarrierHeal`, `Barrier`, `Protect`
- `Mana Tension` — `OnSkillCast`, `Sustain`, `Burst`
- `Short Blink` — `BlinkRepositionUtility`, `Evade`, `MaintainRange`

## support packet

- `Smite` — `ProjectileShot`, `Holy`, `Burst`
- `Protective Prayer` — `ShieldBarrierHeal`, `Protect`, `Recovery`
- `Cleansing Light` — `ShieldBarrierHeal`, `Cleanse`, `Utility`
- `Consecrated Ground` — `GroundArea`, `Heal`, `Aura`
- `Grace Under Fire` — `WhileCondition`, `Protect`, `Recovery`
- `Faith Step` — `BlinkRepositionUtility`, `Evade`, `Protect`

## occult packet

- `Soul Bolt` — `ProjectileShot`, `Shadow`, `Burst`
- `Raise Servant` — `SummonDeployable`, `Summon`, `Utility`
- `Bone Wall` — `GroundArea`, `Control`, `Utility`
- `Grave Burst` — `NovaPulse`, `Shadow`, `AoE`
- `Death Dividend` — `OnKill`, `Summon`, `Sustain`
- `Shade Slip` — `BlinkRepositionUtility`, `Evade`, `Summon`

## active live subset 우선순위

- guardian 2
- bruiser 2
- duelist 2
- ranger 3
- arcanist 3
- support 2
- occult 1~2
