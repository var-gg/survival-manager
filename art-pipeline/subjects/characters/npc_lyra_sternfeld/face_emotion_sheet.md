---
slug: npc_lyra_sternfeld--face_emotion_sheet
kind: face_emotion_sheet
subject_id: npc_lyra_sternfeld
variant: face_emotion_sheet
refs:
- npc_lyra_sternfeld
- npc_lyra_sternfeld:portrait_full
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
# 선영 (宣英) / Lyra Sternfeld — Face Emotion Sheet

## prompt 명세

```prompt
Create a 4-column x 2-row face emotion sprite sheet for 선영 (宣英) / Lyra Sternfeld.

=== REFERENCE PRIORITY ===
1. The P09 anchor for npc_lyra_sternfeld is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The npc_lyra_sternfeld:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. No face-default ref is attached yet because this sheet creates it; use portrait_full for face and costume continuity.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 선영 (宣英) / Lyra Sternfeld
- Pipeline subject_id: npc_lyra_sternfeld
- Faction / job lane: solarum / fanatic_priest
- Uniform group: solarum_fanatic_priest
- Silhouette family: p09_h9_c7_a8_w7_l7_wp9_sh0
- Intent: 단린과 같은 priest baseline을 cold ivory/violet ceremonial authority로 뒤튼 광신 사제.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 2 / HairStyleId 7 / HairColorId 3
- SkinId 1 / EyeColorId 3 / FacialHairId 0 / BustSizeId 2
- Outfit A7/W9/S0 / Head 9 / Chest 7 / Arm 8 / Waist 7 / Leg 7
- WeaponId 9 (ceremonial staff) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #D6CBAE
- Head gear: #D8D2C2
- Chest / upper outfit: #D8D2C2
- Arm / sleeve: #C8C0D2
- Waist / sash: #6E6480
- Leg / lower outfit: #5C5248
- Weapon: #D8D2C2
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
