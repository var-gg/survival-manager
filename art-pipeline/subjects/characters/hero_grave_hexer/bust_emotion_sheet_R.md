---
slug: hero_grave_hexer--bust_emotion_sheet_R
kind: bust_emotion_sheet
subject_id: hero_grave_hexer
variant: bust_emotion_sheet_R
direction: facing_right
refs:
  - hero_grave_hexer
  - hero_grave_hexer:portrait_full
  - hero_grave_hexer:portrait_face_default
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

# 묵향 — Bust Emotion Sheet (facing right, 8 emotion 흉상)

## 산출물 (VN 화면 왼쪽 배치용)

`portrait_bust_<emotion>_R.png` 8장. facing camera-right — VN dialogue에서 화면 왼쪽 위치 캐릭터.

bust_L은 별도 prompt 호출 없이 R sheet의 split 결과를 코드 좌우반전. 묵향의 lore-only 비대칭 디테일(손목 흉터 등)은 P09 미표현이며 일러에 포함하지 않으므로 좌우반전 안전.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 8 cells in the grid below are the SAME character with different facial expressions only — same outfit, same hair, same eye color, same skin, same proportions, same identity, same facing direction.

Match the REF for the character's identity:
- Hair: medium-length ivory/ash (#D8D4C8) with darker secondary tone (#8E8980) in shadows. NOT brown, NOT black. Hand-drawn strand definition, draped over shoulder.
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family).
- Skin: light pale tone with slight warmth (#E8DDC8 family), soft idealized.
- Outfit (visible at chest/shoulders): ivory shoulder cloth (#B8B0A3 warm bone-ash) draped over a dark central bodice (#5F5A54 deep grave gray) with subtle satin sheen. Match REF.
- Hat: brimmed hat (bone-ash beige #B8B0A3) visible at top of frame in EVERY cell.
- Skin tone, eye spacing, nose shape, jaw shape, lip shape, hair shape: identical across ALL 8 cells.

DO NOT:
- Change the character identity between cells.
- Use long flowing hair, dark/black hair, or replace the brimmed hat with a pointed wizard hat or hood.
- Add wrist scars, talisman/charm props, hanging bells, or grave-dust accessories.
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
- Crop: head + hat + shoulders + upper chest (down to collarbone area). Hat fully framed top.
- **FACING DIRECTION IS DEFINED BY THIS SPEC — REFERENCES ARE FOR IDENTITY ONLY.** The attached reference images are for hair color, eye color, outfit colors, facial features only. **DO NOT copy the references' facing direction.** Render the facing direction strictly per the angles below.

GAZE ANGLE (precise — applies to ALL 8 cells):
- Eyes look toward the RIGHT side of the frame at approximately 25-30° from frontal axis. The gaze is clearly off-center to the RIGHT.
- Pupils positioned RIGHT of center within each eye.
- The left side of each eye (toward viewer) shows slightly more sclera than the right side.

HEAD ROTATION (precise):
- Head turned approximately 20-25° toward the RIGHT.
- Chin sweeps slightly toward the LEFT side of the frame; back of the head sweeps toward RIGHT.
- RIGHT cheek (camera-side closer) is more visible to viewer than left cheek.
- Ivory hair falls forward over the RIGHT shoulder slightly.
- Hat brim tilts naturally with head rotation.

BODY / SHOULDER ROTATION (precise):
- Body rotated approximately 20-25° so the RIGHT shoulder is closer to the camera (forward in 3/4 view).
- The left shoulder is angled away from camera.
- Robe/shoulder-cloth drape asymmetry follows the rotation.

Mental anchor: "VN dialogue scene, character on LEFT side of screen, looking toward conversation partner on RIGHT."
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline on the character silhouette in each cell.

CELL ALIGNMENT (CRITICAL):
- All 8 cells perfectly aligned in regular 4×2 grid.
- NO overlap, NO crooked angles.
- Same scale, same head height position, same body rotation across all cells.
- HAT visible in every cell.

=== EMOTION DESCRIPTORS (per cell) ===

(0,0) DEFAULT: dry-amused memory keeper at rest — composed with very faint half-smile at one lip corner. Eyes amber-bright with distant focus, gazing toward the right of frame. Lips closed gently. The "한 300년쯤 됐지" baseline.

(1,0) SMILE: a teasing dry smile — slight asymmetric lift at one lip corner, eye softening with subtle wrinkle (earned laugh lines). The dry humor of someone who finds gentle amusement in others' weight. Restrained.

(2,0) SERIOUS: jaw soft but lips closed in a firm line, eyes carrying visible weight without humor. The dry-humor baseline drained — what remains is plain surface of long-carried weight. Eye contact direct.

(3,0) SHOCK: lips parted as if about to speak (or joke) but caught mid-breath, eyes widening (whites visible above iris), pupils dilated, brow lifted. A joke attempt visibly stalling. Hat brim slightly askew.

(0,1) ANGER: cold mystic restraint — eyes narrowed sharply with distant ice (NOT hot fury), jaw tightened, lips pressed thin. Stillness more dangerous than expression.

(1,1) SAD: eyes lowered, lashes hide gaze partially, lips pressed thin without tremor. Slight downward angle of head. The dry mourning of departed contemporaries.

(2,1) CRY: a single dry tear traced down one cheek, lips pressed firmly, no jaw tremor. Used very sparingly. Mystic dignity preserved even in tears.

(3,1) QUIET: no expression — deepest emotional state. Dry humor fully evaporated. Lip corners that usually held a faint half-smile sit flat. Gaze far away, eyes distant. The "...그래. 내 이름이네." beat. Stillness, not blankness — alive but emptied.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 8 cells, all FACING RIGHT.
- HAT visible in every cell.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere.
- Output: 3168 × 2336 PNG, 4-column × 2-row grid sheet.
```
