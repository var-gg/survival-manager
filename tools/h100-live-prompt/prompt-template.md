## Response schema (decision)

Return this object:

- `selected_action` (string): exactly one action key from the "Legal actions" list at the end of this message, copied verbatim (including any required grammar).
- `declared_intent` (object): the build you are pursuing right now —
  - `intent_id` (string): a short stable id you assign this intent (reuse the same id across decisions when continuing the same plan).
  - `track_token_ids` (string[]): the specific game elements this intent is about, named by their EXACT ids as shown in the observation (see id rules below). At least one.
  - `expected_payoff` (string): what you expect this build to do for you, in your own words.
  - `evidence_fact_ids` (string[]): the fact ids from this message's evidence section that led you to this expectation. At least one.
  - `next_acquisition_plan` (string): what you would try to pick up next toward this intent.
  - `allowed_substitutions` (string[]): acceptable alternatives if your first choice is unavailable.
  - `pivot_conditions` (string[]): what you would have to see to abandon or change this intent.
  - `confidence` (number): how sure you are, in the range (0, 1].
- `intent_ref` (string): the `intent_id` of the earlier intent you are continuing with this action; if this action starts a brand-new intent, repeat this decision's own `intent_id`.
- `build_hypotheses` (object[]): zero or more guesses about how elements relate — each `{ subject_kind, subject_id, relation, target_kind, target_id, evidence_refs (string[]), confidence (number in (0,1]) }`. Use the same id rules for the ids.

## Response schema (run report — only on the final "run report" message)

Return `{ desire_retrospective, payoff_or_near_miss, next_concept, complaints (string[]), evaluation_sentences ([{ sentence, telemetry_event_ids (string[]) }]), retry_intent }`. In `evaluation_sentences`, each sentence's `telemetry_event_ids` must be fact/decision ids you actually saw in the observations during this run.

## Id rules (so your reasoning connects to what you saw)

- Whenever a field asks for ids (`track_token_ids`, hypothesis ids, `telemetry_event_ids`, `evidence_fact_ids`), copy the id EXACTLY as printed in the observation. Do not paraphrase, translate, re-case, or invent ids.
- An id may be written bare (e.g. `emberguard`) or as `kind:id` where kind is one of: `archetype, skill, item, item_instance, affix, augment, passive, synergy, status, tag, team_rule`.
- `evidence_fact_ids` may only use fact ids printed in this message's evidence section.
- `confidence` is always a number greater than 0 and at most 1.

## How to fill this in

- Fill every field with your own genuine reasoning — what you want, why, from what evidence, what comes next, what you would accept instead, and what would make you change course. Blank or placeholder values are rejected by the parser.
- These are your decisions as a new player forming your own read of the game. Decide from the observation; do not wait for a recommended answer, because none is given.
