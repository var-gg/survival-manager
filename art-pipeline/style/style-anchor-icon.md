# Game Image Style Anchor — Icon (kind: skill_icon / passive_icon / equipment_icon)

> 1:1 centered icon, magenta chroma, readable at 64px.

```text
=== ART STYLE (icon kind 엄수) ===
Stylized JRPG mobile game ability icon — single concept silhouette, centered, painterly with strong rim/key light.

Stylization:
- Painterly base + cel-shading at silhouette outline (allows clean 64px scaling without loss).
- Single concept (one weapon, one rune, one symbol) — NO scene, NO character figure, NO environment background.
- High contrast silhouette → 64px scaling 시 still readable.

Composition:
- 1:1 square, subject CENTERED. For explicit sheet subjects, apply this rule independently inside every cell.
- Subject takes ~70% of canvas (clear margin around silhouette).
- Hero rim-light from upper-left, deep shadow from lower-right.
- Color zone 명확: 1-2 primary color + 1 accent (subject prompt 명시).

=== LAYOUT / COMPOSITION (icon kind) ===
Single subject, perfectly centered. For explicit sheet subjects, each cell is one separate centered icon.
NO multi-object icon (NO 무기 + 방패 함께, NO 스킬 + 캐릭터 함께).
NO panel border, NO frame ring (these are added by UI runtime, not by art).
Subject silhouette must be readable as a single shape.

=== SHADING / LIGHTING (icon kind) ===
Strong rim-light on subject silhouette to maintain readability at small size.
Subtle gradient inside subject (NOT flat).
All glow/shadow/highlight effects must stay INSIDE the subject silhouette or inside the outer stroke.

=== CHROMA BACKGROUND (icon kind 엄수) ===
Background: solid uniform color #FF00FF (pure fluorescent magenta).
- NO gradient. NO shadow on background. NO blur, glow, particles, haze, or semi-transparent residue outside the subject.
- Flat #FF00FF fill from canvas edge to the OUTER stroke.
- Subject edge: continuous clean 2–4 px outer stroke along the entire silhouette.
- The area outside the outer stroke must be pure #FF00FF only for flood-fill / chroma removal.

FORBIDDEN on subject:
- NO magenta / hot pink / fuchsia anywhere on the subject.
- If a deep red or violet is part of the design, keep it well below #C040A0.

=== NEGATIVE (icon kind 추가) ===
- NO multi-object composition.
- NO scene/environment behind subject.
- NO text/numeral/letter on icon.
- NO frame ring or border decoration (UI handles).
- NO character portrait (use character_portrait_* kind instead).
- NO drop shadow, glow, or blur outside the outer stroke.
- NO multi-frame strip unless the subject prompt explicitly declares a sprite sheet. For sheet subjects, keep one isolated icon per cell with solid #FF00FF gutters.
```
