/**
 * 1023 资源库问题汇总 —— 本地验证截图脚本（租户教师端：发布共享课 + 共享后选课）
 *
 * 用法：node scripts/issue-1023/shots-teacher.cjs
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
const USERS = ['qa-teacher', 'wwq', '1213', 'school_admin'];
const COURSE_TITLE = `共享课验证-${Date.now().toString().slice(-6)}`;

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const ctx = await browser.newContext({
    viewport: { width: 1600, height: 1000 },
    ignoreHTTPSErrors: true,
  });
  const page = await ctx.newPage();
  page.on('pageerror', e => console.log('  [pageerror]', e.message));

  // ── 登录（租户教师）─────────────────────────────────────────
  console.log('[1] 租户教师登录');
  await page.goto(`${BASE}/account/login`, { waitUntil: 'networkidle' });
  await sleep(1800);
  await page.locator('.tenant-trigger').click();
  await sleep(600);
  const opt = page.locator('.tenant-option', { hasText: '启迪学院' }).first();
  if (await opt.count()) { await opt.click(); await sleep(700); }

  let ok = false;
  for (const u of USERS) {
    await page.locator('#login-username').fill(u);
    await page.locator('#login-password').fill('1q2w3E*');
    await page.locator('.remember input[type=checkbox]').check({ force: true });
    await page.locator('button.submit-btn').click();
    await sleep(5000);
    if (!page.url().includes('/account/login')) { console.log(`  ✓ 登录成功：${u}`); ok = true; break; }
    console.log(`  ✗ ${u} 登录失败`);
  }
  if (!ok) { console.log('  ! 教师账号登录失败，终止'); await browser.close(); return; }

  // ── 新建课程（草稿）────────────────────────────────────────
  console.log('[2] 新建课程（草稿）');
  await page.goto(`${BASE}/learning/course-list`, { waitUntil: 'networkidle' });
  await sleep(3000);
  await page.locator('button', { hasText: '新建课程' }).first().click();
  await sleep(1500);
  const titleInput = page.locator('.ant-modal-body input').first();
  await titleInput.fill(COURSE_TITLE);
  await sleep(400);
  await page.locator('.ant-modal-footer button', { hasText: '保存' }).first().click();
  await sleep(4000);
  await shot(page, '10-course-created-draft');

  // ── 发布弹窗（共享课标签）──────────────────────────────────
  console.log('[3] 发布弹窗');
  const row = page.locator('tr', { hasText: COURSE_TITLE }).first();
  const publishBtn = row.locator('a.publish', { hasText: '发布' }).first();
  if (await publishBtn.count()) {
    await publishBtn.scrollIntoViewIfNeeded();
    await sleep(600);
    await publishBtn.click();
    await sleep(1800);
    await shot(page, '11-publish-shared-tag-dialog');

    const cb = page.locator('.ant-modal-body input[type=checkbox]').first();
    if (await cb.count()) {
      await cb.check({ force: true });
      await sleep(800);
      await shot(page, '12-publish-shared-tag-checked');

      // 确认发布
      const okBtn = page.locator('.ant-modal-footer button', { hasText: '确认发布' }).first();
      await okBtn.click();
      await sleep(5000);
      await shot(page, '13-course-published-shared');
      console.log('  ✓ 已发布为共享课');
    }
  } else {
    console.log('  ! 未找到「发布」按钮');
    await shot(page, '11-course-list-nopublish');
  }

  await browser.close();
  console.log('\n完成。输出目录:', OUT);
})().catch(e => { console.error('FAILED:', e); process.exit(1); });
