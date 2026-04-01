# skill authoring schema

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/combat/skill-authoring-schema.md`
- 관련문서:
  - `docs/02_design/combat/authority-matrix.md`
  - `docs/02_design/combat/resource-cadence-loadout.md`
  - `docs/02_design/combat/targeting-and-ai-vocabulary.md`
  - `docs/02_design/combat/summon-ownership-and-deployables.md`
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
  - `docs/02_design/combat/skill-catalog-v1.md`
  - `docs/02_design/meta/skill-acquisition-and-retrain.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## 목적

이 문서는 Loop A 기준의 action authoring 필드와, tag만으로 닫히지 않는 의도를 어디에 기록해야 하는지 정의한다.

## canonical formula

```text
Action = Slot + Template + EffectDescriptors + TargetRule + Presentation Hooks
```

- slot은 cadence와 topology를 잠근다.
- template는 전달 방식과 기본 shape를 잠근다.
- effect descriptor는 authority, scope, capability를 담는다.
- target rule은 selector, fallback, lock, hysteresis 해석을 닫는다.
- presentation hook은 animation / vfx / sfx 분리를 담당한다.

## compile boundary

- battle compile contract는 `BasicAttack / SignatureActive / FlexActive / SignaturePassive / FlexPassive / MobilityReaction` 6-slot이다.
- `SkillDefinitionAsset`은 active slot만 소유하고, `BasicAttackDefinition`, `PassiveDefinition`, `MobilityDefinition`이 나머지 slot을 소유한다.
- recruit/retrain은 `FlexActive`, `FlexPassive`만 바꿀 수 있다.

## template catalog

### active template

1. `SingleTargetStrike`
2. `SweepCleave`
3. `DashStrikeLunge`
4. `ProjectileShot`
5. `MultiShotVolley`
6. `BeamRay`
7. `ConeCast`
8. `GroundArea`
9. `NovaPulse`
10. `SelfBuffStance`
11. `AllyBuffAuraPulse`
12. `ShieldBarrierHeal`
13. `SummonDeployable`
14. `BlinkRepositionUtility`

### passive / trigger template

1. `OnAttack`
2. `OnCrit`
3. `OnBlock`
4. `OnHitTaken`
5. `OnKill`
6. `OnSkillCast`
7. `WhileCondition`
8. `PeriodicAura`

## 필수 필드

| 필드 | 목적 |
| --- | --- |
| `Id`, `NameKey`, `DescriptionKey` | canonical 식별과 localization |
| `AuthorityLayer` | effect authority 판별 |
| `TemplateType` | template closure |
| `ActionSlotKind`, `ActivationModel`, `ActionLane`, `ActionLockRule` | cadence / topology / arbitration |
| `Kind`, `DamageType`, `Delivery`, `TargetRule` | compile-visible taxonomy |
| `Effects` | `EffectScope`, `EffectCapability` 기반 payload 선언 |
| `RangeMin`, `RangeMax`, `Radius`, `Width`, `ArcDegrees` | shape / distance |
| `Power`, `PowerFlat`, coeffs | numeric payload |
| `CastWindupSeconds`, `CooldownSeconds`, `RecoverySeconds` | cadence |
| `AppliedStatuses`, `CleanseProfileId` | status payload |
| `CompileTags`, `RuleModifierTags` | search / restriction / synergy |
| `TargetRuleData` | selector / fallback / lock / cluster |
| `SummonProfile` | summon/deployable ownership 규칙 |
| `AnimationHookId`, `VfxHookId`, `SfxHookId` | presentation binding |
| `PowerBudget` | balance language |
| `LearnSource` | recruit / retrain / item / augment provenance |

## tag layer

### delivery

- `Melee`
- `Projectile`
- `Beam`
- `AoE`
- `Nova`
- `Dash`
- `Blink`
- `Summon`
- `Aura`
- `Channel`

### school / payload

- `Physical`
- `Fire`
- `Cold`
- `Lightning`
- `Shadow`
- `Holy`
- `Bleed`
- `Poison`

### behavior

- `Burst`
- `Sustained`
- `Guard`
- `Mobility`
- `Control`
- `Execute`
- `Utility`
- `Recovery`

## authoring rule

- template가 `LegacyDerived`가 아니면 range / shape와 target rule을 함께 채운다.
- `SignatureActive`는 반드시 `ActivationModel = Energy`와 `ActionLane = Primary`를 가진다.
- `FlexActive`는 `ActivationModel = Cooldown` 또는 `Trigger`만 허용한다.
- `MobilityReaction`은 `ActivationModel = Trigger`, `ActionLane = Reaction`을 가져야 한다.
- `FlexPassive` modifier 계열은 compatibility tag를 반드시 가진다.
- `blink / dash / roll` 계열은 movement style이 아니라 `purpose`와 `distance band`를 함께 기록한다.
- presentation hook이 비어 있어도 compile은 가능하지만, design catalog에는 hook placeholder를 남긴다.

## non-goal

- template별 full runtime resolver rollout
- 자유선택형 스킬 트리 전체 구현
