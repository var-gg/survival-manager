---
slug: npc_black_vellum--portrait_full_default
kind: character_portrait_full
subject_id: npc_black_vellum
variant: portrait_full
emotion: default
refs:
  - npc_black_vellum
  - hero_dawn_priest:portrait_full_style_seed_best
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: rendered
---

# 흑지 (黑紙) / Black Vellum - Portrait Full Default (v3 - P09 canon + house style seed)

## 생성 의도

첫 번째 reference는 흑지의 P09 canon이다. 두 번째 reference는 단린 best full-body illustration seed이며 style-only로 사용한다. 흑지는 묵향과 robe 계열을 공유하지만, black hair, parchment/ink, low emerald glow로 강경 단죄자처럼 분리되어야 한다.

## prompt 명세

```prompt
Create one full-body polished JRPG character illustration of Black Vellum / Heukji, using the attached references with strict priority:

REFERENCE PRIORITY
1. The attached P09 model reference for npc_black_vellum is CANON for identity, outfit slots, silhouette, hair, weapon, shield absence, and color zoning.
2. The attached Dawn Priest full-body illustration is STYLE ONLY. Match its professional 2D illustration quality, elegant face polish, confident linework, painterly cloth rendering, and clean silhouette. Do NOT copy Dawn Priest costume, colors, sword, shield, pose, or warm priest mood.

CORE IDENTITY
- Female Pale Conclave black vellum judicator, severe memory-law hardliner.
- Black long hair, light skin, controlled expression.
- Armor_009_Head, Armor_009_Chest, Armor_009_Waist parchment robe baseline.
- Armor_011_Arm dark condemner sleeve and Armor_008_Leg dark caster boots.
- Weapon: Staff_002 condemnation staff only.
- Shield slot is empty. No shield, no sword, no bow.
- Distinct from Grave Hexer: colder parchment/ink palette, darker arm, lower emerald glow, no warm jade kindness.

P09 CANON OUTFIT LOCK
- Head: Armor_009_Head ritual headpiece, parchment and ink.
- Chest: Armor_009_Chest robe, parchment grey with black/ink secondary zone.
- Arm: Armor_011_Arm black sleeve/rogue arm, stronger hardliner silhouette.
- Waist: Armor_009_Waist dark ritual waist layer.
- Leg: Armor_008_Leg dark caster boots.
- Weapon: Staff_002, low emerald accent, not a spear or scythe.

COLOR ANCHORS
- Hair: main #151517, shadow #050508, highlight #34343A.
- Head: main #AFA79A, ink #202022, emerald #2E5A4F.
- Chest: main #AFA79A, ink #202022, parchment shadow #5A5148.
- Arm: main #4A4648, shadow #202022, emerald #2E5A4F.
- Waist: main #3E3A36, deep ink #151517, parchment #AFA79A.
- Leg: main #56505C, deep violet #2A2434, emerald #2E5A4F.
- Staff: main #3B3745, emerald #2E5A4F, parchment #AFA79A, subtle emission #123028.

FACE AND EXPRESSION
- Calm, judicial, uncompromising.
- Lips closed, eyes sharp but not screaming.
- The expression should feel like a quiet sentence being passed.
- Make the face refined and professional, not a literal 3D conversion.

POSE AND COMPOSITION
- Full body, three-quarter front view, both feet visible.
- Staff held upright or slightly diagonal, clearly Staff_002.
- No shield, no sword, no skull props, no giant spell circle.
- Black hair, parchment robe, dark arm sleeve, and emerald staff glint must be readable.
- Vertical 2:3 composition, 1024 x 1536 PNG.

STYLE TARGET
- Same internal house style as the Dawn Priest seed: polished hand-painted JRPG character art, elegant face rendering, crisp silhouette, painterly robe folds, controlled cinematic rim light.
- Preserve P09 canon while upgrading it into professional 2D illustration.

BACKGROUND AND CLEANUP
- Solid #FF00FF magenta background, flat fill only.
- 1-2 px clean dark outline around the whole subject for chroma cleanup.
- No cast shadow, no gradient, no environment, no magenta contamination on subject.

NEGATIVE LOCKS
- No sword, no shield, no bow, no scythe, no skulls, no necromancer props.
- No copied Grave Hexer hat/personality, no warm bone-jade palette.
- No giant runic circle, no floating paper swarm, no extra text symbols.
- No pure black blob; keep parchment, ink, dark arm, and emerald accent separated by value.
```
