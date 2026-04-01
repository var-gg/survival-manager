# 스킬 태그 카탈로그와 호환성 resolve

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/03_architecture/skill-tag-catalog-and-compatibility-resolution.md`
- 관련문서:
  - `docs/02_design/combat/skill-keywords-support-modifiers-and-weapon-restrictions.md`
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## 목적

이 문서는 stable tag catalog가 `SkillDefinitionAsset`, item, archetype, compile output, validator에서 같은 canonical root를 보도록 정의한다.

## canonical tag root

- keyword와 flex passive modifier는 `StableTags` asset root를 canonical catalog로 사용한다.
- launch floor keyword는 `tag_melee`, `tag_projectile`, `tag_aoe`, `tag_zone`, `tag_trap`, `tag_aura`, `tag_strike`, `tag_burst`, `tag_heal`, `tag_shield_skill`, `tag_dash`, `tag_guard`, `tag_mark`, `tag_cleanse`, `tag_burn`, `tag_bleed`, `tag_wound`, `tag_sunder`, `tag_slow`, `tag_silence`, `tag_execute`, `tag_pierce`, `tag_chain`을 사용한다.
- safe target weapon family는 `tag_greatblade`, `tag_polearm`로 먼저 예약한다.

## authoring contract

`SkillDefinitionAsset`은 아래 호환성 필드를 가진다.

- `CompileTags`
- `RuleModifierTags`
- `SupportAllowedTags`
- `SupportBlockedTags`
- `RequiredWeaponTags`
- `RequiredClassTags`
- `AppliedStatuses`
- `CleanseProfileId`

item 쪽은 아래 authoring field를 가진다.

- `WeaponFamilyTag`
- `GrantedSkillId`
- `UniqueRuleModifierTag`
- `CraftCurrencyTag`
- `AllowedCraftOperations`

## compile 계약

- `LoadoutCompiler`는 authored tag를 normalize해 compile output에 남긴다.
- compile hash는 normalized tag 집합을 포함해야 한다.
- same authored tags => same compile output => same compile hash를 만족해야 한다.
- ally compile flow는 계속 `SquadBlueprint -> LoadoutCompiler -> BattleLoadoutSnapshot`이다.

## validator 규칙

- invalid flex passive compatibility 금지
- invalid weapon-family lock 금지
- flex passive modifier asset는 canonical `FlexPassive` slot만 사용
- include / exclude 같은 tag 중복 사용 금지
- missing cleanse profile ref 금지
- incompatible skill / weapon family 조합 금지

## runtime 사용 지점

- `RuntimeCombatContentLookup`가 stable tag catalog를 snapshot에 로드한다.
- `BattleSkillSpec`는 normalized compile tags와 status/cleanse 정보를 함께 가진다.
- `ContentDefinitionValidator`는 authoring drift를 막고, `LoadoutCompiler`는 deterministic compile output을 보장한다.
