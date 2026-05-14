---
slug: rift_stalker--face_emotion_sheet
kind: face_emotion_sheet
subject_id: rift_stalker
variant: face_emotion_sheet
refs:
- rift_stalker
- rift_stalker:portrait_full
- hero_dawn_priest:portrait_full_style_seed_best
aspect: '2:1'
output_size: 3168x1568
chroma: '#FF00FF'
status: rendered
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
# 틈사냥꾼 / Rift Stalker — Face Emotion Sheet

## prompt 명세

```prompt
Create a 4-column x 2-row face emotion sprite sheet for 틈사냥꾼 / Rift Stalker.

=== REFERENCE PRIORITY ===
1. The P09 anchor for rift_stalker is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The rift_stalker:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. No face-default ref is attached yet because this sheet creates it; use portrait_full for face and costume continuity.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 틈사냥꾼 / Rift Stalker
- Pipeline subject_id: rift_stalker
- Faction / job lane: irisol / rift_stalker
- Uniform group: irisol_rift_stalker
- Silhouette family: p09_h0_c11_a4_w10_l11_wp1_sh0
- Intent: Head를 비운 Armor_011 rogue에 leather/forest clan mark와 cyan rift accent를 더한 stalker.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 3 / HairStyleId 1 / HairColorId 1
- SkinId 1 / EyeColorId 4 / FacialHairId 0 / BustSizeId 1
- Outfit A11/W1/S0 / Head 0 / Chest 11 / Arm 4 / Waist 10 / Leg 11
- WeaponId 1 (short sword / hunting blade) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #2A1F1A
- Head gear: #D1C7B2
- Chest / upper outfit: #5A4A62
- Arm / sleeve: #7A4A32
- Waist / sash: #5A3A25
- Leg / lower outfit: #3F304D
- Weapon: #5B5A58
- Shield: none in P09 preset


=== GRID SHEET LAYOUT ===
- Canvas: 3168 x 1568 PNG.
- 4 columns x 2 rows.
- Each cell: exactly 768 x 768 px.
- Gap between cells: exactly 32 px, solid #FF00FF.
- Outside grid and each cell background: solid #FF00FF.
- Reading order:
  - row 0: default, smile, serious, shock
  - row 1: anger, sad, cry, quiet

=== CELL CONTENT ===
- Face close-up only: top of hair visible, ears inside frame, neck/collar visible at bottom.
- Same character in all 8 cells: same hair, eye color, skin, face proportions, collar, and outfit colors.
- Frontal view with a subtle 5-10 degree turn only.
- Same zoom, same head height, same lighting, same line thickness across all cells.
- Clean 1-2 px dark outline around the visible head / hair / collar silhouette.

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
- Single subject per cell.
- No weapon, hands, UI, captions, labels, environment, shadows, or props.
- Do not change hairstyle, hair color, eye color, collar color, gender presentation, facial hair, or head gear between cells.
- Do not add scars, tattoos, extra ornaments, glasses, animal ears, halos, or jewelry unless visible in the P09 ref.
- No magenta tint on the character.
```
