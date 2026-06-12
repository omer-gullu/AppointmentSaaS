import sharp from "sharp";
import { copyFileSync, renameSync, writeFileSync, existsSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

const dir = dirname(fileURLToPath(import.meta.url));
const outputPath = join(dir, "brand-logo.png");
const tempPath = join(dir, "brand-logo.tmp.png");
const size = 512;
const insetPx = 0;

function resolveSourcePath() {
  const preferred = process.env.BRAND_LOGO_SOURCE;
  if (preferred) {
    const custom = join(dir, preferred);
    if (existsSync(custom)) return custom;
  }

  const brandLogo1 = join(dir, "brand-logo1.png");
  const brandLogoSource = join(dir, "brand-logo-source.png");

  if (existsSync(brandLogo1)) {
    copyFileSync(brandLogo1, brandLogoSource);
  }

  for (const name of ["brand-logo-source.png", "brand-logo1.png", "brand-logo.png"]) {
    const path = join(dir, name);
    if (existsSync(path)) return path;
  }

  throw new Error("No brand logo source found (brand-logo-source.png, brand-logo1.png, or brand-logo.png)");
}

function isBackgroundPixel(r, g, b, a) {
  if (a < 16) return true;
  const avg = (r + g + b) / 3;
  const maxDiff = Math.max(Math.abs(r - g), Math.abs(g - b), Math.abs(r - b));
  if (avg >= 140 && maxDiff < 48) return true;
  if (avg >= 196 && maxDiff < 42) return true;
  if (avg >= 168 && maxDiff < 26) return true;
  return false;
}

function isEdgeFringe(r, g, b) {
  const avg = (r + g + b) / 3;
  const maxDiff = Math.max(Math.abs(r - g), Math.abs(g - b), Math.abs(r - b));
  if (avg >= 118 && maxDiff < 52) return true;
  return b > r + 12 && b > g + 8 && avg >= 95;
}

function clearPixel(data, idx) {
  data[idx] = 0;
  data[idx + 1] = 0;
  data[idx + 2] = 0;
  data[idx + 3] = 0;
}

function measureContentBounds(data, width, height) {
  let minX = width;
  let minY = height;
  let maxX = 0;
  let maxY = 0;
  let count = 0;

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = (y * width + x) * 4;
      if (data[idx + 3] < 20) continue;
      if (isBackgroundPixel(data[idx], data[idx + 1], data[idx + 2], data[idx + 3])) continue;

      count++;
      if (x < minX) minX = x;
      if (y < minY) minY = y;
      if (x > maxX) maxX = x;
      if (y > maxY) maxY = y;
    }
  }

  if (count === 0) {
    return { cx: width / 2, cy: height / 2, radius: width / 2 - insetPx };
  }

  const cx = (minX + maxX) / 2;
  const cy = (minY + maxY) / 2;
  const radius = (Math.min(maxX - minX, maxY - minY) / 2) - insetPx;
  return { cx, cy, radius: Math.max(8, radius) };
}

const sourcePath = resolveSourcePath();

const meta = await sharp(sourcePath).metadata();
const side = Math.min(meta.width, meta.height);
const left = Math.round((meta.width - side) / 2);
const top = Math.round((meta.height - side) / 2);

const { data, info } = await sharp(sourcePath)
  .extract({ left, top, width: side, height: side })
  .resize(size, size, { fit: "cover", position: "centre" })
  .ensureAlpha()
  .raw()
  .toBuffer({ resolveWithObject: true });

for (let y = 0; y < info.height; y++) {
  for (let x = 0; x < info.width; x++) {
    const idx = (y * info.width + x) * 4;
    if (isBackgroundPixel(data[idx], data[idx + 1], data[idx + 2], data[idx + 3])) {
      clearPixel(data, idx);
    }
  }
}

const { cx, cy, radius: circleRadius } = measureContentBounds(data, info.width, info.height);
const fringeBand = Math.max(6, Math.round(circleRadius * 0.06));

for (let y = 0; y < info.height; y++) {
  for (let x = 0; x < info.width; x++) {
    const idx = (y * info.width + x) * 4;
    if (data[idx + 3] === 0) continue;

    const r = data[idx];
    const g = data[idx + 1];
    const b = data[idx + 2];
    let a = data[idx + 3];

    const dist = Math.hypot(x - cx, y - cy);
    if (dist > circleRadius) {
      clearPixel(data, idx);
      continue;
    }

    const distToEdge = circleRadius - dist;
    if (distToEdge < fringeBand && (isBackgroundPixel(r, g, b, a) || isEdgeFringe(r, g, b))) {
      const fade = Math.max(0, Math.min(1, distToEdge / fringeBand));
      a = Math.round(a * fade * fade);
      if (a < 10) {
        clearPixel(data, idx);
        continue;
      }
      data[idx + 3] = a;
    }
  }
}

const circleMaskSvg = Buffer.from(
  `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}">
    <circle cx="${cx}" cy="${cy}" r="${circleRadius}" fill="white"/>
  </svg>`
);

const masked = await sharp(data, {
  raw: { width: info.width, height: info.height, channels: 4 },
})
  .composite([
    {
      input: await sharp(circleMaskSvg).png().toBuffer(),
      blend: "dest-in",
    },
  ])
  .png({ compressionLevel: 9 })
  .toBuffer();

const finalLogo = await sharp(masked)
  .trim({ threshold: 8 })
  .resize(size, size, { fit: "cover", position: "centre" })
  .ensureAlpha()
  .png({ compressionLevel: 9 })
  .toBuffer();

writeFileSync(tempPath, finalLogo);
renameSync(tempPath, outputPath);

await sharp(finalLogo).resize(32, 32, { fit: "cover" }).png().toFile(join(dir, "favicon-32.png"));
await sharp(finalLogo).resize(16, 16, { fit: "cover" }).png().toFile(join(dir, "favicon-16.png"));
await sharp(finalLogo).resize(180, 180, { fit: "cover" }).png().toFile(join(dir, "apple-touch-icon.png"));

console.log(
  `Circular logo ready from ${sourcePath} (center ${cx.toFixed(1)},${cy.toFixed(1)} r=${circleRadius.toFixed(1)}, ${masked.length} bytes)`
);
