---
slug: hero_echo_savant--bust_emotion_sheet_R
kind: bust_emotion_sheet
subject_id: hero_echo_savant
variant: bust_emotion_sheet_R
direction: facing_right
refs:
  - hero_echo_savant
  - hero_echo_savant:portrait_full
  - hero_echo_savant:portrait_face_default
aspect: "1.36:1"
output_size: "3168x2336"
chroma: "#FF00FF"
emotions:
  - default
  - smile
  - serious
  - shock
  - anger
  - sad
  - cry
  - quiet
status: draft
---

# 공한 — Bust Emotion Sheet (facing right, 8 emotion 흉상)

## 산출물 (VN 화면 왼쪽 배치용)

`portrait_bust_<emotion>_R.png` 8장. facing camera-right — VN dialogue에서 화면 왼쪽 위치 캐릭터.

bust_L은 별도 prompt 호출 없이 R sheet의 split 결과를 코드 좌우반전. 공한의 lore-only 비대칭 디테일(이마 격자 문양, 화살통)은 P09 미표현이며 일러에 포함하지 않으므로 좌우반전 안전.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 8 cells in the grid below are the SAME character with different facial expressions only — same outfit, same hair, same eye color, same skin, same proportions, same identity, same facing direction.

Match the REF for the character's identity:
- Hair: SHORT spiky smoky violet/lavender (#92879F main, #C8C1D8 highlights). NOT long, NOT brown, NOT black. Hand-drawn strand definition.
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family).
- Skin: light pale tone with very slight cool undertone (#E8DDC8 family), masculine.
- Outfit (visible at chest/shoulders): smoky lavender ritual armor (#8C7FA0 with subtle weathered tone) with cream/sage scarf (#D8D4C8) draped from one shoulder. Subtle cool cyan trim accents at armor seams. Match REF.
- Face: slender masculine silhouette, no facial hair.
- Skin tone, eye spacing, nose shape, jaw shape, lip shape, hair shape: identical across ALL 8 cells.

DO NOT:
- Change the character identity between cells.
- Use long hair, brown/black hair, or replace short spiky cut.
- Add facial hair, forehead lattice tattoos, hair-tip crystal attachments.
- Change the facing direction between cells.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 4-column × 2-row sprite sheet of 8 separate bust portraits of the SAME character.

CELL SIZE: each cell exactly 768 × 1152 px (vertical bust portrait).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells.
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (col, row — 0-based, top-left to bottom-right):
- (0,0): emotion = default
- (1,0): emotion = smile
- (2,0): emotion = serious
- (3,0): emotion = shock
- (0,1): emotion = anger
- (1,1): emotion = sad
- (2,1): emotion = cry
- (3,1): emotion = quiet

CELL CONTENT — bust portrait (per cell):
- Crop: head + shoulders + upper chest (down to collarbone area). Hair fully framed top.
- **FACING DIRECTION IS DEFINED BY THIS SPEC — REFERENCES ARE FOR IDENTITY ONLY.** The attached references are for hair color, eye color, outfit colors, facial features only. **DO NOT copy the references' facing direction.** Render the facing direction strictly per the angles below.

GAZE ANGLE (precise — applies to ALL 8 cells):
- Eyes look toward the RIGHT side of the frame at approximately 25-30° from frontal axis. The gaze is clearly off-center to the RIGHT.
- Pupils positioned RIGHT of center within each eye.
- The left side of each eye (toward viewer) shows slightly more sclera than the right side.

HEAD ROTATION (precise):
- Head turned approximately 20-25° toward the RIGHT.
- Chin sweeps slightly toward the LEFT side of the frame; back of the head sweeps toward RIGHT.
- RIGHT cheek (camera-side closer) is more visible to viewer than left cheek.
- Short spiky hair tip visible from camera-side angle.

BODY / SHOULDER ROTATION (precise):
- Body rotated approximately 20-25° so the RIGHT shoulder is closer to the camera (forward in 3/4 view).
- The left shoulder is angled away from camera.
- Cream/sage scarf drape asymmetry follows the rotation — scarf's main drape visible on the camera-side shoulder.

Mental anchor: "VN dialogue scene, character on LEFT side of screen, looking toward conversation partner on RIGHT."
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline on the character silhouette in each cell.

CELL ALIGNMENT (CRITICAL):
- All 8 cells perfectly aligned in regular 4×2 grid.
- NO overlap, NO crooked angles.
- Same scale, same head height position, same body rotation across all cells.

=== EMOTION DESCRIPTORS (per cell) ===

(0,0) DEFAULT: composed observer at rest — quiet attentiveness, distant focus toward the right of frame. Eyes amber-bright, slightly half-lidded. Lips closed gently. The "1800년 침묵에서 깨어난 자" baseline.

(1,0) SMILE: very rare warmth — almost imperceptible softening at one lip corner, eye crease very faint. The tentative smile of someone re-learning the act. NOT a friendly grin.

(2,0) SERIOUS: jaw set firmly, eyes carrying visible weight, brow gathered slightly. Lips closed in a firm line. Eye contact direct with the additional quality of weighing something far older than the moment.

(3,0) SHOCK: lips parted slightly with breath caught, eyes widening (whites visible above iris), pupils dilated, brow lifted. The observer's stillness disrupted but contained.

(0,1) ANGER: cold restraint — eyes narrowed sharply with distant ice (NOT hot fury), jaw tightened, lips pressed thin. Stillness more dangerous than expression.

(1,1) SAD: eyes lowered, lashes hide gaze partially, lips pressed thin without tremor. Slight downward angle of head. Quiet grief — the human memory fragment surfacing.

(2,1) CRY: a single dry tear traced down one cheek, lips pressed firmly, no jaw tremor. Used very sparingly. Observer dignity preserved even in tears.

(3,1) QUIET: no expression — deepest emotional state. Distant attentiveness emptied. Lip corners flat, gaze far away. The "...괜찮다" beat — almost never true. Stillness, not blankness.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 8 cells, all FACING RIGHT.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere.
- Output: 3168 × 2336 PNG, 4-column × 2-row grid sheet.
```
