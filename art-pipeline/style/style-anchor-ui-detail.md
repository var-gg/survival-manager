# Game Image Style Anchor — UI Detail Asset (kind: ui_detail_asset)

> Small reusable UI surface asset: 9-slice frame, glow layer, divider, ornament, icon slot frame.
> Use clean magenta chroma and keep base/state layers separated.

```text
=== ART STYLE (ui_detail_asset kind 엄수) ===
Stylized premium anime fantasy RPG UI detail asset. Antique warm-gold trim, deep navy / charcoal interior when requested, restrained painterly bevel, crisp edges, small ornamental accents. The asset must feel compatible with a dark Town UI compendium/codex panel.

This is NOT a full screen mockup and NOT a gameplay icon. It is a reusable UI component part: frame, border, glow, divider, ornament, slot backing, or small state overlay.

Layer separation is mandatory:
- Base frame assets must have NO selected glow and NO strong outside shadow.
- Glow/state assets must contain only the glow/state overlay and no solid frame unless explicitly requested.
- Divider/ornament assets must not include text.

=== LAYOUT / COMPOSITION (ui_detail_asset kind) ===
Single centered UI component on a flat chroma background. The component must be isolated and easy to crop.

For 9-slice frame assets:
- Rectangular or square frame centered on canvas.
- Center must be empty chroma or transparent-looking negative space so Unity background color can show through after chroma cutout.
- The frame border must be thick enough to slice.
- Corner ornaments must stay inside the frame border, not in the stretch center.
- Keep 80px safe margin from canvas edges unless subject prompt overrides.
- Do not bake text, icons, characters, or scene art into the center.

For glow assets:
- Transparent center / chroma center.
- Glow must be a separate halo around the target shape.
- No solid card fill.

For divider/rune line assets:
- Long horizontal centered ornament.
- Empty chroma outside the ornament.
- No readable text; abstract runes/glyph ticks only.

=== SHADING / LIGHTING (ui_detail_asset kind) ===
Painterly bevel and metal highlight are allowed on the component itself. Keep lighting restrained and reusable. Avoid strong cast shadows, large blurred halos, or background haze because those break chroma extraction and state layering.

=== CHROMA BACKGROUND (ui_detail_asset kind 엄수) ===
Background and all cutout holes: solid uniform #FF00FF pure fluorescent magenta.
- NO gradient on chroma.
- NO shadow, blur, glow, particles, or semi-transparent residue outside the component.
- If the center is meant to be transparent after cutout, fill it with pure #FF00FF.
- Component must not use magenta / hot pink / fuchsia.
- Outer edge must be clean enough for flood-fill/chroma removal.

=== NEGATIVE (ui_detail_asset 추가) ===
- NO full UI screen.
- NO game icon subject unless this is explicitly an icon slot frame.
- NO readable text, letters, numbers, UI labels, or fake code.
- NO character portrait, item illustration, weapon art, scene, sky, or environment.
- NO drop shadow or blur outside the intended component boundary.
- NO baked parchment center for dark UI frame assets unless subject prompt explicitly asks for a light parchment panel.
- NO glow baked into base frame variants.
```
