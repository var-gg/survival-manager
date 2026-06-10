#!/usr/bin/env python3
"""Make peak-normalized listening copies of raw MOSS SFX outputs.

Raw MOSS output is often far too quiet to audition (-45 dBFS peaks observed). This writes
`<id>-reviewnorm.wav` next to the raw file, with a sidecar that carries over the generation
recipe and records the gain applied. The raw WAV is never modified — review copies are for
ears only; mastering for Unity import happens in `promote_sfx.py`.

Skips files that are already loud enough (gain below --min-gain-db) and files that already
look like review copies.

Usage:
    python art-pipeline/scripts/reviewnorm_sfx.py <wav|json|dir>... [--target-peak 0.85]
        [--min-gain-db 3]
"""
import argparse
import json
import math
import sys
from pathlib import Path

import numpy as np
from scipy.io import wavfile

SUFFIX = "-reviewnorm"


def collect_wavs(paths: list[str]) -> list[Path]:
    wavs: list[Path] = []
    for raw in paths:
        path = Path(raw)
        if path.is_dir():
            wavs.extend(sorted(path.glob("*.wav")))
        elif path.suffix.lower() == ".json":
            candidate = path.with_suffix(".wav")
            if candidate.exists():
                wavs.append(candidate)
        elif path.suffix.lower() == ".wav":
            wavs.append(path)
    return [w for w in dict.fromkeys(wavs) if SUFFIX not in w.stem]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paths", nargs="+")
    parser.add_argument("--target-peak", type=float, default=0.85)
    parser.add_argument("--min-gain-db", type=float, default=3.0)
    parser.add_argument("--force", action="store_true", help="기존 reviewnorm 사본 덮어쓰기")
    args = parser.parse_args()

    wavs = collect_wavs(args.paths)
    if not wavs:
        sys.exit("no wav files found")

    for wav in wavs:
        out_wav = wav.with_name(f"{wav.stem}{SUFFIX}.wav")
        if out_wav.exists() and not args.force:
            print(f"skip  {wav.name} (reviewnorm exists)")
            continue

        rate, data = wavfile.read(wav)
        if data.dtype == np.int16:
            samples = data.astype(np.float64) / 32768.0
        else:
            samples = data.astype(np.float64)
        peak = float(np.abs(samples).max()) if samples.size else 0.0
        if peak <= 0.0:
            print(f"skip  {wav.name} (silent)")
            continue

        gain = args.target_peak / peak
        gain_db = 20.0 * math.log10(gain)
        if gain_db < args.min_gain_db:
            print(f"skip  {wav.name} (loud enough, gain {gain_db:+.1f} dB)")
            continue

        normalized = np.clip(samples * gain, -1.0, 1.0)
        wavfile.write(out_wav, rate, (normalized * 32767.0).astype(np.int16))

        sidecar_path = wav.with_suffix(".json")
        out_sidecar = {}
        if sidecar_path.exists():
            try:
                out_sidecar = json.loads(sidecar_path.read_text(encoding="utf-8"))
            except json.JSONDecodeError:
                out_sidecar = {}
        out_sidecar["id"] = out_wav.stem
        out_sidecar["output_path"] = str(out_wav)
        out_sidecar["postprocess"] = {
            "type": "review_peak_normalize",
            "source_id": wav.stem,
            "source_path": str(wav),
            "target_peak": args.target_peak,
            "gain_db": round(gain_db, 1),
            "note": "검수용 볼륨 보정 사본. 원본 WAV는 변경하지 않음. 큰 게인은 noise floor도 같이 올림.",
        }
        audio_qc = out_sidecar.get("audio_qc")
        merged = dict(audio_qc) if isinstance(audio_qc, dict) else {}
        merged.update(
            {
                "peak": args.target_peak,
                "level_note": "review-normalized copy for listening only",
            }
        )
        out_sidecar["audio_qc"] = merged
        out_wav.with_suffix(".json").write_text(
            json.dumps(out_sidecar, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
        )
        print(f"write {out_wav.name} (gain {gain_db:+.1f} dB)")


if __name__ == "__main__":
    main()
