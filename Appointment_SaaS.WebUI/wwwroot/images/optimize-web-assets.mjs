import sharp from "sharp";
import { existsSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

const dir = dirname(fileURLToPath(import.meta.url));
const processed = join(dir, "brand-logo.png");

if (!existsSync(processed)) {
  console.error("Run process-brand-logo.mjs first to create brand-logo.png");
  process.exit(1);
}

async function circleMaskPng(size) {
  return sharp({
    create: {
      width: size,
      height: size,
      channels: 4,
      background: { r: 0, g: 0, b: 0, alpha: 0 },
    },
  })
    .composite([
      {
        input: Buffer.from(
          `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}">
            <circle cx="${size / 2}" cy="${size / 2}" r="${size / 2}" fill="white"/>
          </svg>`
        ),
        blend: "over",
      },
    ])
    .png()
    .toBuffer();
}

async function exportCircularLogo(px) {
  const mask = await circleMaskPng(px);
  const pipeline = sharp(processed)
    .resize(px, px, { fit: "cover", position: "centre" })
    .ensureAlpha()
    .composite([{ input: mask, blend: "dest-in" }]);

  const pngBuf = await pipeline.png({ compressionLevel: 9 }).toBuffer();
  await sharp(pngBuf).toFile(join(dir, `brand-logo-${px}.png`));
  await sharp(pngBuf)
    .webp({ quality: 86, alphaQuality: 100, effort: 4 })
    .toFile(join(dir, `brand-logo-${px}.webp`));
}

const sizes = [96, 192];
for (const px of sizes) {
  await exportCircularLogo(px);
}

const png192 = await sharp(processed)
  .resize(192, 192, { fit: "cover", position: "centre" })
  .ensureAlpha()
  .composite([{ input: await circleMaskPng(192), blend: "dest-in" }])
  .png({ compressionLevel: 9 })
  .toBuffer();

await sharp(png192).webp({ quality: 86, alphaQuality: 100 }).toFile(join(dir, "brand-logo.webp"));
await sharp(png192).toFile(join(dir, "brand-logo-192.png"));

for (const [px, name] of [
  [32, "favicon-32.png"],
  [16, "favicon-16.png"],
  [180, "apple-touch-icon.png"],
]) {
  const mask = await circleMaskPng(px);
  await sharp(processed)
    .resize(px, px, { fit: "cover", position: "centre" })
    .ensureAlpha()
    .composite([{ input: mask, blend: "dest-in" }])
    .png()
    .toFile(join(dir, name));
}

const stat = await sharp(join(dir, "brand-logo-192.png")).metadata();
console.log(`Circular logo variants ready (${stat.width}x${stat.height}, alpha preserved)`);
