---
slug: shaman--battle_stance_sheet
kind: battle_stance_sheet
subject_id: shaman
variant: battle_stance_sheet
refs:
- shaman
- shaman:portrait_full
- shaman:portrait_face_default
- hero_dawn_priest:portrait_full_style_seed_best
aspect: '1:1.49'
output_size: 1568x2336
chroma: '#FF00FF'
status: prompted
stances:
- idle
- attack
- guard
- cast
---
# 풍의 (風儀) / Storm Shaman — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 풍의 (風儀) / Storm Shaman.

=== REFERENCE PRIORITY ===
1. The P09 anchor for shaman is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The shaman:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 풍의 (風儀) / Storm Shaman
- Pipeline subject_id: shaman
- Faction / job lane: irisol / storm_shaman
- Uniform group: irisol_storm_shaman
- Silhouette family: p09_h10_c10_a10_w7_l10_wp8_sh0
- Intent: Armor_010 shaman에 robe sash를 더한 red-brown/amber/deep-green 공동체 치유사.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 2 / HairStyleId 10 / HairColorId 1
- SkinId 1 / EyeColorId 1 / FacialHairId 0 / BustSizeId 3
- Outfit A10/W8/S0 / Head 10 / Chest 10 / Arm 10 / Waist 7 / Leg 10
- WeaponId 8 (totem staff) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #2E2119
- Head gear: #8A4F2E
- Chest / upper outfit: #8A4F2E
- Arm / sleeve: #7A4A32
- Waist / sash: #6B4A3A
- Leg / lower outfit: #4F3928
- Weapon: #6A4A35
- Shield: none in P09 preset


=== WEAPON / SHIELD STANCE RULES ===
- WeaponId 8: totem staff.
- ShieldId 0: no shield, no buckler, no mirror shield, no defensive prop in any cell.
- attack: staff thrust or sweeping strike, staff clearly in the correct hand, no sword or bow.
- guard: staff held across the body as a warding bar or barrier focus.
- cast: staff raised or planted with restrained faction-colored glow; avoid oversized spell effects.


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
