/**
 * 1023 资源库问题汇总 —— 本地验证截图脚本（管理端部分）
 *
 * 用法：node scripts/issue-1023/shots-admin.cjs
 * 输出：docs/issue-1023-shots/1*.png
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

  // ── 管理端登录（宿主系统管理员通道）────────────────────────
  console.log('[1] 管理端登录');
  await page.goto(`${BASE}/admin-login`, { waitUntil: 'networkidle' });
  await sleep(1500);
  await page.locator('#login-username').fill('admin');
  await page.locator('#login-password').fill('1q2w3E*');
  await page.locator('.remember input[type=checkbox]').check({ force: true });
  await page.locator('button.submit-btn').click();
  await sleep(6000);
  console.log('  登录后 URL =', page.url());

  // ── 课程管理列表（共享课标签）──────────────────────────────
  console.log('[2] 课程管理');
  await page.goto(`${BASE}/learning/course-list`, { waitUntil: 'networkidle' });
  await sleep(3000);
  await shot(page, '10-course-list');

  // ── 发布弹窗（共享课标签）──────────────────────────────────
  console.log('[3] 发布弹窗');
  const publishBtn = page.locator('a.publish', { hasText: '发布' }).first();
  if (await publishBtn.count()) {
    await publishBtn.scrollIntoViewIfNeeded();
    await sleep(600);
    await publishBtn.click();
    await sleep(1500);
    await shot(page, '11-publish-shared-tag-dialog');

    // 勾选「共享课」再截一张
    const cb = page.locator('.ant-modal-body label input[type=checkbox]').first();
    if (await cb.count()) {
      await cb.check({ force: true });
      await sleep(700);
      await shot(page, '12-publish-shared-tag-checked');
    }
    // 关闭弹窗，不真正发布
    const cancel = page.locator('.ant-modal-footer button', { hasText: '取消' }).first();
    if (await cancel.count()) await cancel.click();
    await sleep(600);
  } else {
    console.log('  ! 未找到「发布」按钮');
  }

  // ── 资源库管理（多校共建）──────────────────────────────────
  console.log('[4] 资源库管理');
  await page.goto(`${BASE}/admin/libraries`, { waitUntil: 'networkidle' });
  await sleep(2500);
  await shot(page, '13-admin-libraries');

  // ── 学校设置（学校档案 / 域名）──────────────────────────────
  console.log('[5] 学校设置');
  await page.goto(`${BASE}/admin/school`, { waitUntil: 'networkidle' });
  await sleep(2500);
  await shot(page, '14-admin-school');

  await browser.close();
  console.log('\n完成。输出目录:', OUT);
})().catch(e => {
  console.error('FAILED:', e);
  process.exit(1);
});
