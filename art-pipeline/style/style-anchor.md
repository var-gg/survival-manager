# Game Image Style Anchor — survival-manager

> 4 블록은 `assemble_prompt.py`가 추출해서 모든 subject prompt의 prefix/suffix로 자동 주입.
> subject 페이지의 `prompt` fence는 캐릭터/스킬/환경 **개별 명세**만 담는다.
> 일관성의 진짜 anchor는 ref 이미지가 아니라 **subject 페이지의 정밀한 명세 텍스트**다.

```text
=== ART STYLE (엄수) ===
Stylized Japanese fantasy gacha-banner character illustration — magnetic, glamorous, top-tier mobile RPG / console JRPG splash art aesthetic.

Stylization (CRITICAL — non-photorealistic):
- Anime-leaning features: large expressive eyes, soft idealized skin, slim elegant proportions.
- Smooth complexion — NO visible pores, NO realistic wrinkles, NO photorealistic skin texture.
- Stylized facial structure — softer than real anatomy, more idealized than realistic.
- This is FANTASY GAME ART, not realism, not concept-art realism, not portrait photography.

Brushwork & rendering:
- Painterly base with rich color washes; cel-shading used SPARINGLY only at hard geometric edges (jawline, cloth fold creases, shield rim, weapon edge).
- Skin and fabric drape: SOFT painterly gradient transitions — NOT hard cel zones.
- Hair: flowing painterly strand work with rim-light highlight, NOT photographic.
- Hand-drawn brushwork feel — intentional line weight, purposeful brush direction, NOT generic AI smoothing.

Magnetic appeal (must achieve):
- This single image must carry the game's appeal on its own — gacha-banner-tier draw.
- Vivid harmonious palette, purposeful saturation (NOT muted realism, NOT neon overload).
- Ornate elegant detail on the character — embroidery, fabric drape, jewelry sparkle, hair sheen.
- Cinematic glow and atmosphere — character feels lit by a story moment, not a studio.

Surface detail (stylized, NOT realistic):
- Beyond what the attached model shows: stylized cloth weave hint, hem embroidery, soft material reflections on metal, hair strand definition.
- AVOID photorealistic detail: NO heavy leather grain rendering across whole surfaces, NO scratched metal realism, NO gritty surface wear.
- "More detailed than the model" means stylization layered ON the silhouette, not realism applied.

Reference relationship:
- The attached image is a SIMPLIFIED 3D MODEL CAPTURE (low-poly, modular humanoid base). Use it for SHAPE / COLOR / SILHOUETTE / OUTFIT-LAYOUT reference ONLY.
- The illustration MUST be more detailed AND more stylized than the model.
- Mental model: "if this illustration is simplified to game-engine geometry, it should reduce to the attached model — same silhouette, same color zones, same proportions."

=== SHADING / LIGHTING ===
Shadow rendering (painterly-first, cel only at edges):
- SOFT painterly gradients dominate skin, hair, and fabric drape (cheek, shoulder, hair backlight, robe folds, leather sheen).
- Hard-edged terminator used SPARINGLY only where geometry truly demands it: under jaw, deep cloth fold creases, shield rim, weapon edge.
- The OVERALL impression is painterly + glamorous, NOT cel-heavy. The character should look hand-painted, not flat-shaded.
- Skin in particular: soft gradient cheek warmth, subtle nose shadow, NO hard cel zones on the face.

Lighting setup: cinematic three-point with warm story atmosphere.
- Key light: warm, from upper-front-left (~30° elevation, 30° azimuth). Catches cheekbone, shoulder, sword/shield highlight.
- Rim light: cool, from upper-back-right, defining silhouette against the magenta background and catching hair edge / shoulder / weapon trim with a clean glow.
- Fill light: soft warm, from below-left, keeping eyes and lower face readable and adding glamorous warmth.
- Atmosphere: subtle bloom on highlights, soft volumetric depth — character feels lit by a story moment, not a sterile studio.

Palette discipline (vivid + harmonious, gacha-banner appeal):
- Saturation purposeful and rich — NOT muted concept-art realism, NOT neon overload.
- Color zones intentional and readable from a distance.
- Warm + cool contrast deliberately staged (warm key on character, cool rim catching silhouette).

=== CHROMA BACKGROUND (CRITICAL — DO NOT IGNORE) ===
Background: solid uniform color #FF00FF (pure fluorescent magenta).
- NO gradient. NO shadow cast on background. NO color variation. Flat fill from canvas edge to character silhouette.
- Magenta covers the entire frame except the subject.

Subject edge: clear 1–2 px dark line-art outline along the entire silhouette.
- Hair strands at silhouette must each have a clean outline.
- Fingers, weapon edges, cloth hems, every contour.
- This outline is what allows clean chroma-key cutout in postprocessing.

FORBIDDEN on subject:
- No magenta / hot pink / fuchsia anywhere — clothing, skin, props, hair, eyes, jewelry, lighting tint, shadow tint.
- If a deep red or violet is part of the design, keep it well below #C040A0 — never approaching the chroma background color.
- Hair shadows lean toward warm brown, never cool pink.
- Skin highlights are warm ivory, never magenta-tinted.

=== NEGATIVE ===
Style avoidance (CRITICAL — character must NOT look photoreal/realistic):
- NO photorealistic skin (no visible pores, no realistic skin micro-texture, no harsh facial wrinkles, no aged-realism rendering).
- NO realistic muscular anatomy / bodybuilding-tier definition.
- NO heavy leather grain rendering across whole garment surfaces (subtle hint only at edges).
- NO scratched-metal realism on weapons (clean stylized highlights only).
- NO gritty surface wear, no dirt, no battle-damage realism.
- NO concept-art-realism palette (avoid muted desaturated everything).
- The character is anime-game-stylized fantasy art, NOT a realistic painting of a real person in costume.

General avoidance:
- Generic AI illustration smoothing (no purposeful brush direction).
- Plastic / waxy skin texture.
- Symmetric "AI face" (eyes too symmetric, lips too symmetric, generic stock proportions).
- Watermarks, signatures, text artifacts in the image.
- Extra fingers, malformed hands, melted weapon shapes.
- Background gradients, painted environments, scenery — background must be FLAT magenta.
- Any magenta tint on the subject.
- Real-world brand logos. Use stylized fantasy sigils only.
- Oversaturated neon palette.
- Multiple characters in one frame (single subject only unless explicitly requested).
- Frame borders, panel layouts, comic-style gutters — this is a single illustration, not a comic page.
```
