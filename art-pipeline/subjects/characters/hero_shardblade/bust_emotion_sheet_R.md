---
slug: hero_shardblade--bust_emotion_sheet_R
kind: bust_emotion_sheet
subject_id: hero_shardblade
variant: bust_emotion_sheet_R
refs:
- hero_shardblade
- hero_shardblade:portrait_full
- hero_shardblade:portrait_face_default
- hero_dawn_priest:portrait_full_style_seed_best
aspect: 1.36:1
output_size: 3168x2336
chroma: '#FF00FF'
status: rendered
direction: facing_right
emotions:
- default
- smile
- serious
- shock
- anger
- sad
- cry
- quiet
---
# 편검 (片劍) / Shardblade — Bust Emotion Sheet R

## prompt 명세

```prompt
Create a 4-column x 2-row bust emotion sprite sheet for 편검 (片劍) / Shardblade, facing camera-right.

=== REFERENCE PRIORITY ===
1. The P09 anchor for hero_shardblade is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The hero_shardblade:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 편검 (片劍) / Shardblade
- Pipeline subject_id: hero_shardblade
- Faction / job lane: lattice_order / shardblade
- Uniform group: lattice_order_shardblade
- Silhouette family: p09_h0_c4_a5_w4_l3_wp4_sh0
- Intent: 외부 일상복 같은 Armor_004에 실전 bracer와 격자검을 더한 earth/indigo/prism 직공 결투가.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 1 / HairStyleId 3 / HairColorId 8
- SkinId 1 / EyeColorId 4 / FacialHairId 0 / BustSizeId 2
- Outfit A4/W4/S0 / Head 0 / Chest 4 / Arm 5 / Waist 4 / Leg 3
- WeaponId 4 (oath execution sword) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #6F4B88
- Head gear: read from REF
- Chest / upper outfit: #7B614A
- Arm / sleeve: #6F737A
- Waist / sash: #2E3440
- Leg / lower outfit: #5C4A38
- Weapon: #D8D2E3
- Shield: none in P09 preset


=== GRID SHEET LAYOUT ===
- Canvas: 3168 x 2336 PNG.
- 4 columns x 2 rows.
- Each cell: exactly 768 x 1152 px.
- Gap between cells: exactly 32 px, solid #FF00FF.
- Outside grid and each cell background: solid #FF00FF.
- Reading order:
  - row 0: default, smile, serious, shock
  - row 1: anger, sad, cry, quiet

=== CAMERA AND CROP ===
- Bust crop: head, shoulders, upper chest, and the most important collar/upper armor zones.
- Facing camera-right for VN left-side placement.
- Eyes look toward the right side of frame at about 25-30 degrees from frontal axis.
- Head turns about 20-25 degrees toward the right side of frame.
- Body rotates the same direction; the camera-side shoulder is closer.
- Same zoom, same shoulder height, same head height, same facing angle across all 8 cells.
- Clean 1-2 px dark outline around the visible silhouette.

=== EXPRESSION SET ===
- default: neutral readable resting state that fits the role and faction.
- smile: restrained character-appropriate warmth, not a broad comedy grin.
- serious: focused, jaw set, eyes intent.
- shock: eyes widened and breath caught, controlled rather than theatrical.
- anger: controlled anger, narrowed eyes, tension in brow and mouth.
- sad: quiet grief, lowered gaze, no melodrama.
- cry: one restrained tear, dignity preserved.
- quiet: emotionally emptied stillness, distant eyes, almost no mouth movement.

=== HARD CONSTRAINTS ===
- Single subject per cell, all facing camera-right.
- No full weapon display unless a small part is already naturally visible in the bust crop. Never add a new prop.
- Do not change hairstyle, hair color, eye color, outfit colors, gender presentation, facial hair, or head gear between cells.
- Do not add scars, tattoos, extra ornaments, glasses, animal ears, halos, or jewelry unless visible in the P09 ref.
- No magenta tint on the character.
```
