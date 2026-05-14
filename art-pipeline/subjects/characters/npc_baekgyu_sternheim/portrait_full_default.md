---
slug: npc_baekgyu_sternheim--portrait_full_default
kind: character_portrait_full
subject_id: npc_baekgyu_sternheim
variant: portrait_full
emotion: default
refs:
  - npc_baekgyu_sternheim
  - hero_dawn_priest:portrait_full_style_seed_best
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: rendered
---

# 백규 (白圭) / Baekgyu Sternheim - Portrait Full Default (v3 - P09 canon + house style seed)

## 생성 의도

첫 번째 reference는 백규의 P09 canon이다. 두 번째 reference는 단린 best full-body illustration seed이며 style-only로 사용한다. 백규는 솔라룸 officer-scholar boss로, officer grey/ivory/violet 권위와 실전형 한쪽 팔이 보여야 한다.

## prompt 명세

```prompt
Create one full-body polished JRPG character illustration of Baekgyu Sternheim, using the attached references with strict priority:

REFERENCE PRIORITY
1. The attached P09 model reference for npc_baekgyu_sternheim is CANON for identity, outfit slots, silhouette, hair, facial hair, weapon, shield absence, and color zoning.
2. The attached Dawn Priest full-body illustration is STYLE ONLY. Match its professional 2D illustration quality, elegant face polish, confident linework, painterly rendering, and clean silhouette. Do NOT copy Dawn Priest costume, sword/shield setup, pose, colors, or priest mood.

CORE IDENTITY
- Male Solarum scholar boss / officer-aristocrat, composed political authority.
- Silver-white hair, facial hair, light skin, controlled older elegance.
- Armor_012_Head, Armor_012_Chest, Armor_012_Waist, Armor_012_Leg formal officer set.
- Armor_005_Arm practical duelist arm, less ornate than the rest.
- Weapon: Sword_005 noble sword only.
- Shield slot is empty. No shield, no bow, no staff.
- Distinct from Pale Executor: Solarum officer grey and ivory, noble violet glint, sword user, not an exile archer.

P09 CANON OUTFIT LOCK
- Head: Armor_012_Head formal officer head/upper authority piece.
- Chest: Armor_012_Chest officer-scholar armor, structured and formal.
- Arm: Armor_005_Arm practical field/duelist arm, visibly lower and more functional.
- Waist: Armor_012_Waist formal officer waist.
- Leg: Armor_012_Leg formal officer leg armor.
- Weapon: Sword_005 noble sword, not a staff, bow, or heavy shield.

COLOR ANCHORS
- Hair: main #D8D4C8, shadow #AFA89A, highlight #F2EEE0.
- Head: main #7E8490, ivory #D8CFB8, noble violet #7D5CC7.
- Chest: main #7E8490, ivory #D8CFB8, noble violet #7D5CC7.
- Arm: main #6F737A, shadow #3E4652, ivory #D8CFB8.
- Waist: main #3E4652, deep #242A31, violet #7D5CC7.
- Legs: main #5C6370, shadow #353B45, ivory #D8CFB8.
- Sword: main #424047, ivory/gold #D8CFB8, violet glint #7D5CC7, subtle emission #30204A.

FACE AND EXPRESSION
- Calm, intelligent, politically dangerous.
- Lips closed, eyes composed, no rage and no smile.
- Looks like someone who already calculated the outcome.
- Make the face refined and professional, not a literal 3D conversion.

POSE AND COMPOSITION
- Full body, three-quarter front view, both feet visible.
- Noble sword held low or resting at one side, readable as Sword_005.
- No shield, no bow, no staff.
- Officer armor, practical arm, silver hair/facial hair, and violet glint must be readable.
- Vertical 2:3 composition, 1024 x 1536 PNG.

STYLE TARGET
- Same internal house style as the Dawn Priest seed: polished hand-painted JRPG character art, refined face rendering, painterly metal/cloth folds, crisp silhouette, controlled cinematic rim light.
- Preserve P09 canon while upgrading it into professional 2D illustration.

BACKGROUND AND CLEANUP
- Solid #FF00FF magenta background, flat fill only.
- 1-2 px clean dark outline around the whole subject for chroma cleanup.
- No cast shadow, no gradient, no environment, no magenta contamination on subject.

NEGATIVE LOCKS
- No bow, no staff, no shield, no exile archer silhouette.
- No copied Pale Executor palette, no ghostly necromancer motif.
- No oversized crown, no throne, no background architecture, no extra books.
- No monochrome grey blob; keep officer grey, ivory trim, and violet glint separated by value.
```
