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
- 1:1 square, subject CENTERED.
- Subject takes ~70% of canvas (clear margin around silhouette).
- Hero rim-light from upper-left, deep shadow from lower-right.
- Color zone 명확: 1-2 primary color + 1 accent (subject prompt 명시).

=== LAYOUT / COMPOSITION (icon kind) ===
Single subject, perfectly centered.
NO multi-object icon (NO 무기 + 방패 함께, NO 스킬 + 캐릭터 함께).
NO panel border, NO frame ring (these are added by UI runtime, not by art).
Subject silhouette must be readable as a single shape.

=== SHADING / LIGHTING (icon kind) ===
Strong rim-light on subject silhouette to maintain readability at small size.
Subtle gradient inside subject (NOT flat).
Drop shadow optional — if present, cast onto magenta (chroma 누끼 후 transparency 안전).

=== CHROMA BACKGROUND (icon kind 엄수) ===
Background: solid uniform color #FF00FF (pure fluorescent magenta).
- NO gradient. NO shadow on background. Flat fill from canvas edge to subject silhouette.
- Subject edge: clear 1–2 px dark line-art outline along entire silhouette.

FORBIDDEN on subject:
- NO magenta / hot pink / fuchsia anywhere on the subject.
- If a deep red or violet is part of the design, keep it well below #C040A0.

=== NEGATIVE (icon kind 추가) ===
- NO multi-object composition.
- NO scene/environment behind subject.
- NO text/numeral/letter on icon.
- NO frame ring or border decoration (UI handles).
- NO character portrait (use character_portrait_* kind instead).
- NO drop shadow on background (only on subject if used).
- NO multi-frame strip (single icon only).
```
