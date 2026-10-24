/**
 * 1023 资源库问题汇总 —— 本地验证截图脚本（租户管理端：多校共建 / 一租户多库 / 学校域名）
 *
 * 用法：node scripts/issue-1023/shots-tenant-admin.cjs
 */
const path = require('path');
const fs = require('fs');
const {
  chromium,
} = require('/Users/bai/.hermes/node/lib/node_modules/@playwright/cli/node_modules/playwright');

const BASE = process.env.KH_BASE || 'http://localhost:4200';
const OUT = path.resolve(__dirname, '../../docs/issue-1023-shots');
fs.mkdirSync(OUT, { recursive: true });

const shot = async (page, name) => {
  await page.screenshot({ path: path.join(OUT, `${name}.png`), fullPage: false });
  console.log(`  ✓ ${name}.png`);
};
const sleep = ms => new Promise(r => setTimeout(r, ms));

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const ctx = await browser.newContext({
    viewport: { width: 1600, height: 1000 },
    ignoreHTTPSErrors: true,
  });
  const page = await ctx.newPage();
  page.on('pageerror', e => console.log('  [pageerror]', e.message));

  // 登录启迪学院（qidi）
  console.log('[1] 启迪学院管理端登录');
  await page.goto(`${BASE}/account/login`, { waitUntil: 'networkidle' });
  await sleep(1800);
  await page.locator('.tenant-trigger').click();
  await sleep(600);
  await page.locator('.tenant-option', { hasText: '启迪学院' }).first().click();
  await sleep(700);
  let loginOk = false;
  for (const u of ['repro-admin', 'qa-teacher', '1213', 'wwq']) {
    await page.locator('#login-username').fill(u);
    await page.locator('#login-password').fill('1q2w3E*');
    await page.locator('.remember input[type=checkbox]').check({ force: true });
    await page.locator('button.submit-btn').click();
    await sleep(5500);
    if (!page.url().includes('/account/login')) { console.log('  ✓ 登录成功：', u); loginOk = true; break; }
    console.log('  ✗ 登录失败：', u);
  }
  if (!loginOk) { console.log('  ! 登录失败，终止'); await browser.close(); return; }
  console.log('  url =', page.url());

  // ── 资源库管理（多校共建 + 一租户多库）──────────────────────
  console.log('[2] 资源库管理');
  await page.goto(`${BASE}/admin/libraries`, { waitUntil: 'networkidle' });
  await sleep(3000);
  await shot(page, '13-admin-libraries');

  // 新建第二个资源库（验证「一个租户下面得用n个资源库」）
  const newBtn = page.locator('button', { hasText: '新建资源库' }).first();
  if (await newBtn.count()) {
    await newBtn.click();
    await sleep(2500);
    const inputs = page.locator('.ant-modal-body input');
    const n = await inputs.count();
    if (n > 0) await inputs.first().fill('智能制造专业资源库');
    const ta = page.locator('.ant-modal-body textarea');
    if (await ta.count()) await ta.first().fill('面向智能制造方向的专业教学资源库');
    const cover = page.locator('.ant-modal-body input[placeholder^="https"]');
    if (await cover.count()) await cover.fill('assets/images/tenant-hero_001.jpg');
    await sleep(400);
    await page.locator('.ant-modal-footer button.ant-btn-primary').first().click();
    await sleep(4000);
    await page.keyboard.press('Escape');
    await sleep(1200);
    await shot(page, '14-admin-libraries-two');
  }

  // ── 学校设置（学校档案 / 独立域名）──────────────────────────
  console.log('[3] 学校设置');
  await page.goto(`${BASE}/admin/school`, { waitUntil: 'networkidle' });
  await sleep(2500);
  await shot(page, '15-admin-school-tenant');

  await browser.close();
  console.log('\n完成。输出目录:', OUT);
})().catch(e => { console.error('FAILED:', e); process.exit(1); });
