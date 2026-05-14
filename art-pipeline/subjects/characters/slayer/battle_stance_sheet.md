---
slug: slayer--battle_stance_sheet
kind: battle_stance_sheet
subject_id: slayer
variant: battle_stance_sheet
refs:
- slayer
- slayer:portrait_full
- slayer:portrait_face_default
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
# 서검 (誓劍) / Oath Slayer — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 서검 (誓劍) / Oath Slayer.

=== REFERENCE PRIORITY ===
1. The P09 anchor for slayer is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The slayer:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 서검 (誓劍) / Oath Slayer
- Pipeline subject_id: slayer
- Faction / job lane: solarum / oath_slayer
- Uniform group: solarum_oath_slayer
- Silhouette family: p09_h0_c5_a12_w5_l5_wp4_sh0
- Intent: Head를 비워 black hair를 보이고 Armor_005 chain에 Armor_012 arm을 붙인 executor duelist.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 2 / HairStyleId 6 / HairColorId 1
- SkinId 1 / EyeColorId 2 / FacialHairId 0 / BustSizeId 1
- Outfit A5/W4/S0 / Head 0 / Chest 5 / Arm 12 / Waist 5 / Leg 5
- WeaponId 4 (oath execution sword) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #1A1718
- Head gear: #D1C7B2
- Chest / upper outfit: #8A8E92
- Arm / sleeve: #5F6570
- Waist / sash: #3E4652
- Leg / lower outfit: #5C6370
- Weapon: #B8B8BC
- Shield: none in P09 preset


=== WEAPON / SHIELD STANCE RULES ===
- WeaponId 4: oath execution sword.
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
