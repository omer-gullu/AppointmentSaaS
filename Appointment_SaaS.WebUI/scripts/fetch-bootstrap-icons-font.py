#!/usr/bin/env python3
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
dest = ROOT / "wwwroot" / "fonts" / "bootstrap-icons.woff2"
dest.parent.mkdir(parents=True, exist_ok=True)
url = "https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/fonts/bootstrap-icons.woff2"
urllib.request.urlretrieve(url, dest)
print(dest.stat().st_size)
