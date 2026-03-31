# skill authoring schema

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/combat/skill-authoring-schema.md`
- 관련문서:
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
  - `docs/02_design/combat/skill-catalog-v1.md`
  - `docs/02_design/meta/skill-acquisition-and-retrain.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## 목적

이 문서는 skill authoring이 어떤 필드를 가져야 하고, tag만으로 닫히지 않는 의도를 어디에 기록해야 하는지 정의한다.

## canonical formula

```text
Skill = Template + Tags + Payload + AI Hints + Presentation Hooks
```

- template는 전달 방식과 기본 shape를 잠근다.
- tags는 검색과 restriction, synergy를 담당한다.
- payload는 damage / heal / shield / status / summon 실효를 담당한다.
- AI hints는 사용 타이밍과 score bias를 담는다.
- presentation hook은 animation / vfx / sfx 분리를 담당한다.

## compile boundary

- battle compile contract는 계속 `core_active / utility_active / passive / support` 4-slot이다.
- `basic attack profile`은 skill slot이 아니라 archetype / meta 소유 프로필이다.
- fixed core / flex policy는 recruitment layer에서만 바뀌고, compile 결과는 4-slot으로 normalize된다.

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
| `TemplateType` | template closure |
| `Kind`, `SlotKind`, `DamageType`, `Delivery`, `TargetRule` | compile-visible taxonomy |
| `RangeMin`, `RangeMax`, `Radius`, `Width`, `ArcDegrees` | shape / distance |
| `Power`, `PowerFlat`, coeffs | numeric payload |
| `CastWindupSeconds`, `CooldownSeconds`, `RecoverySeconds`, `ResourceCost` | cadence |
| `AppliedStatuses`, `CleanseProfileId` | status payload |
| `CompileTags`, `RuleModifierTags` | search / restriction / synergy |
| `AiIntents`, `AiScoreHints` | evaluator hint |
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

- template가 `LegacyDerived`가 아니면 range / shape / AI intent를 함께 채운다.
- `support` slot은 support compatibility tag를 반드시 가진다.
- `blink / dash / roll` 계열은 movement style이 아니라 `purpose`와 `distance band`를 함께 기록한다.
- presentation hook이 비어 있어도 compile은 가능하지만, design catalog에는 hook placeholder를 남긴다.

## non-goal

- template별 full runtime resolver rollout
- 자유선택형 스킬 트리 전체 구현
