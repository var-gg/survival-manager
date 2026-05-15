---
slug: title_main_menu--dusk
kind: environment_site
subject_id: title_main_menu
variant: dusk
emotion: default
refs:
  - hero_dawn_priest:portrait_full
  - town_frontier_village:dusk
aspect: "16:9"
output_size: "1920x1080"
chroma: false
status: idea
---

# Background: title_main_menu (타이틀 화면 / Main Menu Title) — dusk

Use case: `title` (Title 씬 첫 인상 배경 + save slot 메뉴)

V1 Title 씬의 wide vista baseline. 변경 마을 baseline (`town_frontier_village:dusk`)을 prior output ref로 누적해 같은 dusk 톤 / 색조 / Solarum 색채가 이어지도록 한다. 첫 인상은 캠페인의 출발 — 변경 마을이 화면 중앙-하단에 작게 보이고, 변경문이 distance에 silhouette으로 서 있고, 그 너머에 wilderness가 펼쳐지는 wide vista. UI 영역(상단 게임 로고 / 중앙-하단 save slot 3 + 메뉴 버튼)을 비워야 한다.

```prompt
# Background: Main Menu Title — dusk (frontier village vista from above)

A wide cinematic title-screen vista of a small frontier village seen from a slightly elevated viewpoint, with a heavy wooden frontier gate standing in the middle distance and a vast wilderness stretching beyond it under a dusk sky. This is the **first impression of the campaign** — the player should feel the threshold of something larger waiting beyond the gate, while the village itself reads as a small lit refuge they will return to.

**STYLE LOCK (CRITICAL)**: This title vista must look like the **same illustrated world** as the project's character roster splash art (top-tier mobile JRPG / gacha-banner aesthetic, Granblue Fantasy / Honkai: Star Rail 계열). Two reference images are attached: (1) a character portrait from the same project — the canonical painted-world stylization anchor, and (2) the hub baseline `town_frontier_village:dusk` — same village seen close, the canonical dusk palette / Darkest-Dungeon hamlet feel. The new vista must read as the **wider view of the same illustrated world** — same anime-leaning painterly base, sparse cel-edge, vivid harmonious palette, hand-drawn brushwork. **NOT** an oil painting, **NOT** concept-art realism, **NOT** a photoreal landscape, **NOT** a 3D-rendered scene, **NOT** plein air landscape. Anime-painted environment. 16:9 wide.

**FIGURE GUARD (CRITICAL)**: The character reference is for **stylization / palette / brushwork reference only**. **DO NOT include the character (or any human figure) in this illustration.** The vista shows the village + gate + wilderness without people — the title screen frames a world, not a portrait.

## Composition

Camera: elevated wide vista, about 25° tilt-down from a slight rise above the village, **fixed**. The camera looks across the village toward the frontier gate in the distance, with wilderness rolling beyond. Wide cinematic 16:9.

The composition is built in three readable bands stacked vertically so the title-screen UI can sit on top:

1. **Upper band (~25% top, RESERVED FOR LOGO)** — dusk sky stretches across the upper quarter: muted slate-blue overhead transitioning down to a band of warm orange-rust at the horizon. A few painterly cloud streaks. **No important silhouettes in the upper 25%** — this band is for the game logo and subtitle. Sky only.

2. **Middle band (~40%, HERO ZONE)** — the frontier village sits in the lower-left third, painted small and intimate: clusters of wooden buildings (the general store / blacksmith / mercenary post / inn from the hub baseline are recognizable as small lit silhouettes, but rendered at distance scale, not close up), a few lit lanterns and warm window glows just beginning to show. The path from the village winds rightward across the middle distance toward the **frontier gate** (변경문) — a heavy wooden gate flanked by stone pillars, partially open, standing **prominent in the middle-right of the frame**. Beyond the gate: rolling wilderness recedes into hazy distance, a vast pine forest spreading outward, a hint of distant ridges and the suggestion of older ruins or a mound on the far horizon (the lost capital's memory, very small and faint). The contrast — small lit village vs. the vast wilderness beyond the gate — is the emotional core of the frame.

3. **Lower band (~35% bottom, RESERVED FOR SAVE SLOTS / MENU)** — the foreground is a **gentle elevated rise** of grass and trodden earth that the camera looks down from. Painterly grass tufts, a few stones, a single weathered standing stone with a faded Solarum sunburst sigil on the lower-left edge as a quiet anchor. The foreground is **deliberately understated** — no figures, no major props, no bright detail — because the save slot panel and menu buttons (Continue / New Game / Theater / Settings) will sit on top of this band. Subtle texture, not narrative content.

## Mood

**The threshold of something larger.** The village is small and lit and warm, a real place the player will inhabit. The frontier gate is the literal door of the campaign — slightly open, inviting, but the wilderness beyond is genuinely vast. The viewer should feel two things at once: this is home, and this is the beginning. Not dramatic, not melancholic — quietly resolute. The sun has just dropped behind the distant ridges, lanterns are starting to glow in the village, and a single bird (or its silhouette) cuts across the upper sky if it adds to mood.

## Lighting

- Time: dusk, sun just below the distant horizon
- Key light: warm orange-rust horizon glow rising from behind the wilderness ridges, raking low across the gate and the village
- Secondary: cool slate ambient from the upper sky
- Practical: small warm pockets in the village (lanterns, window glows) — the only saturated warm at intimate scale
- Color grading: same dusk LUT as the hub baseline — steel-cool dominant with restrained warm anchors, no over-saturated neon, no dramatic god-rays

## Palette

- Slate-blue (upper sky) — dominant atmospheric
- Warm orange-rust (horizon band, distant gate glow) — restrained warm anchor
- Steel grey (gate stone pillars, distant ridges) — accent
- Weathered wood-brown (gate wood, village rooftops at distance) — body
- Hazy slate-green (distant pine wilderness) — atmospheric mid-tone
- Faint warm yellow (village lanterns, window glows at distance) — small bright accents
- Muted earth-brown (foreground rise) — neutral
- Ash-violet (distant ruins on far horizon, very faint) — barely-there narrative hint

## UI overlay empty zones (CRITICAL)

UI will be drawn on top of this background. The illustration must keep these zones visually quiet:

- **Upper ~25% of frame**: game logo + subtitle. Sky only, no important silhouettes. (Distant ridges peek into the bottom of this band, OK.)
- **Lower ~35% of frame**: save slot panel + menu buttons. Foreground rise + subtle texture only. No figures, no bright props, no narrative-critical detail.
- **Hero zone (middle ~40% horizontal band)**: village + path + gate + wilderness. All narrative content lives here.
- **Left ~25% of hero zone**: village (small intimate scale).
- **Center-right of hero zone**: frontier gate (prominent silhouette).
- **Right ~30% of hero zone**: wilderness + distant ridges + faint ruins hint.

## Continuity with hub baseline (CRITICAL)

The prior output illustration (`town_frontier_village:dusk`) is the canonical visual style baseline. This title vista must:

- Reuse the same dusk palette (slate-blue + warm orange-rust horizon + weathered wood + steel grey)
- Recognizably depict the same village (general store / blacksmith / mercenary post / inn silhouettes readable at distance scale, even if individual building details soften at this scale)
- Reuse the same Solarum sunburst sigil treatment (faded paint, not magic)
- Reuse the same painterly Darkest-Dungeon-style brushwork — painterly base + sparse cel-edge on hard geometry, NOT a different stylization

The **camera and framing** are different (elevated wide vista vs. hub close-up), but the **world** must read as the same place.

## Reproduction feasibility (CRITICAL)

The illustration must look like a stylized low-poly toon-shaded 3D environment painted in painterly anime-game style — recreatable with simple stylized polygon village + wooden gate + distant pine forest silhouettes + toon shader + painterly post-process.
- Village at distance: clusters of bold rectangular roofs, small lantern glow dots — NOT individually painted shingles or window detail.
- Frontier gate: simple stone pillars + heavy wooden door, painterly silhouette readable at middle distance.
- Wilderness: layered pine ridges receding into haze, NOT individually rendered trees.
- Distant ruins: very faint silhouette, simple broken arch shape.
- Foreground rise: painterly grass tufts, NOT individual blades.

## What this is NOT

- NOT a battle scene with HUD, health bars, minimap, or button prompts
- NOT a character close-up — the world is the subject, no figures in the illustration
- NOT a hub close-up like the prior output — this is a *wider* vista from a slightly elevated viewpoint
- NOT photoreal terrain or realistic forest micro-detail
- NOT a top-down map view
- NOT lens blur on backdrop (atmospheric fade is OK)
- NOT bright daylight — this is dusk, the sun has already dropped
- NOT dramatic god-rays or magical light shafts (atmospheric haze only)
- NOT cluttered in the upper 25% (logo zone) or lower 35% (menu zone) — those bands must read quiet
- NOT illuminated/glowing rune carvings — paint and natural haze only
- NOT a different stylization from the hub baseline — same painterly Darkest-Dungeon-style world, just framed wider
```
