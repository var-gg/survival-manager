---
slug: hero_grave_hexer--face_combat_state_sheet
kind: face_combat_state_sheet
subject_id: hero_grave_hexer
variant: face_combat_state_sheet
refs:
  - hero_grave_hexer
  - hero_grave_hexer:portrait_full
  - hero_grave_hexer:portrait_face_default
aspect: "1.51:1"
output_size: "2368x1568"
chroma: "#FF00FF"
states:
  - wounded
  - stunned
  - feared
  - charmed
  - pained
  - downed
status: draft
---

# 묵향 — Face Combat State Sheet (6 game state frontal)

## 산출물

배틀 시스템에서 face icon swap에 사용. 1번 호출로 6 state 동시 생성.

## 단린/이빨바람과 차이점

mystic restraint baseline — 통증/공포/CC 상태에서도 dry humor의 잔재가 살짝 남는 캐릭터. wounded는 "이 정도는 괜찮네… 200년 전에 더 심한 거 있었어" 어조의 여유, stunned는 시간 거리감 흩어짐(그녀의 고유 무기인 시간 척도가 일시적으로 깨짐), feared는 가장 매력적 상태 — 시간 거리감을 가진 자가 처음으로 두려움을 느끼는 자리, charmed는 dry humor가 hypnotic blank smile로 대체.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 6 cells in the grid below are the SAME character — same outfit collar, same hair, same eye color, same skin, same head proportions, same identity.

Match the REF for the character's identity:
- Hair: medium-length ivory/ash (#D8D4C8) with darker secondary tone (#8E8980). NOT brown, NOT black.
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family).
- Skin: light pale tone with slight warmth (#E8DDC8 family), soft idealized.
- Outfit collar visible: ivory shoulder cloth (#B8B0A3) over dark bodice (#5F5A54).
- Hat: brimmed hat (bone-ash beige #B8B0A3) visible at top of frame in EVERY cell.
- Skin tone, eye spacing, nose shape, jaw shape, lip shape: identical across ALL 6 cells.

DO NOT:
- Change the character identity between cells.
- Use long flowing hair, dark/black hair, or replace the brimmed hat with a pointed wizard hat.
- Add wrist scars, talisman/charm props, or grave-dust accessories.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 3-column × 2-row sprite sheet of 6 separate face close-up portraits.

CELL SIZE: each cell exactly 768 × 768 px (square close-up).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells.
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (col, row — 0-based, top-left to bottom-right):
- (0,0): state = wounded
- (1,0): state = stunned
- (2,0): state = feared
- (0,1): state = charmed
- (1,1): state = pained
- (2,1): state = downed

CELL CONTENT — face close-up (per cell):
- Crop: face only — top of head + hat brim visible at top edge, neck/collar at bottom edge, ears within frame, eyes positioned on upper-third line.
- Frontal view with subtle 5-10° rotation.
- HAT BRIM fully visible at top of cell.
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline along the character silhouette in each cell.

CELL ALIGNMENT (CRITICAL):
- All 6 cells perfectly aligned in regular 3×2 grid.
- NO overlap, NO crooked angles.
- Same scale, same head height position across all cells.

=== STATE DESCRIPTORS (per cell — these are GAME STATES, not narrative emotions) ===

(0,0) WOUNDED: HP critical state. A small streak of blood traced from the temple or one cheek; subtle blood mark at lip corner. Hair partially disheveled — a few ivory strands fall across the brow. Eyes narrowed in collected weariness, NOT teary. A thin sheen of sweat on the forehead. Lips slightly parted with breath escape. The faintest dry-humor residue at one lip corner — "이 정도는 괜찮네" baseline. Mystic restraint preserved despite physical limit.

(1,0) STUNNED: CC stun state. Eyes have lost focus — pupils slightly dilated and gaze drifting off-center, NOT looking at camera. Lips parted slightly, jaw slack, expression vacant. Head tilted softly to one side (~10°). Her time-distance composure has disrupted — without it she looks unusually present, jarringly here-and-now. NOT exaggerated cartoon stun.

(2,0) FEARED: CC fear state. The most narratively powerful state for this character — TERROR breaking the time-distance baseline. Eyes wide (whites clearly visible above and below iris), pupils smaller, brow lifted in a sharp peak. A bead of cold sweat on the temple. Lips trembling slightly, parted with caught breath. The mystic who measures threats in centuries suddenly catching one she cannot calibrate. The baseline composure has cracked — and that crack is the threat's weight.

(0,1) CHARMED: CC charm state. Hypnotized blank smile replacing the dry-humor baseline — lip corners lifted softly but eyes ABSENT (gaze unfocused, looking through the viewer). Pupils slightly enlarged with a faint magical hint (a soft warm violet glow at the iris edge — stay well below #C040A0, NOT magenta). Head relaxed, slight tilt forward. Mystic identity temporarily suspended. The dry humor that was her armor is gone, replaced with empty pleasantness.

(1,1) PAINED: damage-over-time state (burn/poison/bleed). Eyes squeezed shut or sharply narrowed, deep crease between brows. Lips drawn tight, jaw clenched. A bead of sweat near the temple. Mystic discipline absorbing pain — stoic, NOT screaming. Head slightly bowed forward. The detached endurance of someone who has seen worse, longer.

(2,1) DOWNED: HP=0 state. Eyes closed or half-closed, eyelashes lowered. Lips parted slightly. Head tilted to one side, ivory hair falling over. A small blood trace on the brow or cheek (more visible than wounded cell). Mystic's last expression — peaceful unconsciousness with the dry-humor lip corner residue gone too. The "downed but recoverable" framing — alive but limp.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 6 cells.
- HAT visible in every cell.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere — Charmed cell's magical iris hint stays in warm violet (#7050A0 family or below #C040A0).
- Output: 2368 × 1568 PNG, 3-column × 2-row grid sheet.
```
