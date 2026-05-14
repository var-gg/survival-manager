---
slug: npc_grey_fang--battle_stance_sheet
kind: battle_stance_sheet
subject_id: npc_grey_fang
variant: battle_stance_sheet
refs:
- npc_grey_fang
- npc_grey_fang:portrait_full
- npc_grey_fang:portrait_face_default
- hero_dawn_priest:portrait_full_style_seed_best
aspect: '1:1.49'
output_size: 1568x2336
chroma: '#FF00FF'
status: rendered
stances:
- idle
- attack
- guard
- cast
---
# 회조 (灰爪) / Grey Fang — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 회조 (灰爪) / Grey Fang.

=== REFERENCE PRIORITY ===
1. The P09 anchor for npc_grey_fang is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The npc_grey_fang:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 회조 (灰爪) / Grey Fang
- Pipeline subject_id: npc_grey_fang
- Faction / job lane: irisol / grey_fang_duelist
- Uniform group: irisol_grey_fang_duelist
- Silhouette family: p09_h0_c4_a11_w4_l3_wp1_sh0
- Intent: 피형제 leather baseline에 frost hair와 rogue arm/travel boots를 더한 분리파 결투가.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 1 / HairStyleId 2 / HairColorId 5
- SkinId 1 / EyeColorId 1 / FacialHairId 5 / BustSizeId 2
- Outfit A4/W1/S0 / Head 0 / Chest 4 / Arm 11 / Waist 4 / Leg 3
- WeaponId 1 (short sword / hunting blade) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #B0B3B0
- Head gear: read from REF
- Chest / upper outfit: #8B7A68
- Arm / sleeve: #6A625F
- Waist / sash: #5C5248
- Leg / lower outfit: #6F665C
- Weapon: #5B5A58
- Shield: none in P09 preset


=== WEAPON / SHIELD STANCE RULES ===
- WeaponId 1: short sword / hunting blade.
- ShieldId 0: no shield, no buckler, no mirror shield, no defensive prop in any cell.
- attack: sword lunge or diagonal strike, clear blade silhouette, no bow or staff.
- guard: sword parry or evasive low guard; off-hand empty, no shield or buckler.
- cast: class technique pose with weapon held low or used as focus; restrained effect, not a new weapon.


=== GRID SHEET LAYOUT ===
- Canvas: 1568 x 2336 PNG.
- 2 columns x 2 rows.
- Each cell: exactly 768 x 1152 px.
- Gap between cells: exactly 32 px, solid #FF00FF.
- Outside grid and each cell background: solid #FF00FF.
- Reading order:
  - row 0: idle, attack
  - row 1: guard, cast

=== CELL CONTENT ===
- Full body in every cell: head to feet visible, weapon and shield state readable.
- Same character, same outfit, same hair, same color zones, same weapon, same shield state across all 4 cells.
- Three-quarter front view with dynamic stance differences.
- Same scale and vertical footprint across all cells.
- Clean 1-2 px dark outline around the entire character silhouette for chroma cleanup.

=== STANCE SET ===
- idle: combat-ready rest posture, readable class identity.
- attack: active offensive motion as specified in weapon rules.
- guard: defensive posture as specified in weapon/shield rules.
- cast: class technique or support stance; restrained effect only, no environment.

=== HARD CONSTRAINTS ===
- Single subject per cell.
- No UI, captions, labels, environment, ground shadows, companions, extra weapons, or extra props.
- Do not change gender presentation, hair, face, outfit slots, weapon family, shield state, or palette between cells.
- Do not add capes, halos, animal features, scars, tattoos, jewelry, class symbols, or ornaments not visible in P09.
- No magenta tint on the character. Any effect must avoid #FF00FF/fuchsia and must not bridge cells.
```
