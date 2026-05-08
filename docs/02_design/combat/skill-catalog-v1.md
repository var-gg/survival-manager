# skill catalog v1

- 상태: deprecated
- deprecated 일자: 2026-05-06
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: 없음 (아래 대체 SoT 참조)
- 대체 SoT:
  - `docs/02_design/systems/launch-floor-content-matrix.md` — 12 archetype별 Loop A slot anchor (`skill_warden_utility`, `skill_guardian_core` 등)
  - `docs/02_design/combat/skill-authoring-schema.md` — skill 필수 필드, template, tag layer 관계
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md` — skill kind / delivery / target rule taxonomy + 데미지 수식
- 관련문서:
  - pindoc Decision: [고유명사 ID/Label 분리 baseline](http://localhost:5830/p/survival-manager/wiki/고유명사-id-label-분리-모든-layer-baseline)

## deprecation 사유

본 문서의 7-packet seed(`guardian / bruiser / duelist / ranger / arcanist / support / occult`)와 `launch-floor-content-matrix.md`의 12 archetype Loop A anchor(`skill_warden_utility` 등)가 다른 namespace를 쓴다. matrix가 V1 launch floor 기준이라 본 문서의 packet seed는 archetype anchor와 1:1 매칭되지 않는다.

12 archetype 기준 새 catalog는 narrative reskin이 settled된 뒤 별도 Task로 propose한다(ID는 보존, presentation layer만 reskin). 본 문서는 audit log로만 보존한다.

---

## 목적 (deprecated)

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
