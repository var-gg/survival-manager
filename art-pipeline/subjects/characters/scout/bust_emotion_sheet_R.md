---
slug: scout--bust_emotion_sheet_R
kind: bust_emotion_sheet
subject_id: scout
variant: bust_emotion_sheet_R
refs:
- scout
- scout:portrait_full
- scout:portrait_face_default
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
# 숲살이 / Trail Scout — Bust Emotion Sheet R

## prompt 명세

```prompt
Create a 4-column x 2-row bust emotion sprite sheet for 숲살이 / Trail Scout, facing camera-right.

=== REFERENCE PRIORITY ===
1. The P09 anchor for scout is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The scout:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 숲살이 / Trail Scout
- Pipeline subject_id: scout
- Faction / job lane: irisol / trail_scout
- Uniform group: irisol_trail_scout
- Silhouette family: p09_h10_c10_a3_w10_l4_wp11_sh0
- Intent: Armor_010 forest scout에 travel sleeve/leather leg를 섞은 green/orange 혼혈 정찰병.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 1 / HairStyleId 12 / HairColorId 4
- SkinId 1 / EyeColorId 1 / FacialHairId 0 / BustSizeId 2
- Outfit A10/W11/S0 / Head 10 / Chest 10 / Arm 3 / Waist 10 / Leg 4
- WeaponId 11 (forest bow) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #4F6B35
- Head gear: #6F8A3A
- Chest / upper outfit: #6F8A3A
- Arm / sleeve: #A88A5F
- Waist / sash: #5A3A25
- Leg / lower outfit: #6B472D
- Weapon: #73543A
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
