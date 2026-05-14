---
slug: affix_icon_sheet--default
kind: skill_icon
subject_id: affix_icon_sheet
variant: default
emotion: default
refs: []
aspect: "3:2"
output_size: "2304x1536"
chroma: "#FF00FF"
status: idea
---

# Icon Sheet: affix_icon_sheet (24 affix stat icons / 24 affix 능력치 아이콘) — sheet

Use case: Equipment Refit modal affix list (5-line strip) + Inventory item detail (affix entry per line). 24 affix stat type 각각의 작은 stat icon. SoT [`affix-pool-v1`](http://localhost:5830/p/survival-manager/wiki/) 참조 — V1 launch 24 affix subset.

## Output target (sheet_split 분할 후)

- `affix_atk.png`, `affix_crit.png`, `affix_pierce.png`, `affix_speed.png`, `affix_lifesteal.png`, `affix_armor.png`
- `affix_hp.png`, `affix_dodge.png`, `affix_block.png`, `affix_resist_phys.png`, `affix_resist_magic.png`, `affix_thorn.png`
- `affix_magic_atk.png`, `affix_cast_speed.png`, `affix_mana.png`, `affix_cooldown.png`, `affix_charge.png`, `affix_amplify.png`
- `affix_heal.png`, `affix_cleanse.png`, `affix_taunt.png`, `affix_aura.png`, `affix_link.png`, `affix_revive.png`

## Sheet 분할 호출

```powershell
python art-pipeline/postprocess/sheet_split.py `
    art-pipeline/output/affix_icon_sheet/default.png `
    --rows 4 --cols 6 `
    --emotions atk,crit,pierce,speed,lifesteal,armor,hp,dodge,block,resist_phys,resist_magic,thorn,magic_atk,cast_speed,mana,cooldown,charge,amplify,heal,cleanse,taunt,aura,link,revive `
    --out-dir art-pipeline/output/icons/affix/ `
    --prefix affix
```

```prompt
# Icon Sheet: 24 affix stat icons — for sprite atlas

A 6×4 grid sheet of stylized **stat affix icons** for a premium mobile JRPG / gacha-banner game's gear / loadout UI. Each cell contains a single small stat symbol centered on solid #FF00FF magenta background. Same painterly anime icon style as P0 sheet baselines (class / currency / trigger / posture card). NOT realistic data viz symbols — anime-game iconic.

## Sheet structure (CRITICAL)

- **2304×1536 canvas = 4 rows × 6 columns = 24 cells**
- Each cell ~360×360 active icon area (small, dense)
- **Magenta gutter (#FF00FF, ~25-40px wide)** between cells and outer edges
- Each cell pure #FF00FF background
- Icon centered with breathing room ~40-60px from cell edges (tighter than larger sheets since cells are smaller)

## Cell contents (24 affix, reading order top-left to bottom-right, row-major)

### Row 1 — Offense physical
- **atk**: Crossed sword silhouette, warm gold (different from class duelist — simpler, smaller)
- **crit**: Sword with red star burst behind tip
- **pierce**: Arrow piercing through circle (penetration symbol)
- **speed**: Forward chevrons or wing-foot (Hermes-style)
- **lifesteal**: Sword with red droplet at blade
- **armor**: Chestplate front view, steel-grey

### Row 2 — Defense
- **hp**: Heart shield (different from trigger ally_hp — simpler, single tone red)
- **dodge**: Stylized blur of crouched silhouette (evade pose)
- **block**: Small round buckler shield
- **resist_phys**: Shield + sword overlay (physical resist)
- **resist_magic**: Shield + crystal overlay (magic resist)
- **thorn**: Spiked sphere (reflect damage)

### Row 3 — Offense magic
- **magic_atk**: Crystal staff orb (different from class mystic — smaller, just orb), violet
- **cast_speed**: Spell circle with motion lines, violet
- **mana**: Blue droplet or wave
- **cooldown**: Hourglass with sand draining, warm gold
- **charge**: Lightning bolt accumulating (different from trigger burst — has progress bar)
- **amplify**: Concentric expanding rings, gold

### Row 4 — Support
- **heal**: Cross+leaf combo, warm green
- **cleanse**: Wave washing over droplet, cyan
- **taunt**: Open mouth / shout silhouette with sound waves
- **aura**: Standing figure with radiating rings, gold
- **link**: Two figures connected by chain, steel-grey
- **revive**: Phoenix wing rising from circle, warm gold + orange

## Style consistency

24 icons in same painterly anime icon style as P0 baseline sheets. Small cell size (~360×360) — icons more compact, simpler silhouette than larger sheets. Hard outline + cel-edge at hard geometric edges. Each cell readable at 48×48 final UI size.

## Color discipline

- Offense physical: warm gold / red tint
- Defense: steel-grey / cool blue tint
- Offense magic: violet / cool blue tint
- Support: warm green / gold tint
- Magenta background strict — no tint into icon

## What this is NOT

- NOT realistic stat icons (anime-painted, NOT corporate stat infographic)
- NOT 3D rendered objects
- NOT photoreal weapons
- NOT cluttered — single small symbol centered per cell
- NOT readable text on icons
- NOT chibi / mascot
- NOT magenta hue on icons
- NOT different style per row — all 24 cells in same painterly anime icon style
```
