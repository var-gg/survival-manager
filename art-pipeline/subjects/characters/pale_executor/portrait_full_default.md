---
slug: pale_executor--portrait_full_default
kind: character_portrait_full
subject_id: pale_executor
variant: portrait_full
emotion: default
refs:
- pale_executor
- hero_dawn_priest:portrait_full_style_seed_best
aspect: '2:3'
output_size: 1024x1536
chroma: '#FF00FF'
status: rendered
---
# 백집행 (白執行) / Pale Executor — Portrait Full Default

## 생성 의도

첫 번째 reference는 이 캐릭터의 P09 canon이다. 두 번째 reference `portrait_full_style_seed_best`는 단린 best full-body illustration seed이며 작가감, 선 밀도, 얼굴 polish, painterly rendering, clean silhouette만 맞추는 style-only seed다. 단린의 의상, 무기, 색상, 포즈, 사제 motif는 가져오지 않는다.

## prompt 명세

```prompt
Create one full-body polished JRPG character illustration of 백집행 (白執行) / Pale Executor, using the attached references with strict priority:

REFERENCE PRIORITY
1. The attached P09 model reference for pale_executor is CANON for identity, outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The attached Dawn Priest full-body illustration is STYLE ONLY. Match its professional 2D illustration quality, elegant face polish, confident hand-painted linework, layered cloth/armor rendering, clean silhouette outline, and refined character-art finish. Do NOT copy Dawn Priest costume, sword/shield setup, pose, colors, face, or priest motifs.

The illustration is a high-resolution painterly production version of the exact P09 character. Preserve canon, but do not make a rigid 3D-to-2D conversion.

DO NOT add garments, props, ornaments, scars, tattoos, pouches, capes, hoods, religious symbols, tribal decorations, or extra weapons that are not visible in the REF. Do not replace the P09 outfit with a genre-standard class costume.

=== CHARACTER IDENTITY CONTRACT ===
- Character: 백집행 (白執行) / Pale Executor
- Pipeline subject_id: pale_executor
- Faction / job lane: pale_conclave / pale_executor
- Uniform group: pale_conclave_pale_executor
- Silhouette family: p09_h12_c12_a9_w12_l11_wp12_sh0
- Intent: Armor_012 formal executor에 robe arm/exile leg와 teal bow glow를 더한 망명 처형자.
- Natural-language identity lock: male exile executor archer with Bow_003 only. The weapon is a bow / recurve bow silhouette, not a scythe, not a polearm, not a spear, not an axe, not a staff, and not a sword.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 3 / HairStyleId 13 / HairColorId 5
- SkinId 1 / EyeColorId 4 / FacialHairId 3 / BustSizeId 2
- Outfit A12/W12/S0 / Head 12 / Chest 12 / Arm 9 / Waist 12 / Leg 11

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when the capture lighting obscures the canonical color. The REF shape plus these color anchors is the character identity.
- Hair: #D8D4C8
- Chest / top color zone: #B8B0C8
- Waist / lower color zone: #4A405A
- Leg / lower color zone: #3B3745
- Weapon: #3B3745
- Shield: none in P09 preset

=== VARIANT: portrait_full / default emotion ===
Expression: neutral default state that fits the character identity above. Keep the face readable and distinct from same-faction or same-job characters. Make the face appealing and professionally illustrated, not a literal 3D model face.

Pose and composition:
- Full body, single subject, vertical 2:3.
- Three-quarter front view, slight low camera angle, both feet visible.
- Keep the weapon and shield presence exactly as the REF shows.
- - Shield slot is empty: do not add a shield, buckler, mirror shield, or defensive prop.
- Weapon must read as a bow, with bow string and curved limbs. Do not place a scythe, polearm, spear, axe, or staff in either hand.
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
