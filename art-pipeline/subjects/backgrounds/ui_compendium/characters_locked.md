---
slug: ui_compendium--characters_locked
kind: environment_site
subject_id: ui_compendium
variant: characters_locked
emotion: default
refs:
  - ui_compendium:dusk
  - hero_dawn_priest:portrait_full
  - town_frontier_village:dusk
aspect: "16:9"
output_size: "1920x1080"
chroma: false
status: pending
---

# UI Mockup: ui_compendium (캐릭터 도감) — Characters tab, locked/unlocked

Use case: Town 씬 **System Compendium modal**의 **Characters tab selected** 2차 기준 시안.
1차 `dusk`(Skills tab)가 같은 modal family의 catalog/detail/VFX theater 골격을 닫았다면, 이 시안은
캐릭터 도감 고유의 **점진적 언락 감성**(collection / reveal)을 시각화한다. 슬롯은 처음부터 보이지만
미공개 캐릭터는 id·초상·서사를 노출하지 않고 실루엣/잠금으로 보이고, 공개된 캐릭터는 초상·클래스·역할·
종족·언락 라벨을 드러낸다. 이미 Unity에 출시된 `task-character-compendium-unlock-surface`
(card grid + silhouette + locked redaction, commit `8f4f831d`)가 현재 구현 REF이며, 이 브리프는 그
구현을 한 단계 끌어올리는 mockup 타깃이다.

이 시안은 `pindoc://analysis-compendium-ui-mockup-brief-v0`의 modal 골격을 따르되 Characters tab으로
확장하고, `pindoc://flow-compendium-ui-ref-calibration-v1` 왕복 루프의 character 변형 anchor가 된다.

```prompt
# UI Mockup: System Compendium modal — Characters tab selected, locked/unlocked collection, dusk village backdrop

An illustrated UI mockup screenshot of a premium mobile JRPG / strategy RPG game's System Compendium modal, floating over the frontier village hub at dusk. This screen shows the **Characters** tab selected — a hero collection codex where some heroes are unlocked (revealed) and many are still locked (mystery silhouettes). 16:9 wide. The emotional core is the satisfying tension between a few proudly-revealed portraits and a grid of locked silhouettes waiting to be earned.

**STYLE LOCK (CRITICAL)**: Same visual family as the attached `ui_compendium:dusk` Skills-tab mockup — identical modal frame, header, tab strip, filter row, left catalog rail + right detail rail proportions. Reuse that exact frosted-slate-glass + warm-parchment-rim panel. Anime-leaning painterly base, sparse cel-edge, premium mobile RPG UI, slate-blue frosted glass, warm gold accents, subtle parchment texture. NOT photoreal, NOT oil-painted realism, NOT wireframe, NOT generic web dashboard. Use the attached `town_frontier_village:dusk` as the dimmed backdrop and `hero_dawn_priest:portrait_full` ONLY as a small-portrait stylization reference for the revealed cards.

**FIGURE GUARD**: Do not place one large character splash figure across the modal. Portraits appear ONLY as small framed bust crops inside individual unlocked catalog cards and inside the detail rail's portrait slot. Locked cards contain a silhouette, never a full rendered face.

**TEXT GUARD**: All text must be abstract unreadable glyph blocks — glyph bars, dots, short pseudo-runes, icon labels. Do not render readable Korean, English, or numbers. Locked cards may show a stylized "???" mystery glyph instead of a name.

## Composition

Layered composition:
1. **Background layer**: frontier village hub at dusk, recognizable but quiet, dimmed about 30-35% with a cool slate tint.
2. **Main modal**: centered frosted-glass / parchment hybrid panel, about 82% width and 86% height — matched to the Skills mockup frame.
3. **Compendium layout**: header, tab strip (Characters selected), filter row, left character catalog grid, right detail rail with a hero identity block instead of a VFX theater.

### Header and tabs

Top header bar:
- Left: compact title block with abstract glyph title and small subtitle line, plus a small collection-progress readout (e.g. a tiny "12 / 22" style glyph counter and a thin progress bar) to signal collection completion.
- Right: small close button.
- Below header: four tab buttons — Skills, Status, Synergy, **Characters selected**.
- Characters tab has warm gold underline / cap ornament with a small portrait-silhouette icon; other tabs cool slate.

### Filter row

Under the tab strip, a narrow game-UI filter row:
- Search field with magnifier icon and glyph placeholder.
- Compact chip/dropdown controls: class, role, faction.
- A small "show locked" toggle chip at the right.
- The row should look like game UI controls, not a browser form.

### Left catalog rail — character card grid (THE FOCUS)

Left side is a scrollable catalog rail, about 48% of modal width, showing a **3-column grid of character cards**, enough rows visible to imply a 20-22 hero roster. The grid deliberately MIXES states to sell the collection feeling:

**Unlocked / revealed cards (about 1/3 of visible cards):**
- Small framed bust portrait, painterly, warm-lit.
- Short title glyph block (hero name).
- Two small chips: class and role (e.g. tank / dps / support motifs by color+icon).
- A tiny faction crest glyph and a small "unlocked" gold check or ribbon.
- Restrained warm parchment card surface.

**Locked / mystery cards (about 2/3 of visible cards):**
- Dark slate card, lower opacity, cool tint.
- A flat featureless character **silhouette** (head-and-shoulders bust shape) filled near-black with a faint cool rim light — gender/identity unreadable.
- A small lock glyph badge and a "???" mystery glyph where the name would be.
- No class/role/faction chips revealed — at most a single greyed unknown-slot glyph.
- One or two cards can show a faint "almost there" progress ring to imply an unlock condition in progress.

The contrast between warm revealed cards and cool locked silhouettes is the emotional point: a wall of mystery with a few earned, glowing heroes.

### Right detail rail — selected character

Right side is a selected-character detail panel, about 52% of modal width. Show a **locked character selected** to demonstrate redaction (more interesting than a fully revealed one):

- Top: a large silhouette portrait slot (same near-black bust shape, faint rim light) framed like a portrait, with a lock glyph overlay.
- Title area: "???" mystery glyph instead of a real name; a small subtitle glyph reading like a locked-hint line.
- Identity chips are present but **redacted**: class / role / faction shown as greyed "?" glyph chips, not real values — the panel must NOT leak the real hero identity.
- A short "unlock condition" block: 2-3 glyph lines with a small objective icon and a progress bar, telling the player how this hero is earned.
- A faint locked narrative teaser block — blurred/obscured glyph lines, clearly sealed.
- Restrained, calm, slightly somber — a sealed page waiting to open.

Optionally, a tiny secondary strip at the bottom hints what a REVEALED detail would look like (a small revealed-state thumbnail), so the player understands the payoff.

### Secondary catalog hints

Subtly imply the other tabs without clutter — tiny skill-spark, status-knot, synergy-link motifs in the tab row. Do not show full panels for other tabs. This image is Characters-selected.

## Mood

**A collection worth completing.** The player should feel quiet pride in the few revealed heroes and curiosity / desire toward the locked silhouettes. It should read as a premium in-game hero codex — calmer than Recruit, warmer than a debug roster — where unlocking a character visibly fills a sealed slot with a real face. The locked state must feel intentional and enticing (mystery), never broken or missing-asset.

## Palette

- Background: dim dusk village, slate-blue, warm horizon.
- Modal: frosted slate glass + warm parchment rim (identical to Skills mockup).
- Revealed cards: warm parchment + soft gold, portrait warm-lit.
- Locked cards: cool slate, near-black silhouettes, faint cyan-grey rim light, muted.
- Accents: restrained warm gold for selected tab, unlocked ribbons, and progress fill.
- Lock glyphs and "?" chips: cool steel-grey, low emphasis.

## Reproduction feasibility

Easy to translate to Unity UITK (and the current unlock-surface implementation already does the structural version):
- Card grid is uniform framed cells; locked vs unlocked is a card-state variant (fill, opacity, silhouette image vs portrait, chip visibility).
- Silhouette = a single dark bust shape sprite + rim, not a per-hero asset.
- Redacted detail = the same detail layout with values swapped for "?" glyph chips and a sealed narrative block.
- Collection progress = a glyph counter + thin progress bar in the header.
- Use abstract glyph blocks instead of actual text.

## What this is NOT

- NOT a wireframe or flat web dashboard
- NOT an editor inspector with raw data rows
- NOT a character splash screen (no single large figure)
- NOT a gacha pull animation screen
- NOT readable text; locked names are "???" glyphs
- NOT a broken/missing-portrait look for locked cards — locked is an intentional silhouette aesthetic
- NOT neon sci-fi; this remains frontier-town fantasy strategy UI
```
