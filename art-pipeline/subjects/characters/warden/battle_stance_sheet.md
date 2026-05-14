---
slug: warden--battle_stance_sheet
kind: battle_stance_sheet
subject_id: warden
variant: battle_stance_sheet
refs:
- warden
- warden:portrait_full
- warden:portrait_face_default
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
# 철위 (鐵衛) / Iron Warden — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 철위 (鐵衛) / Iron Warden.

=== REFERENCE PRIORITY ===
1. The P09 anchor for warden is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The warden:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 철위 (鐵衛) / Iron Warden
- Pipeline subject_id: warden
- Faction / job lane: solarum / iron_warden
- Uniform group: solarum_iron_warden
- Silhouette family: p09_h6_c6_a6_w5_l6_wp3_sh5
- Intent: Armor_006 full plate에 field waist와 royal blue shield를 둔 veteran warden.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 2 / HairStyleId 5 / HairColorId 5
- SkinId 1 / EyeColorId 2 / FacialHairId 4 / BustSizeId 2
- Outfit A6/W3/S5 / Head 6 / Chest 6 / Arm 6 / Waist 5 / Leg 6
- WeaponId 3 (regular military sword) / ShieldId 5

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #C0C0B8
- Head gear: #9AA0A6
- Chest / upper outfit: #9AA0A6
- Arm / sleeve: #8D949C
- Waist / sash: #4A5160
- Leg / lower outfit: #7A828C
- Weapon: #B0B4B8
- Shield: #4A6FA8


=== WEAPON / SHIELD STANCE RULES ===
- WeaponId 3: regular military sword.
- ShieldId 5: render the shield exactly as P09 shows in every stance where the shield arm is visible.
- attack: sword lunge or diagonal strike, clear blade silhouette, no bow or staff.
- guard: shield-forward defensive stance using the P09 shield, sword held ready.
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
