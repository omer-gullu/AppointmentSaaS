#!/usr/bin/env python3
"""Crop white background and export circular brand logo for the WebUI."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw
import numpy as np

IMAGES_DIR = Path("Appointment_SaaS.WebUI/wwwroot/images")
OUTPUT_NAME = "brand-logo.webp"
SOURCE_CANDIDATES = (
    "Akıllı Logo.webp",
    "Akilli Logo.webp",
    "akilli-logo.webp",
    "Akıllı-Logo.webp",
)


def find_source() -> Path | None:
    if not IMAGES_DIR.is_dir():
        return None
    for name in SOURCE_CANDIDATES:
        path = IMAGES_DIR / name
        if path.is_file():
            return path
    for path in sorted(IMAGES_DIR.glob("*.webp")):
        if path.name != OUTPUT_NAME:
            return path
    return None


def remove_light_background(img: Image.Image, threshold: int = 235) -> Image.Image:
    rgba = img.convert("RGBA")
    data = np.array(rgba)
    r, g, b, a = data[:, :, 0], data[:, :, 1], data[:, :, 2], data[:, :, 3]
    light = (r >= threshold) & (g >= threshold) & (b >= threshold)
    data[light, 3] = 0
    return Image.fromarray(data)


def crop_to_content(img: Image.Image) -> Image.Image:
    bbox = img.getbbox()
    if not bbox:
        return img
    return img.crop(bbox)


def fit_square(img: Image.Image) -> Image.Image:
    width, height = img.size
    side = max(width, height)
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(img, ((side - width) // 2, (side - height) // 2))
    return canvas


def apply_circle_mask(img: Image.Image) -> Image.Image:
    squared = fit_square(img)
    size = squared.size[0]
    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    draw.ellipse((0, 0, size - 1, size - 1), fill=255)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.paste(squared, (0, 0), mask)
    return out


def main() -> int:
    source = find_source()
    if source is None:
        print(f"Kaynak logo bulunamadı. Şuraya ekleyin: {IMAGES_DIR.resolve()}")
        return 1

    image = Image.open(source)
    image = remove_light_background(image)
    image = crop_to_content(image)
    image = apply_circle_mask(image)
    image = image.resize((256, 256), Image.Resampling.LANCZOS)

    IMAGES_DIR.mkdir(parents=True, exist_ok=True)
    output = IMAGES_DIR / OUTPUT_NAME
    image.save(output, format="WEBP", quality=92)
    print(f"Oluşturuldu: {output} (kaynak: {source.name})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
