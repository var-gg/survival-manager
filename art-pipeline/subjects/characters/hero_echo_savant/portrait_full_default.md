---
slug: hero_echo_savant--portrait_full_default
kind: character_portrait_full
subject_id: hero_echo_savant
variant: portrait_full
emotion: default
refs:
  - hero_echo_savant
  - hero_dawn_priest:portrait_full_style_seed_best
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: rendered
---

# 공한 (Echo Savant) - Portrait Full Default (v3 - P09 canon + house style seed)

## 생성 의도

첫 번째 reference는 공한의 P09 canon이다. 의상 slot, 머리, 체형, 무기, 색상 구획은 이 이미지를 따른다.
두 번째 reference `portrait_full_style_seed_best`는 단린 best full-body illustration seed다. 이것은 작가감, 선 밀도, 채색 완성도, 얼굴 polish, cloth rendering rhythm만 맞추는 style-only seed이며 단린의 의상, 포즈, 무기, 색상은 가져오지 않는다.

공한은 lead 4명 중 "관측자/의례 사수"로 읽혀야 한다. 단린의 ivory priest, 이빨바람의 warm leather raider, 묵향의 bone-jade grave caster와 겹치지 않게 smoky violet cowl, pale ritual sash, cyan prism bow를 전면 식별점으로 둔다.

## prompt 명세

```prompt
Create one full-body polished JRPG character illustration of Gonghan / Echo Savant, using the attached references with strict priority:

REFERENCE PRIORITY
1. The attached P09 model reference for hero_echo_savant is CANON for identity, outfit slots, silhouette, hair, weapon, shield absence, and color zoning.
2. The attached Dawn Priest full-body illustration is STYLE ONLY. Match its professional 2D illustration quality, elegant face polish, confident hand-painted linework, layered cloth rendering, clean silhouette outline, and refined character-art finish. Do NOT copy Dawn Priest costume, sword, shield, pose, colors, face, or priest motifs.

CORE IDENTITY
- Male echo savant / ritual archer, slender build, calm outside observer.
- Smoky violet short hair, slightly tousled/spiky, not long, not black, not brown.
- Amber eyes with quiet distant focus.
- Light pale skin, slightly cool but alive.
- Outfit: Armor_011 hooded/cowl rogue-ritual set for head, chest, arms, and legs, with Armor_008 pale ritual sash at the waist.
- Weapon: Bow_004 only, a recurve/prism ritual bow with cool cyan accents.
- Shield slot is empty. No shield, no buckler, no staff, no sword, no dagger.

P09 CANON OUTFIT LOCK
- Head: Armor_011_Head cowl/hood frame, smoky violet-grey, angular and quiet.
- Chest: Armor_011_Chest layered rogue-ritual coat, smoky violet-gray body with pale lavender trim.
- Arm: Armor_011_Arm fitted bracers/sleeves, muted violet and dark violet, no bulky plate.
- Waist: Armor_008_Waist pale ritual sash, visibly lighter than the coat, lavender-white with cyan prism accent.
- Leg: Armor_011_Leg dark violet trousers/boots, slim and mobile.
- Weapon: Bow_004, pale lavender/dark violet bow body with cool cyan prism glow.

COLOR ANCHORS
- Hair: main #6F647A, shadow #2A2430, highlight #C8C1D8.
- Cowl/head: main #8C7FA0, shadow #4C405E, cyan accent #8FD4E8.
- Chest: main #8C7FA0, pale trim #C8C1D8, cyan accent #8FD4E8.
- Arms: main #7B6E90, shadow #4C405E, pale trim #C8C1D8.
- Waist sash: main #D6D2E3, secondary #8C7FA0, accent #8FD4E8.
- Legs: main #4C405E, deep shadow #2E2638, trim #8C7FA0.
- Bow: main #4D435A, cyan prism #8FD4E8, pale trim #C8C1D8, very subtle emission #2A5360.

FACE AND EXPRESSION
- Composed observer at rest, attentive but restrained.
- Lips closed, no smile, no anger, no melodrama.
- Eyes should feel like someone listening to a distant echo, focused beyond the viewer.
- Make the face appealing and professionally illustrated, not a literal 3D conversion.

POSE AND COMPOSITION
- Full body, three-quarter front view, both feet visible.
- Calm standing pose with slight low camera presence.
- Bow visible from top tip to bottom tip, held naturally at one side or slightly across the body.
- Left hand empty or lightly balancing the bow. No shield.
- Keep the cowl/hood shape visible, the pale waist sash clearly readable, and the cyan bow accent visible.
- Vertical 2:3 composition, 1024 x 1536 PNG.

STYLE TARGET
- High-end hand-painted 2D game character illustration.
- Same internal house style as the Dawn Priest full-body seed: clean elegant silhouette, expressive but controlled face, painterly cloth folds, crisp readable costume zones, subtle rim light, polished commercial character art.
- Avoid rigid 3D-to-2D tracing. Translate the P09 model into a confident production illustration while preserving canon.

BACKGROUND AND CLEANUP
- Solid #FF00FF magenta background, flat fill only.
- 1-2 px clean dark outline around the whole subject for chroma cleanup.
- No cast shadow, no gradient, no environment.
- No magenta contamination on the subject.

NEGATIVE LOCKS
- No staff, no sword, no dagger, no shield, no quiver, no extra arrows in hand.
- No assassin pure-black redesign, no grim reaper, no necromancer, no skull motifs.
- No large spell circles, no floating runes, no forehead tattoos, no lattice text symbols.
- No copied Dawn Priest costume, no ivory priest robe, no gold shield, no sword.
- No saturated purple blob; keep smoky violet, pale lavender sash, and cyan bow accents separated by value.
```
