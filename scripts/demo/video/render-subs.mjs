/**
 * 批量渲染字幕 PNG（小而透明，便于 ffmpeg overlay 快速合成）
 *
 * 用法：node render-subs.mjs <outdir> <json-file>
 * JSON: [{ "text": "字幕文本", "out": "sub-001.png" }, ...]
 *
 * 尺寸固定 1700×220；合成时在 1920×1080 画面上贴到 (110, 820)。
 */
import { chromium } from 'playwright-core';
import fs from 'node:fs';
import path from 'node:path';

const W = 1700;
const H = 220;

const [outDir, jsonFile] = process.argv.slice(2);
if (!outDir || !jsonFile) {
  console.error('用法：node render-subs.mjs <outdir> <json-file>');
  process.exit(1);
}
fs.mkdirSync(outDir, { recursive: true });
const items = JSON.parse(fs.readFileSync(jsonFile, 'utf8'));

const html = (text) => `<!doctype html>
<html><head><meta charset="utf-8"><style>
  * { margin:0; padding:0; box-sizing:border-box; }
  html,body { width:${W}px; height:${H}px; background:transparent; }
  body { font-family:"PingFang SC","Hiragino Sans GB","Microsoft YaHei",sans-serif;
    display:flex; align-items:flex-end; justify-content:center; height:${H}px; }
  .box { max-width:${W - 20}px; background:rgba(6,22,24,.86); border:1px solid rgba(159,227,220,.28);
    border-radius:14px; padding:14px 28px; color:#f4fbfa; font-size:33px; line-height:1.45;
    text-align:center; letter-spacing:1px; box-shadow:0 10px 30px rgba(0,0,0,.5); }
</style></head><body><div class="box">${text}</div></body></html>`;

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const ctx = await browser.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: 1 });
const page = await ctx.newPage();
for (const it of items) {
  await page.setContent(html(it.text));
  await page.waitForTimeout(100);
  await page.screenshot({ path: path.join(outDir, it.out), omitBackground: true });
}
await ctx.close();
await browser.close();
console.log(`rendered ${items.length} subtitle png(s) → ${outDir}`);
