---
slug: npc_silent_moon--portrait_full_default
kind: character_portrait_full
subject_id: npc_silent_moon
variant: portrait_full
emotion: default
refs:
  - npc_silent_moon
  - hero_dawn_priest:portrait_full_style_seed_best
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: rendered
---

# 침월 (沉月) / Silent Moon - Portrait Full Default (v3 - P09 canon + house style seed)

## 생성 의도

첫 번째 reference는 침월의 P09 canon이다. 두 번째 reference는 단린 best full-body illustration seed이며 style-only로 사용한다. 침월은 그물 결사의 moon witch hardliner로, 공한의 smoky violet/cyan archer보다 차갑고 닫힌 lavender/eggplant/blue glow를 가져야 한다.

## prompt 명세

```prompt
Create one full-body polished JRPG character illustration of Silent Moon / Chimweol, using the attached references with strict priority:

REFERENCE PRIORITY
1. The attached P09 model reference for npc_silent_moon is CANON for identity, outfit slots, silhouette, hair, weapon, shield absence, and color zoning.
2. The attached Dawn Priest full-body illustration is STYLE ONLY. Match its professional 2D illustration quality, elegant face polish, painterly cloth rendering, clean silhouette, and refined character-art finish. Do NOT copy Dawn Priest costume, colors, sword, shield, pose, or priest mood.

CORE IDENTITY
- Female Lattice Order silent moon hardliner, closed and lunar.
- Pale lavender hair, light skin, cool violet eyes, no facial hair.
- Armor_008_Head, Armor_008_Chest, Armor_008_Arm, Armor_008_Leg pointed moon mage silhouette.
- Armor_009_Waist ceremonial sash layered around the middle.
- Weapon: Staff_002 silence staff only.
- Shield slot is empty. No shield, no bow, no sword.
- Distinct from Echo Savant: colder moon lavender, staff caster, pointed mage silhouette, no archer bow.

P09 CANON OUTFIT LOCK
- Head: Armor_008_Head pointed mage headpiece.
- Chest: Armor_008_Chest moon robe, pale lavender-gray and eggplant value split.
- Arm: Armor_008_Arm matching moon mage sleeves.
- Waist: Armor_009_Waist long ceremonial sash, visibly different and paler.
- Leg: Armor_008_Leg dark moon caster boots.
- Weapon: Staff_002, blue-violet glow, not a bow or spear.

COLOR ANCHORS
- Hair: main #C7C0D8, shadow #6F5D88, highlight #F0EAF8.
- Head: main #AFA7B8, eggplant #3F304D, blue-violet #8EA0FF.
- Chest: main #AFA7B8, eggplant #3F304D, pale trim #D6D2E3.
- Arms: main #AFA7B8, shadow #4A3A5C, blue-violet #8EA0FF.
- Waist: main #D6D2E3, secondary #8A8198, eggplant #3F304D.
- Legs: main #3F304D, deep #241B2D, lavender #AFA7B8.
- Staff: main #4D435A, blue-violet #8EA0FF, pale trim #D6D2E3, subtle emission #26365A.

FACE AND EXPRESSION
- Quiet hardliner calm, restrained and closed.
- Lips closed, gaze slightly cold, no soft smile.
- The expression should feel like a vow kept in silence.
- Make the face polished and appealing, not a literal 3D conversion.

POSE AND COMPOSITION
- Full body, three-quarter front view, both feet visible.
- Staff held upright or slightly diagonal, clearly Staff_002.
- Keep pointed mage silhouette, pale waist sash, and blue-violet staff accent readable.
- No shield, no bow, no sword, no mirror shield.
- Vertical 2:3 composition, 1024 x 1536 PNG.

STYLE TARGET
- Same internal house style as the Dawn Priest seed: high-end hand-painted JRPG character illustration, refined face, painterly folds, crisp silhouette, subtle rim light.
- Preserve P09 canon while upgrading it into professional 2D illustration.

BACKGROUND AND CLEANUP
- Solid #FF00FF magenta background, flat fill only.
- 1-2 px clean dark outline around the whole subject for chroma cleanup.
- No cast shadow, no gradient, no environment, no magenta contamination on subject.

NEGATIVE LOCKS
- No bow, no shield, no sword, no mirror, no quiver.
- No copied Echo Savant archer costume, no warm cyan observer mood.
- No giant moon disc, no floating rune field, no extra tattoos or text.
- No purple blob; keep moon lavender, eggplant, pale sash, and blue glow separated by value.
```
