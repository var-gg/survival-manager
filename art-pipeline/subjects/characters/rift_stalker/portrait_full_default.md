---
slug: rift_stalker--portrait_full_default
kind: character_portrait_full
subject_id: rift_stalker
variant: portrait_full
emotion: default
refs:
- rift_stalker
- hero_dawn_priest:portrait_full_style_seed_best
aspect: '2:3'
output_size: 1024x1536
chroma: '#FF00FF'
status: rendered
---
# 틈사냥꾼 / Rift Stalker — Portrait Full Default

## 생성 의도

첫 번째 reference는 이 캐릭터의 P09 canon이다. 두 번째 reference `portrait_full_style_seed_best`는 단린 best full-body illustration seed이며 작가감, 선 밀도, 얼굴 polish, painterly rendering, clean silhouette만 맞추는 style-only seed다. 단린의 의상, 무기, 색상, 포즈, 사제 motif는 가져오지 않는다.

## prompt 명세

```prompt
Create one full-body polished JRPG character illustration of 틈사냥꾼 / Rift Stalker, using the attached references with strict priority:

REFERENCE PRIORITY
1. The attached P09 model reference for rift_stalker is CANON for identity, outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The attached Dawn Priest full-body illustration is STYLE ONLY. Match its professional 2D illustration quality, elegant face polish, confident hand-painted linework, layered cloth/armor rendering, clean silhouette outline, and refined character-art finish. Do NOT copy Dawn Priest costume, sword/shield setup, pose, colors, face, or priest motifs.

The illustration is a high-resolution painterly production version of the exact P09 character. Preserve canon, but do not make a rigid 3D-to-2D conversion.

DO NOT add garments, props, ornaments, scars, tattoos, pouches, capes, hoods, religious symbols, tribal decorations, or extra weapons that are not visible in the REF. Do not replace the P09 outfit with a genre-standard class costume.

=== CHARACTER IDENTITY CONTRACT ===
- Character: 틈사냥꾼 / Rift Stalker
- Pipeline subject_id: rift_stalker
- Faction / job lane: irisol / rift_stalker
- Uniform group: irisol_rift_stalker
- Silhouette family: p09_h0_c11_a4_w10_l11_wp1_sh0
- Intent: Head를 비운 Armor_011 rogue에 leather/forest clan mark와 cyan rift accent를 더한 stalker.
- Natural-language identity lock: agile female rogue/stalker with no headgear, short dark hair, cyan eyes, and a small Sword_001 / rift dagger. The weapon is a short blade, not a club, not a mace, not a hammer, not a staff.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 3 / HairStyleId 1 / HairColorId 1
- SkinId 1 / EyeColorId 4 / FacialHairId 0 / BustSizeId 1
- Outfit A11/W1/S0 / Head 0 / Chest 11 / Arm 4 / Waist 10 / Leg 11

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when the capture lighting obscures the canonical color. The REF shape plus these color anchors is the character identity.
- Hair: #2A1F1A
- Chest / top color zone: #5A4A62
- Waist / lower color zone: #5A3A25
- Leg / lower color zone: #3F304D
- Weapon: #5B5A58
- Shield: none in P09 preset

=== VARIANT: portrait_full / default emotion ===
Expression: neutral default state that fits the character identity above. Keep the face readable and distinct from same-faction or same-job characters. Make the face appealing and professionally illustrated, not a literal 3D model face.

Pose and composition:
- Full body, single subject, vertical 2:3.
- Three-quarter front view, slight low camera angle, both feet visible.
- Keep the weapon and shield presence exactly as the REF shows.
- - Shield slot is empty: do not add a shield, buckler, mirror shield, or defensive prop.
- Weapon must remain a small blade / short sword / dagger silhouette. Do not turn it into a blunt weapon, branch, club, hammer, axe, or staff.
- Head-to-feet visible with small margin at top and bottom.

OUTPUT: 1024 x 1536 PNG.

=== HARD CONSTRAINTS ===
- Background: solid #FF00FF magenta, flat fill, no gradient, no cast shadow.
- 1-2 px clean dark outline around the entire subject silhouette for chroma cleanup.
- No magenta tint on the subject.
- No companions, no environment, no UI frame.

=== STYLE TARGET ===
- Same internal house style as the Dawn Priest full-body seed: polished hand-painted JRPG character art, elegant face rendering, crisp costume zone separation, painterly folds and metal highlights, controlled cinematic rim light.
- Color zones must remain readable at thumbnail size; avoid monochrome blobs.
- Keep only this character's P09 weapon and shield state.
```
