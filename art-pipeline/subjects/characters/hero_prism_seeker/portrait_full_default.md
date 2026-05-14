---
slug: hero_prism_seeker--portrait_full_default
kind: character_portrait_full
subject_id: hero_prism_seeker
variant: portrait_full
emotion: default
refs:
- hero_prism_seeker
- hero_dawn_priest:portrait_full_style_seed_best
aspect: '2:3'
output_size: 1024x1536
chroma: '#FF00FF'
status: rendered
---
# 광로 (光路) / Prism Seeker — Portrait Full Default

## 생성 의도

첫 번째 reference는 이 캐릭터의 P09 canon이다. 두 번째 reference `portrait_full_style_seed_best`는 단린 best full-body illustration seed이며 작가감, 선 밀도, 얼굴 polish, painterly rendering, clean silhouette만 맞추는 style-only seed다. 단린의 의상, 무기, 색상, 포즈, 사제 motif는 가져오지 않는다.

## prompt 명세

```prompt
Create one full-body polished JRPG character illustration of 광로 (光路) / Prism Seeker, using the attached references with strict priority:

REFERENCE PRIORITY
1. The attached P09 model reference for hero_prism_seeker is CANON for identity, outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The attached Dawn Priest full-body illustration is STYLE ONLY. Match its professional 2D illustration quality, elegant face polish, confident hand-painted linework, layered cloth/armor rendering, clean silhouette outline, and refined character-art finish. Do NOT copy Dawn Priest costume, sword/shield setup, pose, colors, face, or priest motifs.

The illustration is a high-resolution painterly production version of the exact P09 character. Preserve canon, but do not make a rigid 3D-to-2D conversion.

DO NOT add garments, props, ornaments, scars, tattoos, pouches, capes, hoods, religious symbols, tribal decorations, or extra weapons that are not visible in the REF. Do not replace the P09 outfit with a genre-standard class costume.

=== CHARACTER IDENTITY CONTRACT ===
- Character: 광로 (光路) / Prism Seeker
- Pipeline subject_id: hero_prism_seeker
- Faction / job lane: lattice_order / prism_seeker
- Uniform group: lattice_order_prism_seeker
- Silhouette family: p09_h0_c3_a11_w8_l3_wp13_sh0
- Intent: Pindoc wiki의 젊은 남성 Ranger 기준. travel scout gear에 lattice sash와 Bow_004 prism signal을 더한 외부 호기심 정찰병.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 1 / HairStyleId 12 / HairColorId 1
- SkinId 1 / EyeColorId 4 / FacialHairId 0 / BustSizeId 2
- Outfit A3/W13/S0 / Head 0 / Chest 3 / Arm 11 / Waist 8 / Leg 3

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when the capture lighting obscures the canonical color. The REF shape plus these color anchors is the character identity.
- Hair: #2B211D
- Chest / top color zone: #D6D2E3
- Waist / lower color zone: #4C405E
- Leg / lower color zone: #5C6370
- Weapon: #4D435A
- Shield: none in P09 preset

=== VARIANT: portrait_full / default emotion ===
Expression: neutral default state that fits the character identity above. Keep the face readable and distinct from same-faction or same-job characters. Make the face appealing and professionally illustrated, not a literal 3D model face.

Pose and composition:
- Full body, single subject, vertical 2:3.
- Three-quarter front view, slight low camera angle, both feet visible.
- Keep the weapon and shield presence exactly as the REF shows.
- Shield slot is empty: do not add a shield, buckler, mirror shield, or defensive prop.
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
