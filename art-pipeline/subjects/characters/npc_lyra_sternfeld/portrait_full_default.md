---
slug: npc_lyra_sternfeld--portrait_full_default
kind: character_portrait_full
subject_id: npc_lyra_sternfeld
variant: portrait_full
emotion: default
refs:
  - npc_lyra_sternfeld
  - hero_dawn_priest:portrait_full_style_seed_best
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: rendered
---

# 선영 (宣英) / Lyra Sternfeld - Portrait Full Default (v3 - P09 canon + house style seed)

## 생성 의도

첫 번째 reference는 선영의 P09 canon이다. 두 번째 reference는 단린 best full-body illustration seed이며, 작가감과 완성도만 맞춘다. 선영은 단린과 같은 솔라룸 사제 계열이지만, warm ivory/gold가 아니라 cold ivory/violet emblem으로 분리되는 광신 사제다.

## prompt 명세

```prompt
Create one full-body polished JRPG character illustration of Lyra Sternfeld / Seonyeong, using the attached references with strict priority:

REFERENCE PRIORITY
1. The attached P09 model reference for npc_lyra_sternfeld is CANON for identity, outfit slots, silhouette, hair, weapon, shield absence, and color zoning.
2. The attached Dawn Priest full-body illustration is STYLE ONLY. Match its professional 2D illustration quality, elegant face polish, confident hand-painted linework, layered cloth rendering, clean silhouette outline, and refined character-art finish. Do NOT copy Dawn Priest costume, sword, shield, pose, colors, face, or warm gold priest mood.

CORE IDENTITY
- Female fanatic priest of Solarum, cold and ceremonial.
- Cold blonde hair, hair style 7, no facial hair, light skin, refined but severe beauty.
- Armor_009_Head ritual headpiece, Armor_007_Chest priest robe, Armor_008_Arm violet ritual sleeves, Armor_007_Waist, Armor_007_Leg.
- Weapon: Staff_004 ceremonial staff only.
- Shield slot is empty. No shield, no sword.
- Distinct from Danrin: colder expression, colder blonde, cold ivory cloth, violet emblem/ritual accents, staff instead of sword and shield.

P09 CANON OUTFIT LOCK
- Head: Armor_009_Head ritual authority piece, cold ivory and violet.
- Chest: Armor_007_Chest priest robe, cold ivory base.
- Arm: Armor_008_Arm violet ritual caster sleeves, visibly different from the robe.
- Waist: Armor_007_Waist priest sash, darker violet-lavender lower zone.
- Leg: Armor_007_Leg robe lower garment.
- Weapon: Staff_004, ceremonial staff with violet/gold glint, not a spear.

COLOR ANCHORS
- Hair: main #D6CBAE, shadow #BFAE82, highlight #F0E6C8.
- Head: main #D8D2C2, violet #8D74C9, cool trim #B8B9C2.
- Chest: main #D8D2C2, violet emblem #8D74C9, blonde echo #D6CBAE.
- Arms: main #C8C0D2, shadow #6E6480, violet signal #8D74C9.
- Waist: main #6E6480, deep violet #3C2D48, pale accent #D6CBAE.
- Legs: main #5C5248, shadow #3B322D, violet accent #8D74C9.
- Staff: main #D8D2C2, violet #8D74C9, gold #D4AF37, subtle emission #32205A.

FACE AND EXPRESSION
- Controlled fanatic calm, beautiful but unsettling.
- Lips closed, gaze unwavering, no warm smile.
- Eyes focused as if judging the viewer by doctrine.
- Make the face professional and appealing, not a literal 3D conversion.

POSE AND COMPOSITION
- Full body, three-quarter front view, both feet visible.
- Staff held upright or diagonally in one hand, fully visible enough to read as Staff_004.
- No shield, no sword, no floating holy symbol.
- Cold ivory robe and violet sleeves must be readable at thumbnail size.
- Vertical 2:3 composition, 1024 x 1536 PNG.

STYLE TARGET
- High-end hand-painted 2D game character illustration in the same internal house style as the Dawn Priest seed.
- Painterly cloth folds, polished face, crisp costume zone separation, clean silhouette, subtle cinematic rim light.
- Translate the P09 model into a confident production illustration while preserving canon.

BACKGROUND AND CLEANUP
- Solid #FF00FF magenta background, flat fill only.
- 1-2 px clean dark outline around the whole subject for chroma cleanup.
- No cast shadow, no gradient, no environment, no magenta contamination on subject.

NEGATIVE LOCKS
- No sword, no shield, no paladin shield, no warm Danrin gold palette.
- No copied Dawn Priest armor, no rose-gold mood, no gentle healer expression.
- No giant halo, no wings, no church backdrop, no extra ornaments outside P09.
- No saturated purple blob; keep cold ivory, violet sleeve, and dark waist separated by value.
```
