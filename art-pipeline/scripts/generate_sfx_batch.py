#!/usr/bin/env python3
"""Drive a manifest of SFX generation orders through the ai-infra MOSS server.

Reads a 생성 주문서 manifest (`art-pipeline/sfx/manifest/*.json`), POSTs each item × candidate
seed to the MOSS server sequentially (the server enforces a process-wide generation lock), then
merges the manifest's review metadata (`runtime_hook_id`, `variant_key`, `review_context`,
`expected_audio_profile`, ...) into the server-written sidecar so Asset Studio and `qc_sfx.py`
can classify the output without extra wiring.

The server must already be running in keep-loaded mode for batches:
    pwsh -File C:\\projects\\ai-infra\\scripts\\serve-sfx.ps1 -KeepLoaded -IdleUnloadSeconds 600

Retries on 423 (busy) and 503 (VRAM preflight) with backoff; aborts the batch after repeated
preflight failures so a saturated GPU fails loudly instead of looping.

Usage:
    python art-pipeline/scripts/generate_sfx_batch.py --manifest art-pipeline/sfx/manifest/combat_common.json
        [--items combat.impact.flesh.light,...] [--seeds 6201,7411] [--steps 50] [--cfg 4.0]
        [--batch-label c1] [--server http://127.0.0.1:8003] [--dry-run]
"""
import argparse
import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
DEFAULT_SERVER = "http://127.0.0.1:8003"
RETRYABLE_STATUS = {423, 503}
MAX_ATTEMPTS = 4
RETRY_WAIT_S = 20


def post_generate(server: str, payload: dict, timeout_s: int = 600) -> dict:
    request = urllib.request.Request(
        f"{server}/generate",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=timeout_s) as response:
        return json.loads(response.read().decode("utf-8"))


def output_name_for(hook_id: str, seed: int, batch_label: str) -> str:
    tail = hook_id.removeprefix("sfx.").replace(".", "-").replace("_", "-")
    return f"sm-{batch_label}-{tail}-s{seed}"


def merge_sidecar(output_path: Path, item: dict, batch_label: str, settings_used: dict) -> None:
    sidecar_path = output_path.with_suffix(".json")
    if not sidecar_path.exists():
        print(f"  warn: sidecar not found at {sidecar_path}")
        return
    sidecar = json.loads(sidecar_path.read_text(encoding="utf-8"))
    sidecar["runtime_hook_id"] = item["runtime_hook_id"]
    sidecar["hook_id"] = item["hook_id"]
    sidecar["variant_key"] = item["variant_key"]
    sidecar["profile_key"] = item.get("profile_key", item["variant_key"])
    sidecar["category"] = item.get("category")
    sidecar["trigger"] = item.get("trigger")
    sidecar["expected_audio_profile"] = item.get("expected_audio_profile", {})
    review_context = dict(item.get("review_context", {}))
    review_context.setdefault("hook_id", item["hook_id"])
    review_context.setdefault("runtime_hook_id", item["runtime_hook_id"])
    review_context.setdefault("variant_key", item["variant_key"])
    review_context.setdefault("profile_key", sidecar["profile_key"])
    sidecar["review_context"] = review_context
    sidecar["review_batch"] = batch_label
    sidecar["generation_contract"] = {
        "prompt_style": "caption-first",
        "negative_prompt": "separate",
        "manifest": str(settings_used["manifest"]),
        "num_inference_steps": settings_used["num_inference_steps"],
        "cfg_scale": settings_used["cfg_scale"],
        "seed": settings_used["seed"],
    }
    sidecar_path.write_text(json.dumps(sidecar, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--items", default=None, help="variant_key 콤마 목록 (생략 시 전체)")
    parser.add_argument("--seeds", default=None, help="candidate seed 콤마 목록 (manifest 기본 덮어쓰기)")
    parser.add_argument("--steps", type=int, default=None)
    parser.add_argument("--cfg", type=float, default=None)
    parser.add_argument("--batch-label", default="c1")
    parser.add_argument("--server", default=DEFAULT_SERVER)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    manifest_path = Path(args.manifest)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    defaults = manifest.get("generation_defaults", {})
    steps = args.steps if args.steps is not None else defaults.get("num_inference_steps", 100)
    cfg = args.cfg if args.cfg is not None else defaults.get("cfg_scale", 4.0)
    seeds = (
        [int(s) for s in args.seeds.split(",")]
        if args.seeds
        else list(defaults.get("candidate_seeds", [6201]))
    )

    items = manifest.get("items", [])
    if args.items:
        wanted = {key.strip() for key in args.items.split(",")}
        items = [item for item in items if item["variant_key"] in wanted]
        missing = wanted - {item["variant_key"] for item in items}
        if missing:
            sys.exit(f"unknown variant_key: {sorted(missing)}")
    if not items:
        sys.exit("no manifest items selected")

    total = len(items) * len(seeds)
    print(f"batch {args.batch_label}: {len(items)} items x {len(seeds)} seeds = {total} clips "
          f"(steps={steps}, cfg={cfg})")
    if args.dry_run:
        for item in items:
            for seed in seeds:
                print(f"  {output_name_for(item['hook_id'], seed, args.batch_label)}  <- {item['prompt'][:70]}...")
        return

    done = 0
    failures: list[str] = []
    for item in items:
        for seed in seeds:
            name = output_name_for(item["hook_id"], seed, args.batch_label)
            payload = {
                "prompt": item["prompt"],
                "negative_prompt": item.get("negative_prompt", ""),
                "seconds": item.get("seconds", 1.0),
                "output_name": name,
                "num_inference_steps": steps,
                "cfg_scale": cfg,
                "seed": seed,
                "device": "cuda",
                "unload_after_generate": False,
            }
            response = None
            for attempt in range(1, MAX_ATTEMPTS + 1):
                try:
                    started = time.perf_counter()
                    response = post_generate(args.server, payload)
                    break
                except urllib.error.HTTPError as error:
                    if error.code in RETRYABLE_STATUS and attempt < MAX_ATTEMPTS:
                        print(f"  {name}: HTTP {error.code}, retry {attempt}/{MAX_ATTEMPTS - 1} in {RETRY_WAIT_S}s")
                        time.sleep(RETRY_WAIT_S)
                        continue
                    failures.append(f"{name}: HTTP {error.code}")
                    response = None
                    break
                except (urllib.error.URLError, TimeoutError) as error:
                    failures.append(f"{name}: {error}")
                    response = None
                    break
            if response is None:
                if len(failures) >= 3:
                    sys.exit(f"aborting after repeated failures: {failures}")
                continue

            output = response.get("output", {})
            output_path = Path(output.get("output_path", ""))
            wall = time.perf_counter() - started
            merge_sidecar(
                output_path,
                item,
                args.batch_label,
                {
                    "manifest": manifest_path.name,
                    "num_inference_steps": steps,
                    "cfg_scale": cfg,
                    "seed": seed,
                },
            )
            done += 1
            print(
                f"  [{done}/{total}] {name}  inference={output.get('inference_elapsed_seconds')}s "
                f"wall={wall:.1f}s"
            )

    print(f"done: {done}/{total} clips" + (f", failures: {failures}" if failures else ""))


if __name__ == "__main__":
    main()
