/**
 * 1023 资源库问题汇总 —— 本地验证截图脚本
 *
 * 用法：node scripts/issue-1023/shots.cjs
 * 输出：docs/issue-1023-shots/*.png
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
    deviceScaleFactor: 1,
    ignoreHTTPSErrors: true,
  });
  const page = await ctx.newPage();
  page.on('pageerror', e => console.log('  [pageerror]', e.message));

  // ── 1. 登录页：注册按钮 + 机构中文名 + 机构搜索框 ───────────────
  console.log('[1] 登录页');
  await page.goto(`${BASE}/account/login`, { waitUntil: 'networkidle' });
  await sleep(1500);
  await shot(page, '01-login-page');

  // 打开机构下拉（展示中文机构名 + 搜索框）
  const tenantTrigger = page.locator('.tenant-trigger');
  if (await tenantTrigger.count()) {
    await tenantTrigger.first().click();
    await sleep(700);
    await shot(page, '02-login-tenant-search');
    await page.keyboard.press('Escape');
    await page.mouse.click(10, 10);
    await sleep(300);
  }

  // ── 2. 登录表单空值提醒（#4）────────────────────────────────
  console.log('[2] 登录空值提醒');
  await page.goto(`${BASE}/account/login`, { waitUntil: 'networkidle' });
  await sleep(1200);
  await page.locator('button.submit-btn').click();
  await sleep(900);
  await shot(page, '03-login-validation');

  // ── 3. 首页资源库卡片（#6 #7 #9）────────────────────────────
  console.log('[3] 首页资源库卡片');
  await page.goto(BASE, { waitUntil: 'networkidle' });
  await sleep(2500);
  const libSection = page.locator('#tenants');
  if (await libSection.count()) {
    await libSection.scrollIntoViewIfNeeded();
    await sleep(1200);
  }
  await shot(page, '04-home-library-cards');

  // 卡片整卡可点击：hover 显示 pointer + 点击进入资源库
  const card = page.locator('app-library-card .library-card').first();
  if (await card.count()) {
    await card.scrollIntoViewIfNeeded();
    await sleep(600);
    const cursor = await card.evaluate(el => getComputedStyle(el).cursor);
    console.log('  卡片 cursor =', cursor);
    // 记录卡片高度，验证对齐
    const heights = await page.locator('app-library-card .library-card').evaluateAll(
      els => els.map(e => Math.round(e.getBoundingClientRect().height))
    );
    console.log('  卡片高度 =', heights.join(', '));
  }

  // ── 4. 探索课程页（#11 专业/资源库筛选已删）─────────────────
  console.log('[4] 探索课程');
  await page.goto(`${BASE}/explore/courses`, { waitUntil: 'networkidle' });
  await sleep(2000);
  await shot(page, '05-explore-courses');

  // ── 5. 资源库详情（#9 建设单位 / 师资团队）────────────────────
  console.log('[5] 资源库详情');
  const tenantId = '3a2034d1-6c43-2219-8149-45182659c849';
  await page.goto(`${BASE}/tenant/${tenantId}`, { waitUntil: 'networkidle' });
  await sleep(2500);
  await shot(page, '06-tenant-library-detail');
  const intro = page.locator('#section-intro');
  if (await intro.count()) {
    await intro.scrollIntoViewIfNeeded();
    await sleep(900);
    await shot(page, '07-tenant-library-owner-teachers');
  }

  // ── 6. 资源库课程点击 → 登录页（#8）─────────────────────────
  console.log('[6] 资源库课程点击');
  const courseBtn = page.locator('#section-courses .course-card').first();
  if (await courseBtn.count()) {
    await courseBtn.scrollIntoViewIfNeeded();
    await sleep(500);
    await courseBtn.click();
    await sleep(2000);
    await shot(page, '08-course-click-to-login');
    console.log('  跳转后 URL =', page.url());
  }

  // ── 6.5 学校简介页（#10 学校的页面，展示该校所有资源）─────────
  console.log('[7] 学校简介页');
  await page.goto(`${BASE}/school/${tenantId}`, { waitUntil: 'networkidle' });
  await sleep(2500);
  await shot(page, '09-school-profile');

  // ── 7. 游客访问微专业详情（#13 #14）──────────────────────────
  console.log('[7] 游客微专业详情');
  await page.goto(`${BASE}/explore/micro`, { waitUntil: 'networkidle' });
  await sleep(2000);
  const microLink = page.locator('.ex-micro h3').first();
  let microUrl = '';
  if (await microLink.count()) {
    microUrl = await microLink.evaluate(el => el.closest('article')?.querySelector('a')?.getAttribute('href') || '');
  }
  if (microUrl) {
    await page.goto(`${BASE}${microUrl}`, { waitUntil: 'networkidle' });
  } else {
    await page.goto(`${BASE}/student/micro-majors`, { waitUntil: 'networkidle' });
  }
  await sleep(3000);
  await shot(page, '10-micro-major-guest');
  const modalCount = await page.locator('app-auth-error-modal .ant-modal-wrap, .ant-modal-confirm').count();
  console.log('  登录已过期弹窗数量 =', modalCount);

  await browser.close();
  console.log('\n完成。输出目录:', OUT);
})().catch(e => {
  console.error('FAILED:', e);
  process.exit(1);
});
