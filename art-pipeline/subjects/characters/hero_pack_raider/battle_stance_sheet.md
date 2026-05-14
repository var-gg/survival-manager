---
slug: hero_pack_raider--battle_stance_sheet
kind: battle_stance_sheet
subject_id: hero_pack_raider
variant: battle_stance_sheet
refs:
  - hero_pack_raider
  - hero_pack_raider:portrait_full
  - hero_pack_raider:portrait_face_default
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

# 이빨바람 — Battle Stance Sheet (4 stance 풀바디)

## 산출물

`portrait_stance_<id>.png` 4장:
- portrait_stance_idle.png — 평상 자세 (전투 대기, 사냥꾼 sense-ready)
- portrait_stance_attack.png — 공격 모션 (이빨 세우기 — 짧은 사냥칼 lunge)
- portrait_stance_guard.png — 방어 자세 (무리의 자리 — taunt + 회피 자세, 방패 없음)
- portrait_stance_cast.png — 시전 모션 (바람 읽기 — 코를 들어올리는 sense 모션)

인게임 stance sprite + banner art용. portrait_full(풀바디 idle)과 별도 — battle 측에서 더 dynamic한 pose.

## 단린과 차이점

이빨바람은 **방패가 없다**. 따라서 guard stance는 priest의 "shield forward" 패턴 대신 **hunter's evasion + taunt 자세** — 무릎을 살짝 굽히고 한 발 앞으로, sword를 대각선으로 들어 적의 시선을 끄는 모션. cast stance도 priest의 spell light 대신 **wind reading 의례 동작** — 코를 살짝 들어올리고 손바닥을 펴는 sense 모션.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 4 cells in the grid below are the SAME character with different combat stances only — same outfit, same hair, same eye color, same skin, same proportions, same identity, same weapon.

Match the REF for the character's identity:
- Hair: wild shag style, near-black with cool deep tone (#15120F).
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family).
- Skin: light tone, slightly tanned, soft idealized — masculine.
- Outfit: layered hunter leather — ochre leather (#A96F36) on chest/arm/head, dark umber (#6B472D) on waist/leg, bone/amber accents (#C9B58A and #D08A2E) at seams. Match REF.
- Right hand: short hunting sword (Sword_001 family, blade #C9B58A bone/cream tone). NOT shiny gold, NOT mirror silver.
- Left hand: EMPTY — NO shield, NO secondary weapon, NO buckler. The character carries only the single short sword in the right hand.
- Face: sharp masculine silhouette, no facial hair.

DO NOT:
- Change the character identity, outfit, or weapon between cells.
- Add a shield, buckler, axe, secondary blade, or any defensive equipment to the left hand or back.
- Add cape, hood, layered priest vestments, or paladin plate armor.
- Add face tattoos, scarification, bone hair accessories, totem ornaments, multiple belt pouches — they are NOT in the ref.
- Use long hair, copper-auburn hair, brown eyes, or ivory/white outfit.

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
- Same scale, same character size across all cells (each character takes roughly the same vertical span in the cell).

=== STANCE DESCRIPTORS (per cell) ===

(0,0) IDLE: alert hunter rest combat ready. Right hand: sword held at right hip, blade angled slightly downward — relaxed grip, ready to draw fully. Left hand: relaxed at side, empty (no shield). Both arms slightly bent. Weight evenly on both feet, but slightly forward — coiled tracker stance. Slight nose-lift (~3°) reading the wind. Eyes alert toward the threat direction. The "냄새가 다르다" baseline posture — before the strike begins.

(1,0) ATTACK: mid-lunge offensive stance — "이빨 세우기" signature motion. Right hand: sword RAISED in a diagonal mid-strike position, blade angled diagonally up-right with momentum lines visible (the blade is aimed at a target's weak point, not a wide swing). Left foot stepping forward aggressively. Left hand: extended back/down for balance, fingers slightly spread (animal pose). Body weight forward, momentum visible in hair and leather drape motion. Eyes locked on imminent target with predatory focus. NOT mid-impact — the moment before the short blade slips into the gap.

(0,1) GUARD: defensive evasion stance — "무리의 자리" taunt variant (NO shield). Right hand: sword raised diagonally up-left (across body, blade pointed toward an off-screen threat), drawing the enemy's attention. Left hand: extended forward/sideways, palm open in a "stay back" gesture toward an unseen ally behind/beside the hunter. Body lowered into a slight crouch — knees bent, weight on the balls of the feet, ready to dodge or pivot. Head turned slightly toward the threat, eyes locked sharply. The "내가 적의 시선을 가져온다" posture — protecting an ally by being more dangerous.

(1,1) CAST: wind-reading sense stance — "바람 읽기" signature motion. Right hand: sword grounded (held loosely at right side, blade tip near earth or floor — NOT raised). Left hand: open palm raised at chest height, fingers slightly spread, palm tilted up as if catching wind currents. Head tilted slightly upward (~10°), nose subtly lifted, eyes half-closed in concentration.

ABOVE THE PALM (CRITICAL — strict color requirement):
- DO NOT render swirling wisps, smoke tendrils, magical curls, ribbon-shapes, or any wizard-spell-style visualization.
- DO NOT render any pink, magenta, violet, purple, fuchsia, or cool-rose tones above the palm — even subtle ones.
- The palm has nothing dramatic above it. At most: a very faint, FLAT, blurred warm-yellow haze (like dust caught in late afternoon sunlight), color strictly in the warm yellow-ochre-cream family (#D08A2E to #FFD979 to #C9B58A only).
- If you would otherwise add a magical visualization, OMIT IT ENTIRELY and leave the air above the palm clear. The pose alone (palm raised, head tilted, eyes half-closed) communicates the sense-reading.
- This is NOT magic, NOT a spell, NOT a priest cast. The hunter is simply listening to the wind with his palm — the visual is subtractive (no effect), not additive (no glow).

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 4 cells.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere.
- Sense glow in CAST cell: AVOID rendering altogether if you would otherwise produce pink/magenta/violet wisps. If included, strictly warm yellow-ochre-cream (#D08A2E to #FFD979 to #C9B58A) as a flat blurred haze — NEVER as a swirling wisp/tendril pattern.
- LEFT HAND IS EMPTY in ALL cells. NO shield, NO secondary weapon. (Except CAST cell where left palm is raised open for sense-reading; still empty of any item.)
- Output: 1568 × 2336 PNG, 2-column × 2-row grid sheet.
```
