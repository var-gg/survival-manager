---
slug: character_theme_hero_grave_hexer--default
kind: skill_icon_theme_sheet
subject_id: character_theme_hero_grave_hexer
variant: default
theme_character_id: hero_grave_hexer
source_character_id: hero_grave_hexer
presentation_only: true
refs:
  - hero_grave_hexer
  - hero_grave_hexer:portrait_full
aspect: "1:1"
output_size: "1568x1568"
chroma: "#FF00FF"
skills:
  - time_distance
  - memory_project
  - jest_bell
  - voice_scar
status: rendered
---

# 묵향 — Skill Icon Theme Sheet (presentation bridge)

이 subject는 캐릭터 이미지 cycle에 속하지 않는다. `SkillId -> IconId -> Sprite` 프레젠테이션 카탈로그가 확정되기 전까지, 묵향의 팔레트와 회상 결사 시각 언어를 빌린 스킬 아이콘 후보 sheet로만 관리한다. 전투 규칙상 캐릭터 전용 스킬 소유권을 의미하지 않는다.

## 산출물

원본 sheet: `art-pipeline/output/character_theme_hero_grave_hexer/default.png`

`art-pipeline/output/icons/skill/character_theme_hero_grave_hexer/skill_icon_<id>.png` 4장:
- skill_icon_time_distance.png — 시간 거리 (active 1, signature)
- skill_icon_memory_project.png — 기억 투사 (active 2, default flex)
- skill_icon_jest_bell.png — 농담의 종 (active 3, retrain pool)
- skill_icon_voice_scar.png — 발언권의 흉터 (variant late-game, ch4 해금)

인게임 스킬 UI button + tooltip illustration 후보. 추상 메타포 baseline — 캐릭터 figure 등장 X.

## 단린/이빨바람과 차이점

회상 결사 mystic motif로 swap:
- 색감: bone-ash beige (#B8B0A3), grave gray (#5F5A54), jade green emission (#6DBE7C). 단린(holy gold)·이빨바람(ochre leather)과 명확히 분리.
- 시간 / 기억 / 종 / 흉터 motif (memory keeper의 4 signature 어휘).
- jade green emission이 tied to weapon crystal — 4 cell에 모두 jade accent로 연결성.

## prompt 명세

```prompt
=== STYLE ANCHOR (skill icon convention) ===
Game UI skill icon style — flat-tier-painterly, centered composition, single focal symbol, magenta background for chroma cleanup.

Skill icons share Pale Conclave (회상 결사) memory keeper aesthetic:
- Color palette: bone-ash beige (#B8B0A3), warm bone (#D8D4C8), deep grave gray (#5F5A54), cool jade green emission (#6DBE7C). Very dark near-black (#15120F) for outlines.
- Memory keeper / time-distance / mourning motif themes — quiet, weathered, layered. NOT holy radiance, NOT hunter primal, NOT magenta/violet sorcery.
- Conclave ritual motif (hourglass/time, scrolls/memory, bells/sound, scars/binding) — NOT angelic light, NOT explosive fire, NOT modern wizardry.

The attached character reference is for COLOR PALETTE anchor only — DO NOT include the character figure in any cell. Cells contain abstract symbol/object compositions only.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 2-column × 2-row sprite sheet of 4 separate skill icon illustrations.

CELL SIZE: each cell exactly 768 × 768 px (square icon).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells.
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (col, row — 0-based, top-left to bottom-right):
- (0,0): skill = time_distance
- (1,0): skill = memory_project
- (0,1): skill = jest_bell
- (1,1): skill = voice_scar

CELL CONTENT — abstract skill icon (per cell):
- Centered focal symbol/object filling roughly 60-70% of the cell.
- NO character figure, NO human silhouette, NO face.
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline on the symbol silhouette for clean chroma cutout.
- Subtle painterly shading on the symbol — NOT pure flat color, NOT photorealistic.
- Each cell features SOME jade accent (#6DBE7C) tying to weapon crystal — small but present.

CELL ALIGNMENT (CRITICAL):
- All 4 cells perfectly aligned in regular 2×2 grid.
- NO overlap, same scale, focal symbol centered in each cell.

=== SKILL ICON DESCRIPTORS (per cell) ===

(0,0) TIME_DISTANCE (시간 거리): an antique hourglass in bone-ash and grave-gray tones, viewed frontal. The hourglass has thin grave-gray brass frame and bone-cream wooden bases. The sand inside is FROZEN mid-fall — a few suspended grains hovering in the middle, NOT flowing. A small jade green crystal (#6DBE7C) embedded in the top cap of the hourglass emits a faint cool glow. Around the suspended sand: very faint cool jade ripples (3-4 thin concentric arcs) suggesting time has stopped. The visual reads as "time held at distance — the moment paused for observation."

(1,0) MEMORY_PROJECT (기억 투사): an open scroll/parchment in bone-cream tone (#D8D4C8) seen frontal, slightly curled at edges, with grave-gray ink markings (vaguely calligraphic — abstract characters, NOT readable). Above the scroll: 3-5 small jade green wisp shapes (#6DBE7C) rising upward as if memories projected outward — translucent, faintly glowing, abstract human silhouettes barely visible within the jade wisps (faces, hands — but vague, like memory itself). NO clearly defined human figures — just suggested presence in the wisps. The visual reads as "memories drawn from record and shown to others."

(0,1) JEST_BELL (농담의 종): a small antique brass bell in bone-ash and grave-gray (#B8B0A3 to #5F5A54), seen frontal, with a thin twisted cord at top and a small jade green crystal (#6DBE7C) embedded as the clapper inside. Around the bell: 3-4 thin concentric sound-wave ripples in cool jade (#6DBE7C) emanating outward — gentle, not aggressive. The bell is mid-toll, just struck. NO ribbons, NO ornate engravings — restrained, weathered. The visual reads as "a quiet bell rung to dispel weight."

(1,1) VOICE_SCAR (발언권의 흉터): a single forearm/wrist seen from above (no full figure — just the wrist and lower forearm), pale skin tone (#E8DDC8), with a single thin scar line traced horizontally across the wrist in deep grave-gray (#5F5A54). The scar is recent but precise — mystic ritual, NOT trauma. Bound around the wrist: a thin cord of jade green thread (#6DBE7C), looped 2-3 times, with a small drop of jade green ink/blood at the scar's center. NO blood spurts, NO violence — the cost is suggested by the precise scar and the binding thread, not by gore. The visual reads as "voice given form through self-binding — speaking through scar."

=== HARD CONSTRAINTS ===
- Single focal symbol/object per cell.
- NO character figure, NO human face, NO body silhouette in any cell (Memory_project's wisp silhouettes are deliberately vague abstractions, NOT proper figures).
- Background of each cell + grid gaps: solid #FF00FF magenta.
- 1-2 px clean dark outline on symbol silhouette in each cell.
- No magenta or pink tint on any symbol — palette stays in bone-ash/cream/grave-gray with cool jade green accents.
- Jade green emission color in EVERY cell (small or moderate) — strictly #6DBE7C family. NO warm amber, NO holy gold, NO violet/pink.
- NO holy radiance, NO warm sunset glow. Jade green is COOL — cool light, not warm light.
- Output: 1568 × 1568 PNG, 2-column × 2-row grid sheet.
```
