---
slug: npc_grey_fang--portrait_full_default
kind: character_portrait_full
subject_id: npc_grey_fang
variant: portrait_full
emotion: default
refs:
  - npc_grey_fang
  - hero_dawn_priest:portrait_full_style_seed_best
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: rendered
---

# 회조 (灰爪) / Grey Fang - Portrait Full Default (v3 - P09 canon + house style seed)

## 생성 의도

첫 번째 reference는 회조의 P09 canon이다. 두 번째 reference는 단린 best full-body illustration seed이며 style-only로 사용한다. 회조는 이빨바람과 같은 이리솔 가죽 전사 계열이지만, frost hair와 desaturated taupe leather로 돌아오지 않는 형제/분리파 duelist로 읽혀야 한다.

## prompt 명세

```prompt
Create one full-body polished JRPG character illustration of Grey Fang / Hoejo, using the attached references with strict priority:

REFERENCE PRIORITY
1. The attached P09 model reference for npc_grey_fang is CANON for identity, outfit slots, silhouette, hair, facial hair, weapon, shield absence, and color zoning.
2. The attached Dawn Priest full-body illustration is STYLE ONLY. Match its professional 2D illustration quality, line confidence, painterly rendering, face polish, and clean silhouette. Do NOT copy Dawn Priest costume, colors, sword/shield setup, pose, or priest mood.

CORE IDENTITY
- Male IrisoI grey fang duelist, older veteran and estranged brother figure.
- Frost grey wild hair, visible facial hair/beard, no helmet or head armor.
- Armor_004_Chest and Armor_004_Waist leather clan baseline, Armor_011_Arm darker rogue arm, Armor_003_Leg travel boots.
- Weapon: Sword_001 old hunting blade only.
- Shield slot is empty. No shield, no bow.
- Distinct from Pack Raider: colder hair, desaturated taupe leather, darker rogue arm, less warm amber.

P09 CANON OUTFIT LOCK
- Head: none; leave hair and beard visible as the silhouette anchor.
- Chest: Armor_004_Chest worn leather, desaturated taupe.
- Arm: Armor_011_Arm dark rogue arm, cooler and more severe than the chest.
- Waist: Armor_004_Waist leather belt/waist wrap.
- Leg: Armor_003_Leg travel boots, weathered and practical.
- Weapon: Sword_001, old hunting knife/short sword, no ornate noble sword.

COLOR ANCHORS
- Hair: main #B0B3B0, shadow #817A74, highlight #D4D0CA.
- Chest: main #8B7A68, shadow #5C5248, trim #C1B59C.
- Arm: main #6A625F, shadow #3E3A36, cool trim #8F9698.
- Waist: main #5C5248, leather shadow #3A2A1F, chest echo #8B7A68.
- Leg: main #6F665C, leather #4B3A2B, cool trim #8F9698.
- Weapon: main #5B5A58, grip #6A4A35, worn trim #C1B59C.

FACE AND EXPRESSION
- Weathered, restrained, and watchful.
- Eyes steady, mouth closed, no smile.
- Veteran sadness under control, not villainous exaggeration.
- Make the face attractive and readable, not a literal 3D conversion.

POSE AND COMPOSITION
- Full body, three-quarter front view, both feet visible.
- Sword held low at one side or across the body in a calm duelist rest pose.
- No shield, no bow, no quiver, no helmet.
- Hair/beard, dark rogue arm, and travel boots must remain readable.
- Vertical 2:3 composition, 1024 x 1536 PNG.

STYLE TARGET
- Same internal house style as the Dawn Priest seed: polished hand-painted JRPG character art, crisp silhouette, refined face, painterly leather folds, controlled highlights.
- Preserve P09 canon while upgrading it into professional 2D illustration.

BACKGROUND AND CLEANUP
- Solid #FF00FF magenta background, flat fill only.
- 1-2 px clean dark outline around the whole subject for chroma cleanup.
- No cast shadow, no gradient, no environment, no magenta contamination on subject.

NEGATIVE LOCKS
- No shield, no bow, no helmet, no warm orange leader palette.
- No copied Pack Raider expression or amber-heavy costume.
- No assassin pure-black redesign, no fantasy skull ornaments, no extra cape.
- No saturated cyan outfit; cyan/cool trim must stay subtle.
```
