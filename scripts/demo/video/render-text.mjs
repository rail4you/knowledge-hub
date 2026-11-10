/**
 * 文字渲染工具（ffmpeg 本机无 drawtext / libass，改用无头浏览器渲染 PNG）
 *
 * 用法：
 *   node render-text.mjs title    <out.png> <group> <title> <subtitle>
 *   node render-text.mjs subtitle <out.png> <text>
 */
import { chromium } from 'playwright-core';
import fs from 'node:fs';

const W = 1920;
const H = 1080;

const titleHtml = (group, title, subtitle) => `<!doctype html>
<html><head><meta charset="utf-8"><style>
  * { margin:0; padding:0; box-sizing:border-box; }
  html,body { width:${W}px; height:${H}px; }
  body {
    font-family: "PingFang SC","Hiragino Sans GB","Microsoft YaHei",sans-serif;
    background:
      radial-gradient(1200px 700px at 18% 18%, #0f8f88 0%, rgba(15,143,136,0) 60%),
      radial-gradient(900px 600px at 85% 85%, #1b4f5a 0%, rgba(27,79,90,0) 62%),
      linear-gradient(135deg, #07332f 0%, #0b3b3a 55%, #062a2b 100%);
    color:#fff; display:flex; flex-direction:column; justify-content:center;
    padding:0 140px; position:relative; overflow:hidden;
  }
  .badge { display:inline-flex; align-items:center; gap:14px; font-size:22px; letter-spacing:4px;
    color:#9fe3dc; border:1px solid rgba(159,227,220,.45); border-radius:999px; padding:10px 26px; width:max-content; }
  .badge i { width:10px; height:10px; border-radius:50%; background:#5fe0c8; box-shadow:0 0 14px #5fe0c8; }
  h1 { font-size:92px; font-weight:800; letter-spacing:4px; margin:44px 0 0; line-height:1.18; }
  h2 { font-size:38px; font-weight:500; color:#bfe9e4; margin-top:26px; letter-spacing:2px; }
  .bar { width:180px; height:8px; border-radius:6px; background:linear-gradient(90deg,#5fe0c8,#0f8f88); margin-top:46px; }
  .foot { position:absolute; right:140px; bottom:90px; font-size:24px; color:rgba(255,255,255,.55); letter-spacing:2px; }
  .top { position:absolute; left:140px; top:84px; font-size:26px; color:rgba(255,255,255,.75); letter-spacing:3px; }
</style></head><body>
  <div class="top">特殊教育课程教学资源平台</div>
  <div class="badge"><i></i>${group}</div>
  <h1>${title}</h1>
  <h2>${subtitle}</h2>
  <div class="bar"></div>
  <div class="foot">生产环境真实操作录屏</div>
</body></html>`;

const subtitleHtml = (text) => `<!doctype html>
<html><head><meta charset="utf-8"><style>
  * { margin:0; padding:0; box-sizing:border-box; }
  html,body { width:${W}px; height:${H}px; background:transparent; }
  body { font-family:"PingFang SC","Hiragino Sans GB","Microsoft YaHei",sans-serif;
    display:flex; align-items:flex-end; justify-content:center; padding-bottom:56px; }
  .box { max-width:1560px; background:rgba(8,26,28,.82); border:1px solid rgba(159,227,220,.25);
    border-radius:14px; padding:16px 30px; color:#f2fbfa; font-size:34px; line-height:1.5;
    text-align:center; letter-spacing:1px; box-shadow:0 8px 28px rgba(0,0,0,.45); }
</style></head><body><div class="box">${text}</div></body></html>`;

const [mode, out, ...rest] = process.argv.slice(2);
if (!mode || !out) {
  console.error('用法：node render-text.mjs title|subtitle <out.png> [args]');
  process.exit(1);
}
const html = mode === 'title' ? titleHtml(rest[0] || '', rest[1] || '', rest[2] || '') : subtitleHtml(rest.join(' '));
fs.writeFileSync('/tmp/_render.html', html);

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const ctx = await browser.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: 1 });
const page = await ctx.newPage();
await page.goto('file:///tmp/_render.html');
await page.waitForTimeout(700);
await page.screenshot({ path: out, omitBackground: mode !== 'title' });
await ctx.close();
await browser.close();
console.log('rendered:', out);
