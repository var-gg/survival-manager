---
slug: bastion_penitent--face_emotion_sheet
kind: face_emotion_sheet
subject_id: bastion_penitent
variant: face_emotion_sheet
refs:
- bastion_penitent
- bastion_penitent:portrait_full
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
# 참회벽 / Bastion Penitent — Face Emotion Sheet

## prompt 명세

```prompt
Create a 4-column x 2-row face emotion sprite sheet for 참회벽 / Bastion Penitent.

=== REFERENCE PRIORITY ===
1. The P09 anchor for bastion_penitent is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The bastion_penitent:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. No face-default ref is attached yet because this sheet creates it; use portrait_full for face and costume continuity.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 참회벽 / Bastion Penitent
- Pipeline subject_id: bastion_penitent
- Faction / job lane: solarum / bastion_penitent
- Uniform group: solarum_bastion_penitent
- Silhouette family: p09_h0_c6_a5_w6_l5_wp3_sh3
- Intent: Head를 비운 repaired plate/chain mix와 red hair/gold-blue shield의 참회 방패병.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 1 / HairStyleId 5 / HairColorId 9
- SkinId 1 / EyeColorId 2 / FacialHairId 0 / BustSizeId 2
- Outfit A6/W3/S3 / Head 0 / Chest 6 / Arm 5 / Waist 6 / Leg 5
- WeaponId 3 (regular military sword) / ShieldId 3

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #A33A2E
- Head gear: #D1C7B2
- Chest / upper outfit: #8A8E92
- Arm / sleeve: #7A828C
- Waist / sash: #5C6370
- Leg / lower outfit: #5C6370
- Weapon: #A8A8AC
- Shield: #8A8E92


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
