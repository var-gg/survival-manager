---
slug: npc_aldric--bust_emotion_sheet_R
kind: bust_emotion_sheet
subject_id: npc_aldric
variant: bust_emotion_sheet_R
refs:
- npc_aldric
- npc_aldric:portrait_full
- npc_aldric:portrait_face_default
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
# 단현 스턴홀트 (丹玄) / Aldric Sternfeld — Bust Emotion Sheet R

## prompt 명세

```prompt
Create a 4-column x 2-row bust emotion sprite sheet for 단현 스턴홀트 (丹玄) / Aldric Sternfeld, facing camera-right.

=== REFERENCE PRIORITY ===
1. The P09 anchor for npc_aldric is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The npc_aldric:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 단현 스턴홀트 (丹玄) / Aldric Sternfeld
- Pipeline subject_id: npc_aldric
- Faction / job lane: solarum / historical_scholar
- Uniform group: solarum_historical_scholar
- Silhouette family: p09_h0_c3_a3_w3_l3_wp0_sh0
- Intent: Male_Armor_003 scholar robe, ivory tied hair, and dignified facial hair for the historical dead scholar with no combat weapon.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 1 / HairStyleId 8 / HairColorId 5
- SkinId 1 / EyeColorId 2 / FacialHairId 6 / BustSizeId 2
- Outfit A3/W0/S0 / Head 0 / Chest 3 / Arm 3 / Waist 3 / Leg 3
- WeaponId 0 (P09 weapon from REF) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #D8D4C8
- Head gear: read from REF
- Chest / upper outfit: #5C5A58
- Arm / sleeve: #4A4648
- Waist / sash: #2A2A2E
- Leg / lower outfit: #3F3B36
- Weapon: read from REF
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
