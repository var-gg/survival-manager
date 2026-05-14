---
slug: hero_grave_hexer--battle_stance_sheet
kind: battle_stance_sheet
subject_id: hero_grave_hexer
variant: battle_stance_sheet
refs:
  - hero_grave_hexer
  - hero_grave_hexer:portrait_full
  - hero_grave_hexer:portrait_face_default
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

# 묵향 — Battle Stance Sheet (4 stance 풀바디, v2 — staff swap)

## v2 변경 사유

P09 preset weaponId 2 (Sword) → weaponId 7 (Staff_002) 정정 동기화. V1 4 stance가 sword를 들고 있으므로 staff 기반 stance로 재생성. anchor는 새 rev11 (staff caster), `portrait_full` ref도 V2 (staff) 사용.

## 산출물

`portrait_stance_<id>.png` 4장:
- portrait_stance_idle.png — 평상 자세 (전투 대기, mystic composure)
- portrait_stance_attack.png — 공격 모션 (시간 거리 — staff 끝 thrust + jade ripple)
- portrait_stance_guard.png — 방어 자세 (농담의 종 — staff held across body, jade crystal active)
- portrait_stance_cast.png — 시전 모션 (기억 투사 — staff raised vertically, jade memory wisps)

## 단린/이빨바람과 차이점

묵향은 **방패가 없고**, **무기는 staff** (긴 막대 형태, 길이 ~shoulder-to-floor, 한 끝에 jade green crystal). 단린의 paladin sword/shield 패턴, 이빨바람의 hunter short-sword 패턴과 명확히 다름.

stance 의미가 mystic 어법 + staff motion으로 swap:
- **idle** = staff vertically grounded at right side (캐릭터 옆에 막대 세움)
- **attack** = staff 끝(crystal end)을 적 방향으로 thrust (NOT 휘두름 — 정확한 jab)
- **guard** = "농담의 종" 보호 자세 (staff held diagonally across body, jade crystal mid-tone — 동료 보호)
- **cast** = "기억 투사" 자세 (staff raised vertically, crystal end up, jade emission 강함, 좌수 raised palm)

cast의 jade emission color는 P09 asset의 weapon crystal MainColor(#6DBE7C)에 anchor — 단린(warm gold), 이빨바람(warm amber)과 명확 분리.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 4 cells in the grid below are the SAME character with different combat stances only — same outfit, same hair, same eye color, same skin, same proportions, same identity, same weapon.

Match the REF for the character's identity:
- Hair: medium-length ivory/ash (#D8D4C8) with darker secondary tone (#8E8980).
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family).
- Skin: light pale tone with slight warmth (#E8DDC8 family), soft idealized.
- Outfit: layered grave-keeper attire — ivory shoulder cloth (#B8B0A3) over dark central bodice (#5F5A54), layered grave-gray skirt below, dark trousers (#5F5A54), black heeled shoes. Match REF.
- Hat: brimmed hat (bone-ash beige #B8B0A3) with subtle horn-like points — visible at top of frame in EVERY cell. Match REF exactly.
- Right hand: STAFF (Staff_002 family) — long pole, length ~shoulder-to-floor when butt grounded. Body color #8E8980 muted gray ash with subtle weathered finish (NOT shiny metal, NOT mirrored). One end (the TOP / crystal end) has a small jade green crystal (#6DBE7C) embedded with faint cool emission glow. NOT a sword, NOT a short blade, NOT a dagger.
- Left hand: EMPTY — NO shield, NO secondary weapon, NO secondary staff. The character carries only the single staff in the right hand.
- Face: slender feminine silhouette, no facial hair.

DO NOT:
- Change the character identity, outfit, or weapon between cells.
- Render a sword, short blade, dagger, or any short-handled weapon. The character carries a STAFF in EVERY cell.
- Add a shield, buckler, or any defensive equipment.
- Replace the brimmed hat with a tall pointed wizard hat or hood.
- Use long flowing hair, dark/black hair, brown eyes, or priest white outfit.
- Add wrist scars, talisman/charm props, hanging bells (small crystal embedded in staff is OK, but DO NOT add separate hanging bells), or grave-dust accessories.

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
- Crop: full body, hat to feet visible.
- Three-quarter front view (~20° rotation toward camera-left), matching ref orientation.
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline on the character silhouette in each cell.

CELL ALIGNMENT (CRITICAL):
- All 4 cells perfectly aligned in regular 2×2 grid.
- NO overlap, NO crooked angles.
- Same scale, same character size across all cells.
- HAT visible (not cropped) in every cell.

=== STANCE DESCRIPTORS (per cell) ===

(0,0) IDLE: standing relaxed mystic composure. Right hand: STAFF held vertically at right side, butt of staff grounded near foot, top end (with green crystal) extending up past the shoulder. Left hand: relaxed at side, empty. Both arms slightly bent. Weight evenly on both feet. Slight forward gaze with very subtle dry-amused half-smile at one lip corner. The "한 300년쯤 됐지" baseline posture — composed, NOT combat-ready. ENTIRE STAFF visible top-to-bottom.

(1,0) ATTACK: mid-thrust offensive stance — "시간 거리" signature motion. Right hand: STAFF gripped firmly mid-shaft, the CRYSTAL END (top) thrust forward toward an unseen target diagonally up-right. The staff is held more horizontally now (extended in a precise jab motion, NOT vertical). Left foot stepping forward modestly. Left hand: extended back along the staff for stability, fingers gripping the lower shaft. Body weight forward but contained — measured strike, not wide swing. Eyes locked forward. Subtle jade ripple effect (#6DBE7C) trailing from the crystal end, very faint.

(0,1) GUARD: protective stance — "농담의 종" motion (NO shield). Right hand: STAFF held diagonally across body, gripped mid-shaft, crystal end pointed up-left (toward an unseen threat off-screen). Left hand: gripping the lower shaft of the staff (both hands on the staff, like a polearm guard pose). Jade crystal (#6DBE7C) visibly emanating a soft cool glow — the bell-toll motion just completed. Body lowered into a slight defensive stance — knees bent. Head turned slightly toward the threat. The "동료 디버프 해제" posture — protecting allies through ringing the staff bell, not by tanking damage.

(1,1) CAST: memory projection sense stance — "기억 투사" signature motion. Right hand: STAFF held vertically aloft (raised slightly above head, gripped mid-shaft, crystal end pointed UP toward sky). Jade crystal (#6DBE7C) emanating a strong cool jade glow with cool jade particle wisps rising upward from the crystal (suggesting summoned memories). Left hand: open palm raised at chest height, fingers spread, palm tilted forward — directing the memory projection. Head tilted slightly upward (~10°), eyes half-closed in concentration. Subtle cool jade haze (#6DBE7C to #B0E0BC family) around the palm and staff crystal — the visualization of memories being summoned. The mystic's signature ability made manifest as cool jade illumination from the staff crystal.

DO NOT use warm gold, amber, pink, magenta, or violet for the cast cell's glow. Strictly cool jade green (#6DBE7C family).

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 4 cells.
- HAT visible in every cell.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere.
- Cast cell glow: STRICTLY cool jade green (#6DBE7C). Guard cell crystal glow: same cool jade. NO warm amber, NO holy gold, NO violet/magenta in any glow effect.
- LEFT HAND IS EMPTY in ALL cells (no shield, no secondary weapon). Cast cell's left palm is raised but empty.
- Output: 1568 × 2336 PNG, 2-column × 2-row grid sheet.
```
