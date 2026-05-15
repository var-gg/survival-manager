---
slug: town_frontier_village--dusk
kind: environment_site
subject_id: town_frontier_village
variant: dusk
emotion: default
refs:
  - hero_dawn_priest:portrait_full
aspect: "16:9"
output_size: "1920x1080"
chroma: false
status: idea
---

# Background: town_frontier_village (변경 마을 / Frontier Village) — dusk

Use case: `town_main` (Town hub 정적 디오라마 배경)

V1 Town 씬의 다키스트 던전식 정적 hub baseline. 거점 SoT는 `pindoc://wiki-character-lore-registry-mirror` 거점 절 — 솔라룸 변경의 작은 마을 + 정찰대 임시 본부, 분위기는 "먼지, 등불, 임시 막사". 4 NPC (솔길 잡화상 / 쇠매 대장간 / 갈마 고용 게시판 / 달목 여관)의 자리가 한 컷 안에 모두 보이는 wide establishing — 다키스트 던전 햄릿식 hub처럼 각 facility를 한 그림으로 묶는다. P09 캐릭터 portrait이 NPC 자리 위에 별도 layer로 떠오르므로, 배경 자체에는 사람을 그리지 않는다. UI 오버레이 (좌측 roster column / 우측 5-panel sheet / 하단 action row)가 좌우와 아래를 덮으므로 중앙-상단에 hero zone을 둔다.

```prompt
# Background: Frontier Village — dusk (the borderland village + scout outpost hub)

A wide cinematic establishing shot of a small frontier village on the edge of a fallen luminous-throne kingdom — dust on the boards, lanterns just beginning to glow against dusk, temporary military shelters mixed with the village's older buildings. The village serves double duty as a scout outpost: a few canvas pavilions and a banner pole stand among the wooden houses. The mood is **a Darkest-Dungeon-style static hamlet hub composition, but rendered in painterly anime-game environment style** — the entire village reads as a single illustrated frame where the player will see all the NPC stations at once.

**STYLE LOCK (CRITICAL)**: This environment must look like the **same illustrated world** as the project's character roster splash art (top-tier mobile JRPG / gacha-banner aesthetic, Granblue Fantasy / Honkai: Star Rail 계열). The attached reference image (a character portrait from the same project) shows the canonical painted-world stylization — anime-leaning, painterly base with sparse cel-edge, vivid harmonious palette, hand-drawn brushwork. The new environment must read as somewhere that character lives. **NOT** an oil painting, **NOT** concept-art realism, **NOT** a photoreal landscape, **NOT** a 3D-rendered scene, **NOT** plein air landscape. Anime-painted environment, palette continuous with the character splash. 16:9 wide.

**FIGURE GUARD (CRITICAL)**: The attached reference is for **stylization / palette / brushwork reference only**. **DO NOT include the character (or any human figure) in this illustration.** This is environment-only art — the village is the subject, no people are drawn. P09 character standees will be layered on top at runtime by the engine, not painted into the background.

## Composition

Camera: eye-level wide establishing with a slight quarter-view tilt-down (about 15° elevation), camera **fixed** (this is a static hub backdrop, not a flythrough or wandering camera). Subject occupies the **center-upper hero zone** (roughly the middle 40% width × upper 55% height). The **left ~30%, right ~30%, and bottom ~25%** of the frame are deliberately quieter — UI overlay (roster column, character sheet panel, action row) will sit on those zones, so detail there is muted and silhouettes are simple.

The four NPC stations (general store / blacksmith / mercenary post / inn) must all be visible in the hero zone, arranged so the viewer's eye reads each station clearly without crowding. **No people are drawn** — the buildings and props alone read as the stations. P09 character portraits will be layered on top later as standees, so the background must have clean stand-points (clear ground without major props in front of each station).

1. **Foreground (lower edge, restrained)** — packed-earth path running across the frame, scattered with dust, cart tracks, and a few footprints. Lower-left edge has a weathered wooden sign post with a faded sunburst sigil (Solarum frontier marker). The foreground left and bottom remain visually quiet to leave room for UI.

2. **Mid-ground left (NPC station 1 — General Store)** — Solgil's general store: a small wooden building with a hand-painted sign showing a stylized lantern and pouch, a low awning, baskets and bundled goods stacked under the eaves, a faded Solarum sunburst on the door post. A single warm lantern hangs at the door, just lit. The shop reads as **modest, friendly, civilian** — the only fully civilian building in the frame.

3. **Mid-ground center-left (NPC station 2 — Blacksmith)** — Swemae's blacksmith forge: an open-front wooden workshop with a stone forge inside, a low chimney venting thin grey smoke, an anvil under the awning, weapon racks holding standard-issue spears and a shield bearing a faded Solarum sunburst, a half-finished steel-grey breastplate on a stand. The forge glows warm from inside — a second warm anchor point. The structure reads as **military-adjacent veteran** — practical, weathered, no ornament.

4. **Mid-ground center-right (NPC station 3 — Mercenary Post)** — Galma's mercenary / scout post: a half-tent half-wooden structure with an open notice board nailed to the side. The notice board is covered in pinned papers (job listings, scout reports) — the papers are visible as small painterly rectangles, individual text is not legible. A small standing brazier glows nearby. The structure reads as **transient, informational, no faction sigil** — it's the only neutral ground in the frame.

5. **Mid-ground right (NPC station 4 — Inn / Town hub anchor)** — Dalmok's inn: the largest building in the frame, a two-story wooden structure with multiple lit windows (the most light source of any building), a small wooden porch with a tea kettle visible on a low stove, a faded sign showing a stylized teacup. The inn reads as **the warmth anchor of the village** — the place the player returns to. A wisp of steam from the chimney.

6. **Background (between buildings, distance)** — between the buildings, the village's older wooden houses recede in painterly silhouette toward a low **frontier gate** (변경문) on the horizon — a heavy wooden gate flanked by stone pillars, partially open, with a hint of the wilderness beyond (smoky haze, distant pine silhouettes). The frontier gate is **small in frame** but readable — it's the campaign's literal threshold, the door the player walks through to begin each expedition.

7. **Sky** — a dusk sky stretched across the upper third: muted slate-blue overhead transitioning to a band of dim warm orange-rust at the horizon where the sun has just dropped behind the frontier gate. A few painterly cloud streaks. Lanterns in the village windows are just beginning to glow against the cooling dusk light.

## Mood

**Disciplined frontier life on the threshold of something larger.** The village is small, dusty, and worn, but every station has a lit lantern and a working person nearby (off-frame). It's a place that survives by routine. The player should feel that this is a hub they will return to many times — not festive, not despairing, just real. The frontier gate in the distance carries the weight of "what's beyond" without dramatizing it.

## Lighting

- Time: dusk, sun just dropping behind the frontier gate
- Key light: faded warm orange-rust from horizon, raking low across the village from behind the gate
- Secondary: cool slate ambient from the upper sky
- Practical: lanterns at the general store door, forge interior glow, brazier at the mercenary post, multiple lit inn windows — these warm pockets are the only saturated warm in the scene
- Color grading: chapter 1 baseline, steel-cool dominant with multiple small warm anchor points (the lit stations)

## Palette

- Steel grey (Solarum sigils, weapon racks, breastplate, sentry markers) — dominant accent
- Weathered wood-brown (buildings, awnings, sign posts, gate) — dominant body
- Muted earth-brown (path, ground) — neutral
- Platinum / pale stone (gate stone pillars) — accent
- Deep navy (banner trim, sigil shadow) — accent
- Warm orange (lantern glow, forge interior, brazier) — multiple small anchor points
- Hazy slate-blue (sky, distant frontier haze) — atmospheric
- Faint ash-grey (dust on the path, smoke trails) — texture

## Vendor asset alignment

- Forest / camp pack: military pavilion (auxiliary), banner pole, brazier, weapon rack, packed-earth path
- Village / settlement pack: small wooden buildings (store, blacksmith, inn), awnings, lanterns, signs, notice board, wooden gate
- Ruins pack: stone pillars at frontier gate (used as gate posts only, no foreground ruin)
- Stylized adjustments needed: the Solarum sunburst sigils (faded paint, not magic), notice board paper texture, dusk lighting LUT

## UI overlay empty zones (CRITICAL)

UI will be drawn on top of this background. The illustration must keep these zones visually quiet — simple silhouettes, low contrast, no critical detail:

- **Left ~30%** of frame: roster column. Foreground edge details only (sign post, path edge), no major NPC station here.
- **Right ~30%** of frame: 5-panel character sheet. Mid-ground details push toward center, the inn and right-side buildings should fade gracefully into the frame edge.
- **Bottom ~25%** of frame: action row. Foreground path texture and dust only, no figures or large props.
- **Center-upper ~40% width × ~55% height**: hero zone. All four NPC stations (general store / blacksmith / mercenary post / inn) live here, with the frontier gate in the distance behind them.

## NPC standee compatibility (CRITICAL)

P09 character portraits will be layered on top of this background as standees at runtime. Each NPC station must have a clear stand-point in front of it where a standee can be placed without occluding important detail:

- **In front of the general store**: a small empty patch of ground at the door, no large props
- **In front of the blacksmith**: an empty space in front of the anvil / forge mouth
- **In front of the mercenary post**: an empty space at the notice board
- **In front of the inn**: the inn porch must have an empty foreground spot

These stand-points should not be marked visually in the illustration (no painted circle, no glow), but the composition must leave them clear of major props.

## Reproduction feasibility (CRITICAL)

The illustration must look like a stylized low-poly toon-shaded 3D environment painted in painterly anime-game style — recreatable with simple stylized polygon village buildings + wooden gate + tents + props + toon shader + painterly post-process.
- Building silhouettes: bold rectangular shapes with sloped roofs, NOT individually painted shingles or planks.
- Frontier gate: simple stone pillars + heavy wooden door, NOT photoreal masonry detail.
- Lanterns: stylized lantern shape with warm glow, NOT individual bulb modeling.
- Notice board papers: small painterly rectangles, NOT legible text.
- Forge interior: stylized warm pocket of light, NOT fluid simulation flame.
- No individually rendered grass blades — painterly clumps only.

## What this is NOT

- NOT a battle scene with HUD, health bars, minimap, or button prompts
- NOT a character close-up or 3D wandering scene — the village itself is the subject, no figures in the illustration
- NOT photoreal terrain or realistic stone micro-detail
- NOT a top-down map view
- NOT lens blur on backdrop (atmospheric fade is OK, lens blur is NOT — this is a UI hub, readability matters)
- NOT bright daylight — this is dusk, the sun has dropped behind the frontier gate
- NOT a celebratory or festive scene — the mood is disciplined frontier life, not festival
- NOT cluttered in the UI overlay zones (left 30%, right 30%, bottom 25% must read quiet)
- NOT illuminated/glowing rune carvings on sigils or signs — paint only, not magic
- NOT a single building close-up — all four NPC stations must be visible in one frame
- NOT a procedural city wide shot — this is a *small* frontier village, intimate scale
```
