#!/usr/bin/env python3
"""Automated SFX QC gate — measure a generated WAV and verdict it before human review.

Implements the `expected_audio_profile` checks specified in
`docs/03_architecture/sfx-hook-id-contract.md` (생성 주문서 행) and
`docs/03_architecture/sfx-sound-style-bible.md`, which until now existed only on paper:

    duration_s (min/max), attack_ms (max), low_band_ratio (max), leading_silence_ms (max)

plus global sanity checks (decode failure, near-silence, clipping). The verdict is written
into the generation sidecar JSON (`qc_status` red/yellow/green + measured `audio_qc` block),
which Survival Asset Studio already reads — no app change needed for results to surface.

The profile for a file resolves in this order:
  1. `expected_audio_profile` embedded in the sidecar
  2. `profile_key` matched against manifest rows under --manifest-dir

Raw MOSS output can be extremely quiet (observed -45 dBFS peaks). Quiet is a *yellow*
("listen via reviewnorm copy"), not a red — near-silence is the red.

Usage:
    python art-pipeline/scripts/qc_sfx.py <wav|json|dir>... [--manifest-dir art-pipeline/sfx/manifest]
        [--write] [--json]
"""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
from scipy.io import wavfile

REPO = Path(__file__).resolve().parents[2]
DEFAULT_MANIFEST_DIR = REPO / "art-pipeline" / "sfx" / "manifest"

SILENCE_REL_THRESHOLD = 0.01  # -40 dB relative to peak
NEAR_SILENCE_PEAK = 0.005
QUIET_PEAK = 0.03
CLIPPING_LEVEL = 0.999
CLIPPING_MAX_SAMPLES = 8


def load_samples(path: Path) -> tuple[int, np.ndarray]:
    rate, data = wavfile.read(path)
    if data.dtype == np.int16:
        samples = data.astype(np.float64) / 32768.0
    elif data.dtype == np.int32:
        samples = data.astype(np.float64) / 2147483648.0
    elif data.dtype == np.uint8:
        samples = (data.astype(np.float64) - 128.0) / 128.0
    else:
        samples = data.astype(np.float64)
    if samples.ndim > 1:
        samples = samples.mean(axis=1)
    return rate, samples


def measure(rate: int, samples: np.ndarray) -> dict:
    abs_samples = np.abs(samples)
    peak = float(abs_samples.max()) if samples.size else 0.0
    rms = float(np.sqrt(np.mean(np.square(samples)))) if samples.size else 0.0
    duration_s = samples.size / rate if rate else 0.0
    clipping_count = int(np.count_nonzero(abs_samples >= CLIPPING_LEVEL))

    threshold = max(peak * SILENCE_REL_THRESHOLD, 1e-5)
    above = np.nonzero(abs_samples >= threshold)[0]
    if above.size:
        leading_silence_ms = above[0] / rate * 1000.0
        trailing_silence_ms = (samples.size - 1 - above[-1]) / rate * 1000.0
    else:
        leading_silence_ms = duration_s * 1000.0
        trailing_silence_ms = duration_s * 1000.0

    # attack: onset(10% peak 최초 도달)에서 90% peak 최초 도달까지
    attack_ms = None
    if peak > 0.0:
        onset_idx = np.argmax(abs_samples >= peak * 0.1)
        peak_idx_candidates = np.nonzero(abs_samples >= peak * 0.9)[0]
        if peak_idx_candidates.size:
            attack_ms = max(0.0, (peak_idx_candidates[0] - onset_idx) / rate * 1000.0)

    band_ratios = spectral_bands(rate, samples)

    return {
        "sample_rate": rate,
        "duration_seconds": round(duration_s, 4),
        "peak": round(peak, 6),
        "rms": round(rms, 6),
        "clipping_count": clipping_count,
        "leading_silence_ms": round(leading_silence_ms, 1),
        "trailing_silence_ms": round(trailing_silence_ms, 1),
        "attack_ms": round(attack_ms, 1) if attack_ms is not None else None,
        "low_band_ratio": band_ratios["low"],
        "band_ratios": band_ratios,
    }


def spectral_bands(rate: int, samples: np.ndarray) -> dict:
    """4밴드 에너지 비율. low(<180Hz)가 계약의 low_band_ratio."""
    if samples.size < 16:
        return {"low": 0.0, "body": 0.0, "presence": 0.0, "air": 0.0}
    spectrum = np.abs(np.fft.rfft(samples)) ** 2
    freqs = np.fft.rfftfreq(samples.size, d=1.0 / rate)
    total = float(spectrum.sum())
    if total <= 0.0:
        return {"low": 0.0, "body": 0.0, "presence": 0.0, "air": 0.0}

    def ratio(low_hz: float, high_hz: float) -> float:
        mask = (freqs >= low_hz) & (freqs < high_hz)
        return round(float(spectrum[mask].sum()) / total, 4)

    return {
        "low": ratio(0, 180),
        "body": ratio(180, 2000),
        "presence": ratio(2000, 5000),
        "air": ratio(5000, rate / 2),
    }


def load_profile(sidecar: dict | None, manifests: list[dict]) -> tuple[dict | None, str | None]:
    if sidecar:
        profile = sidecar.get("expected_audio_profile")
        if isinstance(profile, dict) and profile:
            return profile, "sidecar"
        profile_key = sidecar.get("profile_key") or (sidecar.get("review_context") or {}).get(
            "profile_key"
        )
        if profile_key:
            for manifest in manifests:
                for item in manifest.get("items", []):
                    if item.get("profile_key") == profile_key:
                        return item.get("expected_audio_profile"), f"manifest:{profile_key}"
    return None, None


def verdict(metrics: dict, profile: dict | None) -> tuple[str, float, list[dict]]:
    checks: list[dict] = []

    def check(name: str, status: str, detail: str) -> None:
        checks.append({"check": name, "status": status, "detail": detail})

    # 글로벌 red 조건
    if metrics["duration_seconds"] <= 0.05:
        check("not_empty", "red", f"duration {metrics['duration_seconds']}s")
    elif metrics["peak"] < NEAR_SILENCE_PEAK and metrics["rms"] < 1e-4:
        check("not_silent", "red", f"peak {metrics['peak']}, rms {metrics['rms']}")
    if metrics["clipping_count"] > CLIPPING_MAX_SAMPLES:
        check("no_clipping", "red", f"{metrics['clipping_count']} samples >= {CLIPPING_LEVEL}")

    # 글로벌 yellow 조건
    if not any(c["status"] == "red" for c in checks) and metrics["peak"] < QUIET_PEAK:
        check(
            "loudness",
            "yellow",
            f"raw very quiet (peak {metrics['peak']}); 검수는 reviewnorm 사본으로",
        )

    # profile 조건
    if profile:
        dur = profile.get("duration_s") or {}
        if isinstance(dur, dict) and metrics["duration_seconds"] > 0.05:
            lo, hi = dur.get("min"), dur.get("max")
            actual = effective_duration_s(metrics)
            if hi is not None and actual > hi * 2:
                check("duration", "red", f"effective {actual}s > 2x max {hi}s")
            elif (hi is not None and actual > hi) or (lo is not None and actual < lo):
                check("duration", "yellow", f"effective {actual}s outside [{lo}, {hi}]")
            else:
                check("duration", "green", f"effective {actual}s in [{lo}, {hi}]")
        attack_max = (profile.get("attack_ms") or {}).get("max")
        if attack_max is not None and metrics["attack_ms"] is not None:
            status = "green" if metrics["attack_ms"] <= attack_max else "yellow"
            check("attack", status, f"{metrics['attack_ms']}ms vs max {attack_max}ms")
        low_max = (profile.get("low_band_ratio") or {}).get("max")
        if low_max is not None:
            status = "green" if metrics["low_band_ratio"] <= low_max else "yellow"
            check("low_band", status, f"{metrics['low_band_ratio']} vs max {low_max}")
        silence_max = (profile.get("leading_silence_ms") or {}).get("max")
        if silence_max is not None:
            status = "green" if metrics["leading_silence_ms"] <= silence_max else "yellow"
            check("leading_silence", status, f"{metrics['leading_silence_ms']}ms vs max {silence_max}ms")

    if any(c["status"] == "red" for c in checks):
        status = "red"
    elif any(c["status"] == "yellow" for c in checks):
        status = "yellow"
    else:
        status = "green"
    score = {"red": 0.0}.get(status, round(max(0.0, 1.0 - 0.15 * sum(1 for c in checks if c["status"] == "yellow")), 2))
    return status, score, checks


def effective_duration_s(metrics: dict) -> float:
    """생성 길이는 요청 seconds로 패딩되므로 trailing silence를 뺀 실효 길이로 판정한다."""
    total_ms = metrics["duration_seconds"] * 1000.0
    effective_ms = max(0.0, total_ms - metrics["trailing_silence_ms"])
    return round(effective_ms / 1000.0, 3)


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
    seen: set[Path] = set()
    unique = []
    for wav in wavs:
        if wav not in seen:
            seen.add(wav)
            unique.append(wav)
    return unique


def load_manifests(manifest_dir: Path) -> list[dict]:
    manifests = []
    if manifest_dir.is_dir():
        for path in sorted(manifest_dir.glob("*.json")):
            try:
                manifests.append(json.loads(path.read_text(encoding="utf-8")))
            except (OSError, json.JSONDecodeError):
                continue
    return manifests


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paths", nargs="+", help="wav/json 파일 또는 디렉터리")
    parser.add_argument("--manifest-dir", default=str(DEFAULT_MANIFEST_DIR))
    parser.add_argument("--write", action="store_true", help="sidecar JSON에 qc 결과 기록")
    parser.add_argument("--json", action="store_true", help="결과를 JSON으로 출력")
    args = parser.parse_args()

    manifests = load_manifests(Path(args.manifest_dir))
    wavs = collect_wavs(args.paths)
    if not wavs:
        sys.exit("no wav files found")

    results = []
    for wav in wavs:
        sidecar_path = wav.with_suffix(".json")
        sidecar = None
        if sidecar_path.exists():
            try:
                sidecar = json.loads(sidecar_path.read_text(encoding="utf-8"))
            except json.JSONDecodeError:
                sidecar = None

        try:
            rate, samples = load_samples(wav)
            metrics = measure(rate, samples)
        except (OSError, ValueError) as error:
            results.append({"file": wav.name, "qc_status": "red", "error": str(error)})
            continue

        profile, profile_source = load_profile(sidecar, manifests)
        status, score, checks = verdict(metrics, profile)
        results.append(
            {
                "file": wav.name,
                "qc_status": status,
                "qc_score": score,
                "profile_source": profile_source,
                "metrics": metrics,
                "checks": checks,
            }
        )

        if args.write and sidecar is not None:
            existing_audio_qc = sidecar.get("audio_qc")
            merged_audio_qc = dict(existing_audio_qc) if isinstance(existing_audio_qc, dict) else {}
            merged_audio_qc.update(metrics)
            sidecar["audio_qc"] = merged_audio_qc
            sidecar["qc_status"] = status
            sidecar["qc_score"] = score
            sidecar["qc_checks"] = checks
            if profile_source:
                sidecar["qc_profile_source"] = profile_source
            sidecar_path.write_text(
                json.dumps(sidecar, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
            )

    if args.json:
        print(json.dumps(results, ensure_ascii=False, indent=2))
    else:
        for row in results:
            metrics = row.get("metrics") or {}
            flags = ", ".join(
                f"{c['check']}:{c['status']}" for c in row.get("checks", []) if c["status"] != "green"
            )
            print(
                f"{row['qc_status']:6} {row['file']:60} "
                f"peak={metrics.get('peak', '?'):<9} eff_dur={effective_duration_s(metrics) if metrics else '?':<6} "
                f"{flags}"
            )
        counts: dict[str, int] = {}
        for row in results:
            counts[row["qc_status"]] = counts.get(row["qc_status"], 0) + 1
        print(f"-- total {len(results)}: " + ", ".join(f"{k}={v}" for k, v in sorted(counts.items())))


if __name__ == "__main__":
    main()
