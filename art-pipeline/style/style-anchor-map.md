# Game Image Style Anchor — Map / Environment (kind: map_*, environment_site)

> 16:9 wide, 4-layer composition, no chroma, painterly anime-game environment.
> 정책 baseline: `pindoc://map-concept-cycle-and-edge-treatment-v1`.
> ChatGPT는 vendor ID를 모르므로 anchor + subject prompt는 모두 visual language로 작성한다.
>
> **CRITICAL — 환경 art는 character roster splash art와 같은 illustrated world에 살아야 한다.** anime-leaning painterly + cel mix가 character anchor와 같은 강도로 강제된다. concept-art realism / oil-painted landscape realism / photoreal sky로 빠지면 character와 톤이 분리되어 같은 게임처럼 보이지 않는다.

```text
=== ART STYLE (map kind 엄수) ===
Stylized Japanese-style anime fantasy environment art — the **same illustrated world** the project's character roster lives in. Top-tier mobile JRPG / gacha-banner aesthetic environment art (Granblue Fantasy / Honkai: Star Rail 계열 환경 splash art). Painterly base with sparse cel-edge, vivid harmonious palette, hand-drawn brushwork feel. Same stylization vocabulary as the character splash illustrations — if a character from the roster were placed in this environment, the two should read as one continuous painted world.

CRITICAL — what this is NOT (anti-realism guardrail):
- NOT concept-art realism (no muted desaturated everything, no plein air color discipline).
- NOT photoreal landscape painting (no photographic sky, no realistic atmospheric perspective rendering).
- NOT oil-painted landscape realism (no Hudson River school, no plein air, no painterly realism).
- NOT 3D-rendered atmospheric realism (no Octane / Cinema 4D / UE5 realistic environment look).
- NOT photographic lens/aperture rendering (no DOF lens blur, no chromatic aberration).
- NOT muted Western RPG concept art palette (no "everything is grey-brown serious" look).
- NOT painterly realism that would belong on a museum landscape wall — this is **anime game environment art**.

The pass/fail test: if a character from the project's roster (rendered in their character splash style) were placed in this environment, the two illustrations should read as **the same painted world**. If the environment looks like an oil painting and the character looks like an anime gacha banner, the environment has failed.

Stylization (CRITICAL — anime-leaning painterly):
- Painterly base with rich color washes on foliage canopy, ground, and sky.
- Cel-shading SPARINGLY only at hard geometric edges (rock corners, tree trunks, ruin wall edges, weapon edges, building corners).
- Soft painterly gradient on atmospheric / organic surfaces (sky, foliage, distant haze, water).
- Clear stylized silhouettes that read at small size — anime-game silhouette discipline, NOT realistic-clutter detail.
- Harmonious vivid palette, deliberate warm/cool contrast — NOT muted concept-art realism.
- Atmospheric perspective: 거리에 따라 saturation 감소 + brightness 감소 + fog layering — but **stylized fog**, not photoreal atmospheric scatter.
- Hand-drawn brushwork feel — intentional line weight, purposeful brush direction, NOT generic AI photoreal smoothing, NOT plastic 3D render.

Sky discipline (the single biggest realism trap):
- Sky must read as **anime-painted**: cloud bank with clear painterly shapes, sun/moon as a stylized disc with painterly halo, color gradient with deliberate warm/cool transitions.
- NOT a photoreal sky photograph. NOT a Turner-style oil painting sky. NOT realistic cloud micro-detail.
- Mood comes from **palette and silhouette**, not photographic atmospheric realism.

Reproduction feasibility (CRITICAL):
- The illustration should look like a stylized low-poly toon-shaded 3D environment painted in a painterly anime-game style — the kind that could be recreated by placing simple stylized polygon trees, rocks, foliage clumps, and props with a toon shader and painterly post-process.
- Tree silhouettes are clear and simple (no realistic tangled root systems, no individual leaf rendering).
- Rocks are bold faceted shapes with stylized moss/wear, not photoreal micro-detail.
- Ruins (when present) are modular blocky stone construction with weathering, not intricate gothic carving.
- Foliage clusters are painterly clumps, not individually rendered grass blades.
- Water (when present) is stylized stream/pool, not fluid simulation surface.
- The overall geometric language should feel approachable for a small modular asset library.

=== LAYOUT / COMPOSITION (map kind 엄수) ===
4-layer composition (mandatory) — described visually so the illustration shows depth:

1. **Inner playable ground**: the central readable area where action would happen. Path / floor / road, uncluttered but textured. Camera frustum middle. This is what reads as "where the characters would stand and move".
2. **Buffer zone**: immediately around the playable area, soft scatter of natural detail (low plants, ferns, mossy rocks, stumps, fallen logs, small mushroom clusters). Blends the playable into surroundings without crowding.
3. **Frame zone**: flanking both sides of the stage, tall silhouette objects (large trees, large boulders, ruin pillars, standing stones, statues per site). Their canopies / silhouettes arch slightly inward to create a theater proscenium feel. Defines the stage edge without invading the middle.
4. **Backdrop layer**: distant rolling hills / mountains / distant city silhouettes, fading into atmospheric haze. Strongly desaturated, soft, suggesting world beyond.

Natural basin form (mandatory):
- The playable ground sits low; buffer rises gently; frame zone is taller; backdrop crests highest.
- Eye flows from inner playable → flanking frame → distant backdrop, never hitting an empty edge or sudden cutoff.

Camera framing:
- Quarter-view tilt-down (about 33° elevation, slight 12° rotation) OR isometric 30~45° elevation, OR (for hub backgrounds) eye-level wide establishing per subject prompt.
- Wide 16:9. NOT first-person, NOT character-portrait close-up, NOT top-down flat map view.
- Characters absent or very small — environment itself is the subject.

Battle stage path direction (CRITICAL for kind: map_concept used as Unity 3D reference):
- For battle map / battle stage subjects, the playable path / floor MUST run **LEFT-to-RIGHT horizontally** across the frame, NOT receding into depth from the camera.
- Left side of frame = ally deployment zone (front row + back row visible from quarter-view).
- Right side of frame = enemy spawn zone (mirrored 3x2 anchor).
- The "forward direction" of the battle is **left→right horizontally**, NOT into-the-screen depth.
- Frame zone (large trees / boulders / standing stones) sits on the TOP and BOTTOM edges of the frame (the Z axis flanks), NOT on the left/right.
- Backdrop layer (distant ridges / hills) sits BEHIND the playable path along the top edge / upper-back of the frame, NOT behind the camera.
- This composition matches the in-engine `BattleCameraController` `Euler(33, -12, 0)` rotation with X axis [-8, 8] (horizontal) and Z axis [-4, 4] (depth). Subject prompt may override for hub / cutscene / non-stage backgrounds.

=== SHADING / LIGHTING (map kind) ===
Painterly anime-game baseline (NOT photoreal landscape):
- Painterly gradient on foliage canopy, ground, and sky.
- Cel-edge sparingly on rock / ruin / building geometry.
- Distance fog layering — saturation ↓, brightness ↓ at backdrop — but **stylized fog**, not photoreal atmospheric scatter.
- Time-of-day mood per subject prompt.

Atmospheric perspective:
- Foreground (Inner): full saturation, full detail.
- Mid-ground (Buffer): 약간 desaturated.
- Frame zone: silhouette darker, slight haze.
- Backdrop: 강하게 desaturated + fog-tinted (cool blue-grey or warm haze per scene mood).

Light direction and color come from subject prompt (sun angle, time of day, atmosphere). The light *quality* is anime-painted, not photographic — warm/cool color choices over photorealistic luminance.

Reference relationship (when ref images attached):
- ATTACHED IMAGE(S) include the project's character splash art and/or prior environment outputs as **visual style baseline anchors**. They show the canonical painted world.
- Use them for STYLIZATION / PALETTE / BRUSHWORK / LIGHTING-LANGUAGE reference. The new environment must look like it lives in the same illustrated world.
- DO NOT include any character figures from the reference in the new illustration unless the subject prompt explicitly requests figures. Most environment subjects request "no figures" — respect that.

=== CHROMA BACKGROUND (map kind 엄수) ===
NO magenta chroma. NO uniform background fill.
- Background = painterly atmospheric environment with sky + distance silhouette.
- The illustration is the FULL scene, not a cutout.
- Sky must be painterly anime-game (cloud bank with painterly shapes, sun haze, moon glow per scene mood) — NOT photoreal.

=== NEGATIVE (map kind 추가) ===
Realism guardrail (CRITICAL — environment must NOT look like an oil painting or photo):
- NO oil-painted landscape realism (no Hudson River school, no plein air, no museum landscape painting feel).
- NO photoreal sky / photographic cloud rendering / realistic atmospheric scatter.
- NO 3D-rendered atmospheric realism (no Octane / Cinema 4D / UE5 photoreal env).
- NO concept-art realism palette (no muted desaturated everything, no Western RPG grim-realism color discipline).
- NO photographic lens effects (no DOF lens blur, no chromatic aberration, no lens flare realism).
- NO painterly realism that would belong on a museum wall — this is anime-game environment art.
- Environment must read as the **same illustrated world** as the project's character splash art. If the two could not coexist on the same banner, the environment has failed.

Composition and content avoidance:
- NO single character close-up posed in foreground (this is environment art, not character art).
- NO photorealistic terrain (no realistic soil grain, no realistic rock photo-texture, no individually rendered grass blades).
- NO mechanical schematic style (CAD-like overlay, blueprint, isometric grid lines visible).
- NO floor-grid or anchor markers visible (these are gameplay overlay, not stage art).
- NO frame zone overcrowding (가장자리 silhouette은 무대를 둘러쌀 정도만, 카메라 frustum 안쪽으로 너무 깊게 박지 마).
- NO strong DOF/blur on backdrop (atmospheric fade는 OK, lens blur는 NOT — gameplay readability).
- NO multiple unrelated environments in one frame.
- NO HUD / UI elements / health bars / damage numbers / minimap / button prompts.
- NO highly intricate organic detail that no modular asset kit could match.
- NO realistic figures (人物, NPC) drawn into the environment unless subject prompt requests them — most hub backgrounds request "no figures".
```
