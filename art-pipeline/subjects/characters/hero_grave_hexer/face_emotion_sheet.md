---
slug: hero_grave_hexer--face_emotion_sheet
kind: face_emotion_sheet
subject_id: hero_grave_hexer
variant: face_emotion_sheet
refs:
  - hero_grave_hexer
  - hero_grave_hexer:portrait_full
aspect: "2:1"
output_size: "3168x1568"
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

# 묵향 — Face Emotion Sheet (8 emotion frontal)

## 산출물

이 페이지 1번 호출로 8 emotion 얼빡 portrait를 한 sheet에 동시 생성. sheet_split.py로 8개 individual PNG로 분리.

## 단린/이빨바람과 차이점

emotion 표현 baseline이 **mystic restraint + 시간 거리감**. 단린의 priest restraint, 이빨바람의 hunter restraint와 다른 축. 묵향의 baseline은 dry-amused composure (건조한 유머가 입가에 살짝). 가드레일이 무너지는 비트(quiet, sad, shock)에서 **농담 시도하다 멈춤** — 입가 주름이 평소와 다른 형태로 굳음. cry는 매우 sparingly (캠페인에서 사실상 1번 정도).

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 8 cells in the grid below are the SAME character with different facial expressions only — same outfit collar, same hair, same eye color, same skin, same head proportions, same identity.

Match the REF for the character's identity:
- Hair: medium-length ivory/ash (#D8D4C8) with darker secondary tone (#8E8980) in shadows. NOT brown, NOT auburn, NOT black, NOT pure platinum white. Hand-drawn strand definition, slightly draped over shoulder.
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family). NOT brown, NOT green, NOT blue.
- Skin: light pale tone with slight warmth (#E8DDC8 family), soft idealized — NOT corpse white, NOT translucent.
- Outfit collar (visible at neck/upper chest area): ivory shoulder cloth (#B8B0A3 warm bone-ash) draped over a dark bodice (#5F5A54 deep grave gray) — both visible just below the collarbone. NOT priest white, NOT hunter leather.
- Hat: brimmed hat (bone-ash beige #B8B0A3) visible at the top of frame in EVERY cell — DO NOT omit the hat, DO NOT use a tall pointed wizard hat, DO NOT use a hood. Match REF's hat shape exactly.
- Skin tone, eye spacing, nose shape, jaw shape, lip shape: identical across ALL 8 cells.

DO NOT:
- Change the character identity between cells.
- Reinterpret the character into a different archetype (witch, necromancer, dark sorceress).
- Use long flowing hair, dark/black hair, brown eyes, or priest/ivory white collar.
- Replace the brimmed hat with a pointed wizard hat or hood.
- Add wrist scars, talisman/charm props, hanging bells, or grave-dust accessories — they are NOT visible in the ref.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 4-column × 2-row sprite sheet of 8 separate face close-up portraits.

CELL SIZE: each cell exactly 768 × 768 px (square close-up).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells (horizontal and vertical).
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (column index, row index — 0-based, top-left to bottom-right):
- (col=0, row=0): emotion = default
- (col=1, row=0): emotion = smile
- (col=2, row=0): emotion = serious
- (col=3, row=0): emotion = shock
- (col=0, row=1): emotion = anger
- (col=1, row=1): emotion = sad
- (col=2, row=1): emotion = cry
- (col=3, row=1): emotion = quiet

CELL CONTENT — face close-up (per cell):
- Crop: face only — top of head + hat brim visible at top edge, neck/collar at bottom edge, ears within frame, eyes positioned on upper-third line.
- Frontal view with subtle 5-10° rotation. Face fills most of the cell.
- HAT BRIM fully visible at top of cell (do NOT crop the top of the hat — frame loosely enough to show the hat completely).
- Background of each cell: solid #FF00FF magenta (same as grid gap).
- 1-2 px clean dark outline along the character silhouette in each cell — for clean chroma cutout.

CELL ALIGNMENT (CRITICAL):
- All 8 cells perfectly aligned in a regular 4×2 grid.
- NO overlap between cells.
- Same scale/zoom across all 8 cells.
- Same head height position within each cell.

=== EMOTION DESCRIPTORS (per cell) ===

(0,0) DEFAULT: dry-amused memory keeper at rest — composed with very faint half-smile at one lip corner (the dry-humor baseline). Eyes amber-bright with distant focus, as if listening to something just past the viewer. Lips closed gently. The expression of someone for whom yesterday and a thousand years ago feel similar in distance. NOT smiling broadly, NOT solemn, NOT grim. The "한 300년쯤 됐지" baseline.

(1,0) SMILE: a teasing dry smile — slight asymmetric lift at one lip corner, eye softening with a small wrinkle (laugh lines that have been earned). Not warm-grin, but the smile of someone who finds gentle amusement in others' weight ("단린, 너 또…"). Restrained, knowing.

(2,0) SERIOUS: jaw soft but lips closed in a firm line, eyes carrying visible weight without humor. Brow gathered subtly. The dry-humor baseline drained — what remains is the plain surface of long-carried weight. Eye contact direct, intent. The moment when the joke would have been her tool but she chooses not to use it.

(3,0) SHOCK: lips parted as if about to speak (or joke) but caught mid-breath, eyes widening (whites visible above iris), pupils dilated, brow lifted. A joke attempt visibly stalling. The "흑지의 두루마리" first-discovery beat — recognition arriving before words. Hat brim slightly askew suggesting the small physical jolt.

(0,1) ANGER: cold mystic restraint — eyes narrowed sharply with distant ice (NOT hot fury), jaw tightened, lips pressed thin. A stillness more dangerous than expression. The detachment of someone who has seen rage cycle through centuries and chosen not to add to the noise.

(1,1) SAD: eyes lowered, lashes hide gaze partially, lips pressed thin without tremor (her grief is dry, not weeping). Slight downward angle of head. The mourning of departed contemporaries — old, accustomed, but never lighter. The quiet weight of "동시대 동료들 거의 전부 Hollow."

(2,1) CRY: a single dry tear traced down one cheek, lips pressed firmly, no jaw tremor — restrained absolutely. Eyes still open but gaze far away. Used very sparingly — used only once or twice across the campaign. Mystic dignity preserved even in tears.

(3,1) QUIET: no expression — the deepest emotional state. The dry humor has fully evaporated. Lip corners that usually held a faint half-smile now sit flat — the absence of the smile is the signal. Gaze far away, eyes distant, lips relaxed but lifeless. The "...그래. 내 이름이네." beat — when the joke that would have been her tool simply fails to form. Stillness, not blankness — alive but emptied. Hat brim shadows fall heavier across the eyes.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 8 cells.
- HAT visible in every cell.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere.
- Output: 3168 × 1568 PNG, 4-column × 2-row grid sheet.
```
