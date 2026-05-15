---
slug: site_wolfpine_trail--concept_thumbnail
kind: map_concept
subject_id: site_wolfpine_trail
variant: concept_thumbnail
emotion: default
refs: []
aspect: "16:9"
output_size: "1920x1080"
chroma: false
status: idea
---

# Site Wolfpine Trail — concept thumbnail (cycle 1)

```prompt
# Wolfpine Trail — concept thumbnail

A peaceful but quietly tense pine forest path at golden hour, viewed as **a battle stage that the in-engine camera will see**. Stylized painterly cartoon environment in the tradition of premium mobile JRPG world maps and high-end console anime fantasy game environment splash art — anime-leaning fantasy game aesthetic, NOT photoreal, NOT concept-art realism. The viewpoint is a quarter-view tilt-down perspective (about 33° elevation, slight 12° rotation), wide cinematic 16:9. No characters in the scene — the environment itself is the subject.

**BATTLE STAGE CAMERA (CRITICAL — Unity 3D reference)**: This illustration must match the in-engine `BattleCameraController` `Euler(33, -12, 0)` quarter-view. The playable path runs **LEFT-to-RIGHT horizontally** across the frame, NOT receding into depth. Left side of frame = ally deployment zone (3×2 anchor pad visible from above-front). Right side of frame = enemy spawn zone (mirror 3×2 anchor). The "forward direction" of the battle is **left→right horizontally**. Frame zone (large pines, boulders, standing stones) sits on the **TOP and BOTTOM edges** of the frame (the Z-axis flanks of the playable corridor), NOT on the left/right. Backdrop (distant pine ridges) sits on the **upper-back edge** of the frame, behind the path, fading into haze.

## Scene composition (battle stage, path running LEFT-to-RIGHT horizontally)

1. **Inner playable ground (the horizontal corridor across the middle of the frame)** — a wide trampled dirt path runs **left-to-right across the central middle band of the frame**, scattered with pine needles, flanked above and below by short sage-green grass. The path color is warm umber. The path is **roughly horizontal**, slightly meandering but maintaining its left-right orientation, NOT winding into depth. It is the readable battle corridor where 4v4 auto-skirmish would happen — left side reads as ally deployment, right side as enemy spawn. Uncluttered and textured.

2. **Buffer zone (immediately above and below the path, NOT on it)** — soft natural scatter blends path into forest: low ferns, small painterly grass tufts, mossy rocks, weathered tree stumps, one or two fallen logs **lying just above and below the path edge** (along the Z-axis flanks). Small clusters of mushrooms tucked near roots. Nothing crowds the path — clear breathing room for action readability.

3. **Frame zone (TOP and BOTTOM edges of the frame, framing the horizontal corridor)** — tall pine trees rise as silhouettes along the **top and bottom edges of the frame**, framing the horizontal path corridor like a theater stage seen from above-front. Among them, a few large mossy boulders anchor the ground. **Two or three weathered standing stones** — dark grey rough-hewn shapes with subtle carved markings (old territorial totems of the Wolfpine indigenous forest tribe) — sit at the upper and lower edges of the path, just off the playable corridor. Frame zone defines the corridor edge without invading the path middle. NOTE: standing stones sit on the **Z-axis flanks (top and bottom of frame)**, NOT at the left or right ends of the path.

4. **Backdrop layer (upper-back of frame, behind the path)** — beyond the path, along the upper edge of the frame and slightly above, rolling hills covered in pine silhouettes recede into warm hazy distance. The haze grows stronger with distance, desaturating far hills into soft greenish-grey. A few god-ray light shafts cut through the canopy from the **upper-back** (matching the camera's slight 12° rotation), catching dust motes and atmosphere. The horizon is small in frame — most of the frame is occupied by the playable corridor + frame zone + buffer.

## Natural basin form (CRITICAL)

The playable path sits low. Buffer rises gently. Frame zone is taller still. Backdrop hills crest highest. The eye flows from path → flanking pines → distant hills, never hitting an empty edge or sudden cutoff. No "edge of world" feeling — the forest extends naturally beyond the frame.

## Atmosphere & lighting

Low-angle warm sunlight from upper-left, raking across the canopy. Soft god-rays filter through the trees. A light atmospheric fog veils the middle distance — not obscuring, just softening — and grows toward the backdrop. The mood is peaceful, slightly mysterious, the kind of forest you would cross at dawn before something significant happens. Tense peace.

Atmospheric perspective:
- Foreground (path + buffer): full saturation, full detail
- Middle (closer frame trees): slightly desaturated
- Frame zone deeper into stage: silhouette darker, slight haze
- Backdrop hills: strongly desaturated, fog-tinted toward warm greenish-grey with a hint of gold from the low sun

## Palette

- Warm umber dirt path
- Muted sage understory
- Pine green canopy with golden rim highlights from sunlight
- Warm grey-brown bark
- Mossy stone with cool green-grey accents
- Hazy warm greenish-grey backdrop
- Gold sun shafts and floating dust motes

## Mood

Tense peace. The setting feels like the threshold of an indigenous forest territory — beautiful but watchful. The weathered standing stones suggest old territorial markers, but nothing overtly aggressive. The viewer should feel drawn to walk the path while sensing the forest is alive and observant.

## Reproduction feasibility (CRITICAL)

The illustration should look like a stylized low-poly toon-shaded 3D environment painted in a painterly anime-game style — recreatable by placing simple stylized polygon trees, rocks, foliage clumps, and props with a toon shader and painterly post-process. Tree silhouettes are clear simple shapes (no realistic tangled roots, no individual leaf rendering). Rocks are bold faceted with stylized moss. Foliage is painterly clumps, not individual blades.

## Camera (matches in-engine `BattleCameraController`)

- Quarter-view tilt-down, **`Euler(33, -12, 0)`** rotation (33° pitch, -12° yaw), wide 16:9
- Camera placed above-front-left of the playable corridor, looking down-right at it
- Path runs **LEFT-to-RIGHT horizontally** across the central middle band — NOT receding into depth
- Left edge of path = ally deployment side, Right edge of path = enemy spawn side
- Frame proportions:
  - Path corridor + buffer (the horizontal middle band): ~50% of frame height, spanning the full frame width
  - Frame zone (top + bottom edges, large trees / standing stones / boulders): ~25% top + ~15% bottom
  - Backdrop (upper-back, distant ridges + sky): ~10-15% upper edge
- This composition is intended as **direct reference for Unity 3D stage authoring** — vendor prefab placement should match this layout when viewed through the in-engine camera

## What this is NOT

- NOT a character close-up or portrait
- NOT photoreal terrain or realistic rock micro-detail
- NOT a top-down flat map view
- NOT a battle screen with HUD, health bars, damage numbers, minimap, or button prompts
- NOT crowded — there must be clear breathing room around the path
- NOT lens blur / strong depth-of-field on backdrop (atmospheric fade is OK, lens blur is NOT)
- **NOT a forward / depth-receding cinematic perspective** — the path must run LEFT-to-RIGHT horizontally, NOT vanish into the back of the frame
- **NOT standing stones at the LEFT or RIGHT ends of the path** — standing stones sit on the TOP and BOTTOM edges (Z-axis flanks), framing the corridor without blocking the ally/enemy spawn ends
```
