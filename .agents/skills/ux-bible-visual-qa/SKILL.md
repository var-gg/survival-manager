---
name: ux-bible-visual-qa
description: survival-manager 전용 UX Bible 시안 대비 visual QA 스킬. PlayMode surface screenshot을 Screenshots/mockups/ui_ux_bible_*_v0.png canonical reference와 매칭해 contact sheet, reference_map, visual_verdict를 만들고 red/yellow/green 판정 후 red가 남으면 수정 루프를 반복할 때 사용한다.
---

# ux-bible-visual-qa

Use this skill when asked to check whether implemented UI surfaces match the UX Bible mockups, to prepare user handoff screenshots, or to run a "Codex/Claude eye QA" before the user reviews.

## Canonical Paths

- References: `Screenshots/mockups/ui_ux_bible_*_v0.png`
- Current evidence: `Logs/ux-bible-visual-qa/<yyyyMMdd-HHmmss>-<shortsha>/`
- Witness fallback evidence: `Logs/ux-bible-witness/<yyyyMMdd-HHmmss>-<shortsha>/`
- Generated files in an evidence packet:
  - `reference_map.json`
  - `comparison_contact_sheet.png`
  - `comparison_contact_sheet.md`
  - `visual_verdict.json`
  - current screenshots such as `town_hub.png`, `character_sheet.png`, `tactical_setup.png`

`Logs/**` evidence is generated output and is normally not committed.

## Default Reference Map

| Surface | Reference | Current screenshot |
| --- | --- | --- |
| Town Service Hub | `ui_ux_bible_town_service_hub_v0.png` | `town_hub.png` |
| Character Sheet | `ui_ux_bible_character_sheet_class_detail_v0.png` | `character_sheet.png` |
| Tactical Setup | `ui_ux_bible_squad_builder_v0.png` | `tactical_setup.png` |
| Inventory Compare | `ui_ux_bible_inventory_equipment_compare_v0.png` | `inventory_compare.png` |
| Recruit Detail | `ui_ux_bible_recruit_candidate_choice_v0.png` | `recruit_detail.png` |
| Atlas Enemy Intel | `ui_ux_bible_atlas_overworld_map_v0.png` | `atlas_enemy_intel.png` |
| Battle HUD Shell | `ui_ux_bible_battle_stage_hud_v0.png` | `battle_authored.png` |
| Reward Result | `ui_ux_bible_reward_result_v0.png` | `reward_result.png` |

If a surface intentionally uses an alias, record it in `reference_map.json` rather than silently comparing against the wrong mockup.

## Workflow

1. Run or reuse a fresh PlayMode witness that captures the target surfaces.

   ```powershell
   pwsh -File tools/unity-bridge.ps1 compile
   pwsh -File tools/unity-bridge.ps1 prepare-playable
   pwsh -File tools/unity-bridge.ps1 test-play -TestFilter "SM.Tests.PlayMode.UxBiblePlayModeWitnessTests"
   ```

2. Find the newest evidence directory under `Logs/ux-bible-visual-qa/`. If the run only produced `Logs/ux-bible-witness/`, use that as the input and write visual QA output into the same packet or a new `ux-bible-visual-qa` packet.

3. Build the reference/current contact sheet.

   ```powershell
   python .agents/skills/ux-bible-visual-qa/scripts/build_contact_sheet.py --repo-root . --evidence-dir Logs/ux-bible-visual-qa/<packet> --strict
   ```

4. Open `comparison_contact_sheet.png` and directly inspect the side-by-side result. Codex should use `view_image` when available; Claude should use its available image view path. Do not claim visual green from query-only witness tests.

5. Write `visual_verdict.json` in the evidence directory. Required fields:
   - `overall`: `green`, `yellow_no_ui_red`, or `red`
   - `redCount`
   - `reviewedScreens`
   - `green`
   - `yellow`
   - `red`
   - `notes`

6. If any red remains, fix the UI and repeat capture -> contact sheet -> visual verdict. User handoff is allowed only when red is zero.

## Severity

- Red: target modal/surface not visible, `No translation found`, raw `content.*` or `ui.*`, debug hash/smoke text in production UI, severe text clipping/overlap, or first-read IA/chrome collapse versus the mockup.
- Yellow: missing final illustration/icon art, minor spacing, atlas/battle world art parity beyond UI-only scope, or polish that does not block user QA.
- Green: large IA, dark/gold material language, chrome hierarchy, information grouping, and readable text match the reference closely enough for user review.

## Guardrails

- The UX Bible mockups are the comparison baseline. Do not generate new reference images unless the user explicitly asks for a new mockup pass.
- Separate "automated witness green" from "visual QA green"; both must be recorded when claiming handoff readiness.
- Do not commit `Logs/**` evidence unless explicitly requested.
- If this uncovers a design decision or long-lived task status, record that in Pindoc. Keep this skill as the operational workflow, not the product SoT.
- When touching `.agents/skills/**`, update `.agents/skills/README.md` and validate the skill.
