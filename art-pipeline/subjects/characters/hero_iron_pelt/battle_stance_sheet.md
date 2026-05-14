---
slug: hero_iron_pelt--battle_stance_sheet
kind: battle_stance_sheet
subject_id: hero_iron_pelt
variant: battle_stance_sheet
refs:
- hero_iron_pelt
- hero_iron_pelt:portrait_full
- hero_iron_pelt:portrait_face_default
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
# 철피 (鐵皮) / Iron Pelt — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 철피 (鐵皮) / Iron Pelt.

=== REFERENCE PRIORITY ===
1. The P09 anchor for hero_iron_pelt is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The hero_iron_pelt:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 철피 (鐵皮) / Iron Pelt
- Pipeline subject_id: hero_iron_pelt
- Faction / job lane: irisol / iron_pelt_hardliner
- Uniform group: irisol_iron_pelt_hardliner
- Silhouette family: p09_h0_c6_a6_w4_l4_wp5_sh3
- Intent: 솔라룸 plate 파편을 이리솔 leather에 강제로 섞은 dark iron/rust/moss 강경 분리파 vanguard.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 1 / HairStyleId 2 / HairColorId 1
- SkinId 1 / EyeColorId 3 / FacialHairId 5 / BustSizeId 2
- Outfit A6/W5/S3 / Head 0 / Chest 6 / Arm 6 / Waist 4 / Leg 4
- WeaponId 5 (formal duelist sword) / ShieldId 3

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #1F1713
- Head gear: read from REF
- Chest / upper outfit: #5E6266
- Arm / sleeve: #5A3A25
- Waist / sash: #4A2E22
- Leg / lower outfit: #5A3A25
- Weapon: #4A4642
- Shield: #5E6266


=== WEAPON / SHIELD STANCE RULES ===
- WeaponId 5: formal duelist sword.
- ShieldId 3: render the shield exactly as P09 shows in every stance where the shield arm is visible.
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
