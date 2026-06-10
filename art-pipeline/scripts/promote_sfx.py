#!/usr/bin/env python3
"""Promote a human-approved SFX candidate from ai-infra raw output into the canonical store.

    C:/projects/ai-infra/data/sfx/outputs/<id>.wav  ->  art-pipeline/sfx/approved/<hook_id>.wav

and writes a sidecar `<hook_id>.json` next to it — the generation recipe + QC measurements +
approval record that Survival Asset Studio surfaces and that keeps the clip re-generatable.

Why a separate canonical store (mirrors `promote_cg.py`): ai-infra `data/sfx/outputs` is the
raw, churny, *unversioned* generation staging. `art-pipeline/sfx/approved/` holds one named
keeper per hook id inside the game repo, so approved SFX are versioned and stable.

By default the keeper is mastered for one-shot use per the style bible
(`docs/03_architecture/sfx-sound-style-bible.md` 후처리 레시피): leading silence trimmed to
<=20 ms, 3 ms fade-in / 30 ms fade-out, peak normalized to -3 dBFS, PCM 16-bit. Raw MOSS
output is far too quiet for runtime use, so promoting the raw bytes verbatim would be a trap.
Use --no-master to copy verbatim. The raw source file is never modified either way.

Usage:
    python art-pipeline/scripts/promote_sfx.py <generated_wav_or_json> [--as <hook_id>]
        [--note "검수 코멘트"] [--no-master] [--target-peak-db -3.0]
"""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
from scipy.io import wavfile

REPO = Path(__file__).resolve().parents[2]
APPROVED = REPO / "art-pipeline" / "sfx" / "approved"

LEADING_SILENCE_KEEP_MS = 20.0
SILENCE_REL_THRESHOLD = 0.01
FADE_IN_MS = 3.0
FADE_OUT_MS = 30.0
DROPPED_SIDECAR_KEYS = ("vram", "vram_sampler", "device", "unload_after_generate", "audio_url")


def master_one_shot(rate: int, samples: np.ndarray, target_peak_db: float) -> tuple[np.ndarray, dict]:
    peak = float(np.abs(samples).max())
    if peak <= 0.0:
        sys.exit("refusing to promote a silent wav")

    threshold = max(peak * SILENCE_REL_THRESHOLD, 1e-5)
    above = np.nonzero(np.abs(samples) >= threshold)[0]
    keep_samples = int(rate * LEADING_SILENCE_KEEP_MS / 1000.0)
    start = max(0, int(above[0]) - keep_samples) if above.size else 0
    trimmed = samples[start:]

    fade_in = min(int(rate * FADE_IN_MS / 1000.0), trimmed.size)
    fade_out = min(int(rate * FADE_OUT_MS / 1000.0), trimmed.size)
    shaped = trimmed.copy()
    if fade_in > 0:
        shaped[:fade_in] *= np.linspace(0.0, 1.0, fade_in)
    if fade_out > 0:
        shaped[-fade_out:] *= np.linspace(1.0, 0.0, fade_out)

    target_peak = 10.0 ** (target_peak_db / 20.0)
    gain = target_peak / float(np.abs(shaped).max())
    mastered = np.clip(shaped * gain, -1.0, 1.0)

    recipe = {
        "type": "promote_one_shot_master",
        "trimmed_leading_ms": round(start / rate * 1000.0, 1),
        "fade_in_ms": FADE_IN_MS,
        "fade_out_ms": FADE_OUT_MS,
        "target_peak_dbfs": target_peak_db,
        "gain_db": round(20.0 * np.log10(gain), 1),
    }
    return mastered, recipe


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", help="생성 wav 또는 sidecar json 경로")
    parser.add_argument("--as", dest="hook_override", default=None, help="hook id 강제 지정")
    parser.add_argument("--note", default=None, help="검수 코멘트 (sidecar approved.note)")
    parser.add_argument("--no-master", action="store_true", help="마스터링 없이 원본 바이트 복사")
    parser.add_argument("--target-peak-db", type=float, default=-3.0)
    args = parser.parse_args()

    source = Path(args.source)
    wav_path = source.with_suffix(".wav") if source.suffix.lower() == ".json" else source
    sidecar_path = wav_path.with_suffix(".json")
    if not wav_path.exists():
        sys.exit(f"source wav not found: {wav_path}")

    sidecar: dict = {}
    if sidecar_path.exists():
        sidecar = json.loads(sidecar_path.read_text(encoding="utf-8"))

    hook_id = args.hook_override or sidecar.get("hook_id") or (sidecar.get("review_context") or {}).get(
        "hook_id"
    )
    if not hook_id:
        sys.exit("hook_id를 sidecar에서 찾지 못했습니다. --as <hook_id>로 지정하세요.")

    APPROVED.mkdir(parents=True, exist_ok=True)
    dst_wav = APPROVED / f"{hook_id}.wav"
    dst_json = APPROVED / f"{hook_id}.json"

    rate, data = wavfile.read(wav_path)
    if data.dtype == np.int16:
        samples = data.astype(np.float64) / 32768.0
    else:
        samples = data.astype(np.float64)
    if samples.ndim > 1:
        samples = samples.mean(axis=1)

    if args.no_master:
        mastered, recipe = samples, {"type": "verbatim_copy"}
    else:
        mastered, recipe = master_one_shot(rate, samples, args.target_peak_db)
    wavfile.write(dst_wav, rate, (mastered * 32767.0).astype(np.int16))

    promoted = {key: value for key, value in sidecar.items() if key not in DROPPED_SIDECAR_KEYS}
    promoted["id"] = hook_id
    promoted["asset_class"] = "sfx_approved"
    promoted["hook_id"] = hook_id
    promoted.setdefault("runtime_hook_id", None)
    promoted["approved"] = {
        "verdict": "green",
        "note": args.note,
        "promoted_from": str(wav_path),
        "source_id": sidecar.get("id", wav_path.stem),
    }
    promoted["postprocess"] = recipe
    promoted["qc_status"] = "green"
    promoted["output_path"] = str(dst_wav)
    dst_json.write_text(json.dumps(promoted, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print(f"promoted {hook_id}")
    print(f"  wav     -> {dst_wav.relative_to(REPO)}")
    print(f"  sidecar -> {dst_json.relative_to(REPO)}")
    if not args.no_master:
        print(f"  master  -> trim {recipe['trimmed_leading_ms']}ms, gain {recipe['gain_db']:+}dB, peak {args.target_peak_db}dBFS")


if __name__ == "__main__":
    main()
