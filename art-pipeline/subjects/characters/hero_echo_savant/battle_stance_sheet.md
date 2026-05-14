---
slug: hero_echo_savant--battle_stance_sheet
kind: battle_stance_sheet
subject_id: hero_echo_savant
variant: battle_stance_sheet
refs:
  - hero_echo_savant
  - hero_echo_savant:portrait_full
  - hero_echo_savant:portrait_face_default
aspect: "1:1.49"
output_size: "1568x2336"
chroma: "#FF00FF"
stances:
  - idle
  - attack
  - guard
  - cast
status: draft
---

# 공한 — Battle Stance Sheet (4 stance 풀바디)

## 산출물

`portrait_stance_<id>.png` 4장:
- portrait_stance_idle.png — 평상 자세 (관측자 attentive composure)
- portrait_stance_attack.png — 공격 모션 (매듭의 화살 — bow drawn back, arrow ritual knot 발현)
- portrait_stance_guard.png — 방어 자세 (휴면의 보호 — bow held across body, cyan ward 발현)
- portrait_stance_cast.png — 시전 모션 (외부 어휘 — bow raised, cyan prism light 발산)

## 단린/이빨바람/묵향과 차이점

공한은 **방패가 없고**, **무기는 활(Bow_004)**. 단린 paladin sword/shield, 이빨바람 hunter sword, 묵향 mystic staff와 명확히 분리.

stance 의미가 ranger archer 어법으로 swap:
- **idle** = bow held vertically at right side, 활시위 풀린 채로
- **attack** = bow drawn back (활시위 당김), arrow nocked + cyan knot 발현
- **guard** = bow held diagonally across body (활을 옆으로 들고), 휴면 의례 cyan ward 동료 보호
- **cast** = bow raised (활을 위로), cyan prism light 발산 ("외부 어휘" 의례)

**cast emission color는 cool cyan (#8FD4E8)** — 묵향(jade green)과도 다른 cool tone. 단린(warm gold), 이빨바람(warm amber)과도 명확 분리.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 4 cells in the grid below are the SAME character with different combat stances only — same outfit, same hair, same eye color, same skin, same proportions, same identity, same weapon.

Match the REF for the character's identity:
- Hair: SHORT spiky smoky violet/lavender (#92879F). NOT long, NOT brown, NOT black.
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family).
- Skin: light pale tone with very slight cool undertone, masculine, soft idealized.
- Outfit: layered ritual armor — smoky lavender (#8C7FA0) chest/arm armor with subtle weathered tone, dark muted violet (#5A4C6B) trousers, cream/sage scarf (#D8D4C8) draped from one shoulder, dark boots. Match REF.
- Right hand: BOW (Bow_004 family) — recurve bow body in pale lavender (#C8C1D8 with cool undertone) with prism crystal points at limb tips in cool cyan (#8FD4E8) with faint emission glow. NOT a longbow with wood texture, NOT a metal compound. The ritual archer's bow.
- Left hand: EMPTY — NO shield, NO secondary weapon. The character carries only the bow in the right hand. (Or holding arrow / drawing bowstring in attack/cast cells per descriptors.)
- Face: slender masculine silhouette, no facial hair.

DO NOT:
- Change the character identity, outfit, or weapon between cells.
- Render a sword, dagger, staff, or any non-bow weapon. The character carries a BOW.
- Add a shield, buckler, or any defensive equipment.
- Replace the cream scarf with a hood, full cape, or different drape.
- Use long flowing hair, brown/black hair, or saturated unnatural hair color.
- Add forehead lattice tattoos, hair-tip crystal attachments, visible arrow quiver, knotted ritual arrows held separately, or other lore-only props.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 2-column × 2-row sprite sheet of 4 separate FULL BODY portraits of the SAME character.

CELL SIZE: each cell exactly 768 × 1152 px (vertical full body).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells.
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (col, row — 0-based, top-left to bottom-right):
- (0,0): stance = idle
- (1,0): stance = attack
- (0,1): stance = guard
- (1,1): stance = cast

CELL CONTENT — full body portrait (per cell):
- Crop: full body, head to feet visible.
- Three-quarter front view (~20° rotation toward camera-left), matching ref orientation.
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline on the character silhouette in each cell.

CELL ALIGNMENT (CRITICAL):
- All 4 cells perfectly aligned in regular 2×2 grid.
- NO overlap, NO crooked angles.
- Same scale, same character size across all cells.

=== STANCE DESCRIPTORS (per cell) ===

(0,0) IDLE: standing relaxed observer composure. Right hand: BOW held vertically at right side, lower limb grounded near foot, upper limb extending up past shoulder, bowstring slack (not drawn). Left hand: relaxed at side, empty. Both arms slightly bent. Weight evenly on both feet. Slight forward gaze with very subtle distant attentiveness. The "1800년 침묵에서 깨어난 자" baseline posture — composed observer, NOT combat-ready, NOT mid-action. ENTIRE BOW visible top-to-bottom.

(1,0) ATTACK: mid-draw offensive stance — "매듭의 화살" signature motion. Right hand: BOW gripped, raised to shoulder level, BOWSTRING DRAWN BACK to the cheek with arrow nocked. Left hand: gripping the bow body, supporting the draw. Both arms bent into proper archer's draw stance. Body weight slightly forward but stable, archer's stance. Eyes locked on imminent target with calm focus. The arrow's nocked tip has a faint cool cyan glimmer (#8FD4E8) — the "ritual knot" being formed. NOT mid-loose — the moment of holding the draw before release.

(0,1) GUARD: protective ritual stance — "휴면의 보호" motion (NO shield). Right hand: BOW held diagonally across body horizontally at chest level, bowstring slack, the bow body acting as a barrier in front of the character. Left hand: extended forward/sideways, palm open in a "stay back" gesture toward an unseen ally. Body lowered into a slight defensive stance — knees bent. Cool cyan light (#8FD4E8) emanating softly from the bow's prism crystals (visible at the bow's limb tips), suggesting the ritual ward being projected. The "동료 휴면 보호" posture.

(1,1) CAST: ritual ability stance — "외부 어휘" signature motion. Right hand: BOW held VERTICALLY ALOFT (raised slightly above head, gripped mid-shaft, both bow tips pointed up and down vertical). Left hand: open palm raised at chest height, fingers spread, palm tilted forward — directing the ritual emanation. Strong cool cyan light (#8FD4E8) emanating from the bow's crystal points and from around the open palm, with faint cool cyan particle wisps rising upward (the visualization of "external lexicon" being projected outward). Head tilted slightly upward (~10°), eyes half-closed in concentration. Subtle cool cyan haze around the bow and palm. The ritual archer's signature ability made manifest as cool cyan illumination.

DO NOT use warm gold, amber, pink, magenta, or violet for the cast cell's glow. Strictly cool cyan (#8FD4E8 family). Also DO NOT use jade green (that is 묵향's color) — this is CYAN, distinctly bluer than green.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 4 cells.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere.
- Cast cell glow: STRICTLY cool cyan (#8FD4E8). Guard cell crystal glow: same cool cyan. Attack cell arrow knot: faint cool cyan. NO warm amber, NO holy gold, NO violet/magenta, NO jade green in any glow effect.
- LEFT HAND IS EMPTY in idle. In attack: gripping bow body. In guard: extended forward palm. In cast: open palm raised.
- BOW always held in right hand. NO sword, dagger, or staff.
- Output: 1568 × 2336 PNG, 2-column × 2-row grid sheet.
```
