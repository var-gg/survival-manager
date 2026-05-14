---
slug: npc_black_vellum--battle_stance_sheet
kind: battle_stance_sheet
subject_id: npc_black_vellum
variant: battle_stance_sheet
refs:
- npc_black_vellum
- npc_black_vellum:portrait_full
- npc_black_vellum:portrait_face_default
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
# 흑지 (黑紙) / Black Vellum — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 흑지 (黑紙) / Black Vellum.

=== REFERENCE PRIORITY ===
1. The P09 anchor for npc_black_vellum is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The npc_black_vellum:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 흑지 (黑紙) / Black Vellum
- Pipeline subject_id: npc_black_vellum
- Faction / job lane: pale_conclave / black_vellum_judicator
- Uniform group: pale_conclave_black_vellum_judicator
- Silhouette family: p09_h9_c9_a11_w9_l8_wp7_sh0
- Intent: Armor_009 robe에 black sleeve와 dark caster boots를 붙인 parchment/ink/emerald 단죄자.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 2 / HairStyleId 9 / HairColorId 1
- SkinId 1 / EyeColorId 1 / FacialHairId 0 / BustSizeId 2
- Outfit A9/W7/S0 / Head 9 / Chest 9 / Arm 11 / Waist 9 / Leg 8
- WeaponId 7 (ritual staff) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #151517
- Head gear: #AFA79A
- Chest / upper outfit: #AFA79A
- Arm / sleeve: #4A4648
- Waist / sash: #3E3A36
- Leg / lower outfit: #56505C
- Weapon: #3B3745
- Shield: none in P09 preset


=== WEAPON / SHIELD STANCE RULES ===
- WeaponId 7: ritual staff.
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
