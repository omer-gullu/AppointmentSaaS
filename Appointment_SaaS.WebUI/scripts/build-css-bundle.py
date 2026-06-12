#!/usr/bin/env python3
"""Concatenate WebUI lean CSS bundle (BuildBundlerMinifier minify is disabled for modern selectors)."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CSS = ROOT / "wwwroot" / "css"
INPUTS = [
    "brand-icons.css",
    "pricing.css",
    "layout-shell.css",
    "pricing-critical.css",
    "ar-brand.css",
    "lean-bootstrap.css",
]
OUT = CSS / "bundle.min.css"

def main() -> None:
    parts = []
    for name in INPUTS:
        path = CSS / name
        if not path.is_file():
            raise SystemExit(f"Missing: {path}")
        parts.append(path.read_text(encoding="utf-8"))
    OUT.write_text("".join(parts), encoding="utf-8")
    print(f"Wrote {OUT} ({OUT.stat().st_size} bytes)")

if __name__ == "__main__":
    main()
