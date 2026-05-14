#!/usr/bin/env python3
"""game-image-gen orchestrator — subject.md → ChatGPT image generation → chroma cutout.

Pipeline:
  1. parse subject page (frontmatter + prompt fence) + resolve REFs
  2. compose final prompt (style-anchor prepend/append)
  3. launch Playwright persistent Chrome context
  4. ensure logged-in ChatGPT game project (prompt user if first run)
  5. upload REFs via DataTransfer (CDP file_upload blocked on ChatGPT)
  6. inject prompt into ProseMirror textarea
  7. submit + poll for generated image (up to --timeout seconds)
  8. download via page.context.request (session cookies reused)
  9. save raw to art-pipeline/output/{subject_id}/{variant}_raw.png
 10. chroma_key cutout → art-pipeline/output/{subject_id}/{variant}.png
 11. flip frontmatter status: prompted → rendered

Usage:
    python upload_subject.py <subject.md>
    python upload_subject.py <subject.md> --dry-run    # compose only, no browser
    python upload_subject.py <subject.md> --headless   # after session saved
    python upload_subject.py <subject.md> --force      # overwrite rendered output

Adapted from vargg-webtoon comic-imagegen upload_page.py
(A:\\vargg-workspace\\vargg-webtoon\\.claude\\skills\\comic-imagegen\\scripts\\upload_page.py).
Domain changes:
  - find_pipeline_root (no series/episode tree)
  - style-anchor at pipeline_root/style/style-anchor.md (single, project-level)
  - output at pipeline_root/output/{subject_id}/{variant}.png
  - chroma_key cutout chained after raw save
  - REF mime auto-detected from extension (vargg fixed image/webp)
"""
import argparse
import base64
import json
import mimetypes
import os
import re
import subprocess
import sys
import time
from pathlib import Path
from typing import Any

import yaml
from playwright.sync_api import Page, sync_playwright

if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8")
if sys.stderr.encoding and sys.stderr.encoding.lower() != "utf-8":
    sys.stderr.reconfigure(encoding="utf-8")

sys.path.insert(0, str(Path(__file__).parent))
from assemble_prompt import (  # noqa: E402
    assemble,
    extract_anchor_for_kind,
    find_pipeline_root,
    parse_subject,
    resolve_ref_paths,
)

# -------------------------------------------------------------------------
# Config
# -------------------------------------------------------------------------

DEFAULT_CONFIG = {
    "chatgpt_project_url": "https://chatgpt.com/g/g-p-69c7aa09d1588191a8dd5bd93db389ba/project",
    "user_data_dir": "~/.claude/game-image-gen/chrome-user-data",
    "default_timeout": 240,
    "headless_default": False,
    "start_minimized_default": True,
}


def load_config(pipeline_root: Path) -> dict[str, Any]:
    cfg_path = pipeline_root / ".imagegen-config.yaml"
    cfg = dict(DEFAULT_CONFIG)
    if cfg_path.is_file():
        user_cfg = yaml.safe_load(cfg_path.read_text(encoding="utf-8")) or {}
        cfg.update(user_cfg)
    cfg["user_data_dir"] = os.path.expanduser(cfg["user_data_dir"])
    return cfg


# -------------------------------------------------------------------------
# Browser steps (vargg-derived JS)
# -------------------------------------------------------------------------

UPLOAD_REFS_JS = """
(filesData) => {
  const dt = new DataTransfer();
  filesData.forEach(({ name, data, mime }) => {
    const bytes = Uint8Array.from(atob(data), c => c.charCodeAt(0));
    const blob = new Blob([bytes], { type: mime });
    const file = new File([blob], name, { type: mime });
    dt.items.add(file);
  });
  const input = document.getElementById('upload-files');
  if (!input) throw new Error('upload-files input not found');
  input.files = dt.files;
  input.dispatchEvent(new Event('change', { bubbles: true }));
  return { attached: input.files.length, names: Array.from(input.files).map(f => f.name) };
}
"""

INJECT_PROMPT_JS = """
(text) => {
  const pm = document.getElementById('prompt-textarea');
  if (!pm) throw new Error('prompt-textarea not found');
  pm.focus();
  const sel = window.getSelection();
  const range = document.createRange();
  range.selectNodeContents(pm);
  sel.removeAllRanges();
  sel.addRange(range);
  document.execCommand('delete');
  document.execCommand('insertText', false, text);
  return { textLength: pm.textContent.length };
}
"""

SNAPSHOT_INITIAL_IMGS_JS = """
() => {
  const imgs = Array.from(document.querySelectorAll('img'));
  return imgs.map(i => i.src).filter(s => s);
}
"""

POLL_IMAGE_JS = """
(initialSrcs) => {
  const initial = new Set(initialSrcs || []);
  const imgs = Array.from(document.querySelectorAll('img'));
  const newImgs = imgs.filter(i => i.src && !initial.has(i.src));

  // REJECT: ref upload thumbnails — they have alt='업로드한 이미지' / 'attached image'
  // and are NOT actual generations. With chained REF (multiple ref images) these
  // thumbnails appear in DOM after pre-submit snapshot was taken.
  const isRefThumb = (alt) => {
    const a = (alt || '').toLowerCase();
    return alt.includes('업로드한 이미지') ||
           a.includes('attached image') ||
           a.includes('uploaded image') ||
           a.includes('__anchor') ||
           a.endsWith('anchor.png') ||
           a.endsWith('anchor.jpg') ||
           a.endsWith('anchor.jpeg') ||
           a.endsWith('anchor.webp') ||
           /^.+\\.(png|jpg|jpeg|webp)$/i.test(alt || '') ||
           alt.includes('첨부') ||
           a.includes('attachment');
  };

  // Priority 1: alt explicitly says '생성된 이미지' (most reliable)
  let img = newImgs.find(i => (i.alt || '').startsWith('생성된 이미지'));

  // Priority 2: oaiusercontent / estuary URL + size >= 512 + NOT a ref thumbnail
  if (!img) {
    img = newImgs.find(i => {
      if (!i.src) return false;
      const isContent = i.src.includes('oaiusercontent') || i.src.includes('/backend-api/estuary/content');
      if (!isContent) return false;
      if (i.naturalWidth < 512 || i.naturalHeight < 512) return false;
      if (isRefThumb(i.alt)) return false;  // explicitly reject ref thumbnails
      return true;
    });
  }

  if (!img) return null;
  if (!img.naturalWidth) return null;
  return {
    src: img.src,
    width: img.naturalWidth,
    height: img.naturalHeight,
    alt: img.alt || ''
  };
}
"""

POLL_ERROR_JS = """
() => {
  const body = document.body.innerText || '';
  const patterns = [
    '콘텐츠 정책', 'content policy',
    'unable to generate', '생성할 수 없',
    '요청이 거부', 'refused'
  ];
  for (const p of patterns) {
    if (body.includes(p)) return { pattern: p };
  }
  return null;
}
"""

DISABLE_PRO_MODE_JS = """
() => {
  const all = document.querySelectorAll('button, [role="button"]');
  for (const el of all) {
    const aria = el.getAttribute('aria-label') || '';
    const hasPro = /Pro/i.test(aria);
    const isDismiss = /제거|remove|clear/i.test(aria);
    if (hasPro && isDismiss) {
      el.click();
      return { disabled: true, aria };
    }
  }
  return { disabled: false };
}
"""


# -------------------------------------------------------------------------
# Window minimize helpers (PID-scoped, Windows-only)
# -------------------------------------------------------------------------


def get_playwright_chromium_pids() -> set[int]:
    """Return PIDs of Chromium processes spawned as children of THIS Python process.

    Critical to PID-filter so we don't accidentally minimize Claude Desktop,
    VS Code, Discord, regular Chrome — all Chromium-based apps.
    """
    try:
        import psutil
    except ImportError:
        print(
            "[imagegen] psutil not installed — install with 'pip install psutil' for PID-safe minimize",
            file=sys.stderr,
        )
        return set()
    pids: set[int] = set()
    try:
        proc = psutil.Process()
        for child in proc.children(recursive=True):
            try:
                name = child.name().lower()
                if "chrome" in name or "chromium" in name:
                    pids.add(child.pid)
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                pass
    except Exception:
        pass
    return pids


def minimize_chrome_windows(target_pids: set[int], timeout_s: float = 3.0) -> int:
    """Force-minimize Chromium windows owned by specific PIDs only."""
    if sys.platform != "win32":
        return 0
    if not target_pids:
        return 0
    try:
        import win32con
        import win32gui
        import win32process
    except ImportError:
        print(
            "[imagegen] pywin32 not installed — install with 'pip install pywin32' to auto-minimize",
            file=sys.stderr,
        )
        return 0

    minimized_count = [0]
    t0 = time.time()
    while time.time() - t0 < timeout_s:
        found_this_pass = [0]

        def _cb(hwnd: int, _: Any) -> None:
            if not win32gui.IsWindowVisible(hwnd):
                return
            try:
                cls = win32gui.GetClassName(hwnd)
            except Exception:
                return
            if cls != "Chrome_WidgetWin_1":
                return
            try:
                _tid, pid = win32process.GetWindowThreadProcessId(hwnd)
            except Exception:
                return
            if pid not in target_pids:
                return
            try:
                placement = win32gui.GetWindowPlacement(hwnd)
            except Exception:
                return
            if placement[1] == win32con.SW_SHOWMINIMIZED:
                return
            try:
                win32gui.ShowWindow(hwnd, win32con.SW_MINIMIZE)
                minimized_count[0] += 1
                found_this_pass[0] += 1
            except Exception:
                pass

        try:
            win32gui.EnumWindows(_cb, None)
        except Exception:
            pass

        if found_this_pass[0] > 0:
            time.sleep(0.2)
            try:
                win32gui.EnumWindows(_cb, None)
            except Exception:
                pass
            return minimized_count[0]
        time.sleep(0.2)
    return minimized_count[0]


# -------------------------------------------------------------------------
# Browser orchestration
# -------------------------------------------------------------------------


def ensure_logged_in(page: Page, url: str, timeout_s: float = 240) -> None:
    page.goto(url, wait_until="domcontentloaded")
    t0 = time.time()
    prompted = False
    last_renav = 0.0
    while time.time() - t0 < timeout_s:
        try:
            textarea_visible = page.locator("#prompt-textarea").is_visible(timeout=2000)
        except Exception:
            textarea_visible = False

        current_url = page.url
        on_project = "/g/g-p-" in current_url and "/project" in current_url

        if textarea_visible and on_project:
            page.wait_for_timeout(1000)
            return

        if textarea_visible and not on_project and time.time() - last_renav > 10:
            print(f"[imagegen] re-navigating to project URL (was at {current_url})", file=sys.stderr)
            try:
                page.goto(url, wait_until="domcontentloaded")
                last_renav = time.time()
            except Exception:
                pass

        if not prompted and time.time() - t0 > 5:
            print(
                f"\n[game-image-gen] ChatGPT 로그인 대기 — 브라우저 창에서 로그인 필요 (최대 {int(timeout_s)}초). 로그인 후 자동 진행.",
                file=sys.stderr,
                flush=True,
            )
            prompted = True
        try:
            page.wait_for_timeout(2000)
        except Exception:
            pass
    raise RuntimeError(f"timed out waiting for ChatGPT login ({timeout_s}s)")


def disable_pro_mode(page: Page) -> None:
    try:
        result = page.evaluate(DISABLE_PRO_MODE_JS)
    except Exception:
        return
    if isinstance(result, dict) and result.get("disabled"):
        print(f"[imagegen] Pro mode was active — disabled ({result.get('aria')!r})", file=sys.stderr)
        page.wait_for_timeout(800)


def force_new_chat(page: Page, project_url: str) -> None:
    current_url = page.url
    if "/c/" in current_url:
        print(f"[imagegen] was on chat detail ({current_url}), forcing new chat", file=sys.stderr)
        page.goto(project_url, wait_until="domcontentloaded")
        for _ in range(10):
            if page.locator("#prompt-textarea").is_visible(timeout=2000):
                break
            page.wait_for_timeout(500)

    try:
        textarea = page.locator("#prompt-textarea").first
        existing = textarea.inner_text(timeout=2000) or ""
        if existing.strip():
            print(f"[imagegen] composer had stale text ({len(existing)} chars), clearing", file=sys.stderr)
            textarea.click()
            page.keyboard.press("Control+A")
            page.keyboard.press("Delete")
    except Exception:
        pass


def upload_refs(page: Page, ref_paths: list[tuple[Path, str]]) -> None:
    files_data = []
    for p, _label in ref_paths:
        mime, _ = mimetypes.guess_type(str(p))
        if not mime:
            mime = "image/png"
        # Disambiguate filename when multiple refs share base name
        # (e.g., anchor.png from different character slugs would collide).
        # Use parent_dir as prefix to keep names unique across cells.
        unique_name = f"{p.parent.name}__{p.name}" if p.name == "anchor.png" else p.name
        files_data.append({
            "name": unique_name,
            "mime": mime,
            "data": base64.b64encode(p.read_bytes()).decode("ascii"),
        })
    total_bytes = sum(len(f["data"]) for f in files_data)
    print(
        f"[imagegen] uploading {len(files_data)} REF(s) via DataTransfer ({total_bytes // 1024} KB base64)",
        file=sys.stderr,
    )
    result = page.evaluate(UPLOAD_REFS_JS, files_data)
    attached = result.get("attached", 0) if isinstance(result, dict) else 0
    if attached != len(ref_paths):
        raise RuntimeError(f"REF attachment mismatch: expected {len(ref_paths)}, got {attached}")
    print(f"[imagegen] attached: {result.get('names')}", file=sys.stderr)
    # Wait longer for multi-ref uploads — each ref thumbnail needs to land in DOM
    # before snapshot_initial_imgs runs. 3s for 1 ref, +2s per additional ref.
    wait_ms = 3000 + max(0, len(ref_paths) - 1) * 2000
    page.wait_for_timeout(wait_ms)


def inject_prompt(page: Page, prompt: str) -> None:
    print(f"[imagegen] injecting prompt ({len(prompt)} chars)", file=sys.stderr)
    result = page.evaluate(INJECT_PROMPT_JS, prompt)
    length = result.get("textLength", 0) if isinstance(result, dict) else 0
    if length < len(prompt) * 0.9:
        raise RuntimeError(f"prompt injection truncated: {length} of {len(prompt)} chars")


def snapshot_initial_imgs(page: Page) -> list[str]:
    try:
        return page.evaluate(SNAPSHOT_INITIAL_IMGS_JS) or []
    except Exception:
        return []


def submit_prompt(page: Page) -> None:
    print("[imagegen] submitting", file=sys.stderr)
    btn = page.locator('button[aria-label="프롬프트 보내기"], button[data-testid="send-button"]').first
    btn.wait_for(state="visible", timeout=10_000)
    btn.click()
    page.wait_for_timeout(2000)


def poll_for_image(page: Page, timeout_s: int, initial_srcs: list[str]) -> dict[str, Any]:
    t0 = time.time()
    last_print = 0
    while time.time() - t0 < timeout_s:
        try:
            err = page.evaluate(POLL_ERROR_JS)
            if err:
                raise RuntimeError(f"ChatGPT error detected: {err}")
            img = page.evaluate(POLL_IMAGE_JS, initial_srcs)
        except Exception as e:
            if "Execution context" in str(e):
                page.wait_for_timeout(2000)
                continue
            raise
        if img:
            elapsed = int(time.time() - t0)
            print(
                f"[imagegen] generated in {elapsed}s: {img['width']}x{img['height']} (alt={img['alt'][:40]!r})",
                file=sys.stderr,
            )
            return img
        now = int(time.time() - t0)
        if now - last_print >= 10:
            print(f"[imagegen] waiting for image... {now}s", file=sys.stderr)
            last_print = now
        page.wait_for_timeout(2000)
    raise RuntimeError(f"image generation timeout ({timeout_s}s)")


def download_image(page: Page, img_src: str) -> bytes:
    print("[imagegen] downloading image bytes", file=sys.stderr)
    b64 = page.evaluate(
        """async (url) => {
            const r = await fetch(url);
            if (!r.ok) throw new Error(`fetch failed: ${r.status}`);
            const buf = await r.arrayBuffer();
            const bytes = new Uint8Array(buf);
            let binary = '';
            const chunk = 0x8000;
            for (let i = 0; i < bytes.length; i += chunk) {
                binary += String.fromCharCode.apply(null, bytes.slice(i, i + chunk));
            }
            return btoa(binary);
        }""",
        img_src,
    )
    return base64.b64decode(b64)


def update_status(subject_path: Path, from_status: str, to_status: str) -> None:
    text = subject_path.read_text(encoding="utf-8")
    new_text = re.sub(
        rf"^(status:\s*){re.escape(from_status)}\s*$",
        rf"\g<1>{to_status}",
        text,
        count=1,
        flags=re.MULTILINE,
    )
    if new_text == text:
        print(
            f"[imagegen] WARNING: status '{from_status}' not in frontmatter — leaving unchanged",
            file=sys.stderr,
        )
        return
    subject_path.write_text(new_text, encoding="utf-8")
    print(f"[imagegen] frontmatter status: {from_status} → {to_status}", file=sys.stderr)


# -------------------------------------------------------------------------
# Output + chroma chain
# -------------------------------------------------------------------------


def output_dest(pipeline_root: Path, fm: dict[str, Any]) -> tuple[Path, Path]:
    """Return (raw_path, final_path).

    raw_path: art-pipeline/output/{subject_id}/{variant}_raw.png (chroma input)
    final_path: art-pipeline/output/{subject_id}/{variant}.png (chroma output)
    """
    subject_id = fm["subject_id"]
    variant = fm["variant"]
    out_dir = pipeline_root / "output" / subject_id
    return (
        out_dir / f"{variant}_raw.png",
        out_dir / f"{variant}.png",
    )


def save_raw(image_bytes: bytes, raw_path: Path, force: bool) -> None:
    if raw_path.exists() and not force:
        ts = int(time.time())
        backup = raw_path.with_name(f"{raw_path.stem}.backup-{ts}{raw_path.suffix}")
        raw_path.rename(backup)
        print(f"[imagegen] existing raw backed up: {backup.name}", file=sys.stderr)
    raw_path.parent.mkdir(parents=True, exist_ok=True)
    raw_path.write_bytes(image_bytes)


def run_chroma_key(pipeline_root: Path, raw_path: Path, final_path: Path, chroma_hex: str) -> None:
    script = pipeline_root / "postprocess" / "chroma_key.py"
    if not script.is_file():
        raise FileNotFoundError(f"chroma_key.py not found at {script}")
    print(f"[imagegen] chroma cutout: {raw_path.name} -> {final_path.name}", file=sys.stderr)
    cmd = [sys.executable, str(script), str(raw_path), str(final_path), "--chroma", chroma_hex]
    subprocess.run(cmd, check=True)


# -------------------------------------------------------------------------
# Main
# -------------------------------------------------------------------------


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("subject", type=Path, help="path to subject .md")
    ap.add_argument("--dry-run", action="store_true", help="compose only, no browser")
    ap.add_argument("--headless", action="store_true", help="headless browser (after session saved)")
    ap.add_argument("--force", action="store_true", help="overwrite existing rendered output")
    ap.add_argument("--timeout", type=int, default=None, help="generation timeout seconds")
    ap.add_argument("--keep-browser-open", action="store_true", help="don't close browser after completion")
    ap.add_argument("--no-minimized", action="store_true", help="do not start browser minimized")
    ap.add_argument("--no-chroma", action="store_true", help="skip chroma cutout (raw save only)")
    args = ap.parse_args()

    subject_path = args.subject.resolve()
    if not subject_path.is_file():
        print(f"error: subject file not found: {subject_path}", file=sys.stderr)
        return 1

    try:
        fm, fence = parse_subject(subject_path)
    except ValueError as e:
        print(f"error: {e}", file=sys.stderr)
        return 2

    if fm["status"] == "rendered" and not args.force:
        print("error: subject status is already 'rendered'. Use --force to regenerate.", file=sys.stderr)
        return 3

    pipeline_root = find_pipeline_root(subject_path)
    config = load_config(pipeline_root)
    timeout = args.timeout or config["default_timeout"]

    try:
        anchor = extract_anchor_for_kind(pipeline_root, fm["kind"])
        ref_paths = resolve_ref_paths(pipeline_root, fm["refs"], fm["kind"])
    except (ValueError, FileNotFoundError) as e:
        print(f"error: {e}", file=sys.stderr)
        return 5

    prompt = assemble(fm, fence, anchor, ref_paths)

    print(
        f"[imagegen] subject={fm['slug']} kind={fm['kind']} refs={len(ref_paths)} prompt={len(prompt)} chars aspect={fm['aspect']}",
        file=sys.stderr,
    )

    if args.dry_run:
        print(prompt)
        return 0

    raw_path, final_path = output_dest(pipeline_root, fm)
    # chroma resolution: explicit frontmatter > kind default > magenta fallback.
    # kind starting with map_ or in {cutscene_cut, environment_site} → OFF (no chroma).
    chroma_explicit = fm.get("chroma")
    if args.no_chroma or chroma_explicit is False or chroma_explicit == "false":
        chroma_hex = None
    elif isinstance(chroma_explicit, str) and chroma_explicit.startswith("#"):
        chroma_hex = chroma_explicit
    else:
        _kind = fm.get("kind", "")
        if _kind.startswith("map_") or _kind in ("cutscene_cut", "environment_site"):
            chroma_hex = None
        else:
            chroma_hex = "#FF00FF"

    user_data_dir = Path(config["user_data_dir"]).resolve()
    user_data_dir.mkdir(parents=True, exist_ok=True)
    print(f"[imagegen] user-data-dir: {user_data_dir}", file=sys.stderr)

    chromium_args = ["--disable-blink-features=AutomationControlled"]
    start_minimized = (
        config.get("start_minimized_default", True) and not args.no_minimized and not args.headless
    )
    if start_minimized:
        chromium_args.append("--start-minimized")

    img_info: dict[str, Any] = {}
    image_bytes = b""

    with sync_playwright() as p:
        context = p.chromium.launch_persistent_context(
            user_data_dir=str(user_data_dir),
            headless=args.headless,
            viewport={"width": 1600, "height": 960},
            args=chromium_args,
        )
        try:
            if start_minimized:
                pids = get_playwright_chromium_pids()
                n = minimize_chrome_windows(pids, timeout_s=3.0)
                if n > 0:
                    print(f"[imagegen] minimized {n} Playwright Chromium window(s)", file=sys.stderr)

            page = context.pages[0] if context.pages else context.new_page()
            ensure_logged_in(page, config["chatgpt_project_url"])
            if start_minimized:
                pids = get_playwright_chromium_pids()
                minimize_chrome_windows(pids, timeout_s=1.0)
            force_new_chat(page, config["chatgpt_project_url"])
            disable_pro_mode(page)
            upload_refs(page, ref_paths)
            inject_prompt(page, prompt)
            initial_srcs = snapshot_initial_imgs(page)
            print(f"[imagegen] pre-submit img snapshot: {len(initial_srcs)} src(s)", file=sys.stderr)
            submit_prompt(page)
            img_info = poll_for_image(page, timeout, initial_srcs)
            image_bytes = download_image(page, img_info["src"])
            save_raw(image_bytes, raw_path, force=args.force)
            print(f"[imagegen] raw saved: {raw_path}", file=sys.stderr)

            if args.keep_browser_open:
                print("[imagegen] --keep-browser-open: press Enter to close browser", file=sys.stderr)
                input()
        finally:
            context.close()

    if chroma_hex is not None:
        try:
            run_chroma_key(pipeline_root, raw_path, final_path, chroma_hex)
        except subprocess.CalledProcessError as e:
            print(f"[imagegen] chroma_key failed (exit={e.returncode}). raw kept at {raw_path}", file=sys.stderr)
            return 6
    else:
        # chroma skipped (frontmatter chroma:false / kind=map_*/cutscene_cut/environment_site / --no-chroma flag) — copy raw to final.
        final_path.parent.mkdir(parents=True, exist_ok=True)
        final_path.write_bytes(raw_path.read_bytes())

    update_status(subject_path, "prompted", "rendered")

    result = {
        "subject": fm["slug"],
        "kind": fm["kind"],
        "raw": str(raw_path.relative_to(pipeline_root.parent)),
        "final": str(final_path.relative_to(pipeline_root.parent)),
        "size_bytes": len(image_bytes),
        "width": img_info.get("width"),
        "height": img_info.get("height"),
        "status": "rendered",
        "chroma_applied": chroma_hex is not None,
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
