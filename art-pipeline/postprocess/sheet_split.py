"""
art-pipeline/postprocess/sheet_split.py

Split a chroma-key transparent sprite sheet into individual cell PNGs
based on alpha channel connected components.

Input:
    - A transparent PNG (chroma magenta already applied via chroma_key.py).
    - Cell layout (rows × cols).
    - Emotion ID list ordered by reading order (row-major, top-left to bottom-right).

Output:
    - One PNG per cell, named by emotion id.

Usage:
    python sheet_split.py <transparent_sheet.png> --rows 2 --cols 4 \\
        --emotions default,smile,serious,shock,anger,sad,cry,quiet \\
        --out-dir art-pipeline/output/hero_dawn_priest/ \\
        --prefix portrait_face

Algorithm:
    1. Load RGBA sheet, get alpha channel.
    2. Threshold alpha > 0 -> visible pixels.
    3. scipy.ndimage.label -> connected components (each cell = one big region).
    4. Filter components by size (drop noise).
    5. Sort components by row (y center) then col (x center) using k-means-like
       row binning -> read-order list.
    6. For each component, crop a tight bbox + small padding -> save as PNG.

Falls back to a fixed-grid hardcoded crop if component count != expected.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import label


def find_cells_by_chroma(
    arr: np.ndarray,
    chroma: tuple[int, int, int] = (255, 0, 255),
    tolerance: float = 70.0,
    gap_threshold: float = 0.80,
) -> list[tuple[int, int, int, int]]:
    """Detect grid cells by finding horizontal/vertical chroma-filled rows/cols.

    Most reliable strategy for sprite sheets — chroma magenta gaps are
    consistently full-row/full-col chroma, so finding them is deterministic.
    The cells are the regions BETWEEN chroma gap rows/cols.

    Returns bboxes in row-major reading order (top-to-bottom, left-to-right).
    """
    rgb = arr[..., :3].astype(np.int32)
    chroma_arr = np.array(chroma, dtype=np.int32)
    dist = np.sqrt(((rgb - chroma_arr) ** 2).sum(axis=2))
    chroma_mask = dist < tolerance
    h, w = chroma_mask.shape

    row_chroma_ratio = chroma_mask.mean(axis=1)
    col_chroma_ratio = chroma_mask.mean(axis=0)

    def find_runs(mask: np.ndarray) -> list[tuple[int, int]]:
        runs: list[tuple[int, int]] = []
        in_run = False
        start = 0
        for i, v in enumerate(mask):
            if v and not in_run:
                start = i
                in_run = True
            elif not v and in_run:
                runs.append((start, i))
                in_run = False
        if in_run:
            runs.append((start, len(mask)))
        return runs

    row_gaps = find_runs(row_chroma_ratio >= gap_threshold)
    col_gaps = find_runs(col_chroma_ratio >= gap_threshold)

    def gaps_to_cell_ranges(gaps: list[tuple[int, int]], total: int) -> list[tuple[int, int]]:
        ranges = []
        prev = 0
        for s, e in gaps:
            if s > prev:
                ranges.append((prev, s))
            prev = e
        if prev < total:
            ranges.append((prev, total))
        return ranges

    col_ranges = gaps_to_cell_ranges(col_gaps, w)
    row_ranges = gaps_to_cell_ranges(row_gaps, h)

    cells: list[tuple[int, int, int, int]] = []
    for y0, y1 in row_ranges:
        for x0, x1 in col_ranges:
            cells.append((x0, y0, x1, y1))
    return cells


def find_cells_by_alpha(arr: np.ndarray, min_size: int = 5000, pad: int = 16) -> list[tuple[int, int, int, int]]:
    """Return list of (x0, y0, x1, y1) bboxes of cells found via alpha components."""
    alpha = arr[..., 3]
    visible = alpha > 0
    labels, n = label(visible)

    h, w = arr.shape[:2]
    bboxes: list[tuple[int, int, int, int]] = []
    for cid in range(1, n + 1):
        region = labels == cid
        if region.sum() < min_size:
            continue
        ys, xs = np.where(region)
        x0 = max(0, int(xs.min()) - pad)
        y0 = max(0, int(ys.min()) - pad)
        x1 = min(w, int(xs.max()) + 1 + pad)
        y1 = min(h, int(ys.max()) + 1 + pad)
        bboxes.append((x0, y0, x1, y1))
    return bboxes


def sort_cells_reading_order(bboxes: list[tuple[int, int, int, int]], rows: int) -> list[tuple[int, int, int, int]]:
    """Sort bboxes into reading order (row-major, left-to-right within each row).

    Uses y-center binning into `rows` groups, then x-sort within each row.
    """
    if not bboxes:
        return []
    centers = [(((x0 + x1) / 2), ((y0 + y1) / 2)) for x0, y0, x1, y1 in bboxes]

    # Sort by y, then partition into `rows` even buckets.
    indexed = sorted(enumerate(bboxes), key=lambda i_b: centers[i_b[0]][1])

    per_row = max(1, len(indexed) // rows)
    rows_list: list[list[tuple[int, int, int, int]]] = []
    for r in range(rows):
        if r < rows - 1:
            chunk = indexed[r * per_row : (r + 1) * per_row]
        else:
            chunk = indexed[r * per_row :]
        # Sort by x within row
        chunk.sort(key=lambda i_b: centers[i_b[0]][0])
        rows_list.append([b for _, b in chunk])

    flat: list[tuple[int, int, int, int]] = []
    for row in rows_list:
        flat.extend(row)
    return flat


def hardcoded_grid_bboxes(
    canvas_w: int, canvas_h: int, rows: int, cols: int, gap: int
) -> list[tuple[int, int, int, int]]:
    """Compute exact grid cell bboxes assuming no margin around the grid."""
    cell_w = (canvas_w - gap * (cols - 1)) // cols
    cell_h = (canvas_h - gap * (rows - 1)) // rows
    bboxes = []
    for r in range(rows):
        for c in range(cols):
            x0 = c * (cell_w + gap)
            y0 = r * (cell_h + gap)
            x1 = x0 + cell_w
            y1 = y0 + cell_h
            bboxes.append((x0, y0, x1, y1))
    return bboxes


def split_sheet(
    sheet_path: Path,
    rows: int,
    cols: int,
    emotions: list[str],
    out_dir: Path,
    prefix: str = "portrait_face",
    min_size: int = 5000,
    pad: int = 16,
    fallback_gap: int = 32,
    method: str = "auto",
) -> list[Path]:
    """Split sheet into individual cells, name by emotion.

    method:
      - "auto" (default): try chroma → alpha → hardcoded
      - "hardcoded": skip detection, use canvas / rows / cols / gap directly.
        Use when alpha components leak between cells (e.g. a wide hand gesture
        connects two cells visually) and forced-grid is the only safe split.
    """
    img = Image.open(sheet_path).convert("RGBA")
    arr = np.array(img)
    expected = rows * cols
    if len(emotions) != expected:
        raise ValueError(f"emotions list length {len(emotions)} != rows*cols {expected}")

    if method == "hardcoded":
        sorted_bboxes = hardcoded_grid_bboxes(arr.shape[1], arr.shape[0], rows, cols, fallback_gap)
        used_method = "hardcoded grid (forced)"
    else:
        # Strategy priority:
        #   1. chroma row/col gap detection (most accurate, uses magenta gaps in raw sheet)
        #   2. alpha-based connected component label
        #   3. hardcoded grid fallback (canvas / rows / cols / gap)
        used_method = ""
        chroma_bboxes = find_cells_by_chroma(arr)
        if len(chroma_bboxes) == expected:
            sorted_bboxes = chroma_bboxes  # already in row-major reading order
            used_method = "chroma boundary"
        else:
            print(
                f"[sheet_split] chroma boundary detection found {len(chroma_bboxes)} cells "
                f"(expected {expected}); trying alpha component fallback",
                file=sys.stderr,
            )
            alpha_bboxes = find_cells_by_alpha(arr, min_size=min_size, pad=pad)
            if len(alpha_bboxes) == expected:
                sorted_bboxes = sort_cells_reading_order(alpha_bboxes, rows)
                used_method = "alpha component"
            else:
                print(
                    f"[sheet_split] alpha-based detection found {len(alpha_bboxes)} cells "
                    f"(expected {expected}); falling back to hardcoded grid",
                    file=sys.stderr,
                )
                sorted_bboxes = hardcoded_grid_bboxes(arr.shape[1], arr.shape[0], rows, cols, fallback_gap)
                used_method = "hardcoded grid"
    print(f"[sheet_split] split method: {used_method}", file=sys.stderr)

    out_dir.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []
    for bbox, emotion in zip(sorted_bboxes, emotions):
        x0, y0, x1, y1 = bbox
        cell = img.crop((x0, y0, x1, y1))
        out_path = out_dir / f"{prefix}_{emotion}.png"
        cell.save(out_path, "PNG")
        written.append(out_path)
        print(f"  {emotion:>10}: bbox=({x0},{y0},{x1},{y1}) size={x1-x0}x{y1-y0} -> {out_path.name}")
    return written


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("sheet", type=Path, help="transparent PNG (chroma already applied)")
    ap.add_argument("--rows", type=int, required=True)
    ap.add_argument("--cols", type=int, required=True)
    ap.add_argument(
        "--emotions",
        type=str,
        required=True,
        help="comma-separated emotion ids in reading order (row-major, left-to-right)",
    )
    ap.add_argument("--out-dir", type=Path, required=True)
    ap.add_argument("--prefix", type=str, default="portrait_face")
    ap.add_argument("--min-size", type=int, default=5000)
    ap.add_argument("--pad", type=int, default=16)
    ap.add_argument("--fallback-gap", type=int, default=32)
    ap.add_argument(
        "--method",
        choices=["auto", "hardcoded"],
        default="auto",
        help="auto: chroma → alpha → hardcoded fallback chain (default). "
             "hardcoded: skip detection, use forced grid math (use when alpha "
             "components leak across cells).",
    )
    args = ap.parse_args()

    emotions = [e.strip() for e in args.emotions.split(",") if e.strip()]
    written = split_sheet(
        args.sheet,
        args.rows,
        args.cols,
        emotions,
        args.out_dir,
        prefix=args.prefix,
        min_size=args.min_size,
        pad=args.pad,
        fallback_gap=args.fallback_gap,
        method=args.method,
    )
    print(f"[sheet_split] wrote {len(written)} cells")
    return 0


if __name__ == "__main__":
    sys.exit(main())
