/**
 * 场景 s03 · 课程资源管理（前台筛选与在线播放）
 * 资源中心 → 按模块筛选（政策经验 / 课标教材）→ 详情 → 预览 → 视频真实播放
 */
import { launch, newRecordingContext, saveVideo, login, ACCOUNTS, sleep } from '../lib/browser.mjs';
import { gotoApp, hold, selectOption, slowHover } from '../lib/scene.mjs';

const b = await launch();
const { ctx, page } = await newRecordingContext(b, 's03-resource-browse');

await login(page, ACCOUNTS.teacher);
await gotoApp(page, '/resources', 5000);
await hold(page, 3500);

// 按模块筛选 → 政策经验
const moduleFilter = page.locator('.filter-select--module').first();
await moduleFilter.click();
await sleep(1500);
await page.locator('.ant-select-dropdown:not(.ant-select-dropdown-hidden) .ant-select-item-option', { hasText: '政策经验' }).first().click();
await sleep(4000);
await hold(page, 8000);

// 换成 课标教材
await moduleFilter.click();
await sleep(1500);
await page.locator('.ant-select-dropdown:not(.ant-select-dropdown-hidden) .ant-select-item-option', { hasText: '课标教材' }).first().click();
await sleep(4000);
await hold(page, 7000);

// 打开「特教课堂教学示范视频」详情
await page.getByText('特教课堂教学示范视频', { exact: true }).first().click();
await sleep(4500);
await hold(page, 5000);

// 预览 → 视频播放
const previewCard = page.locator('.drawer-actions .action-card', { hasText: '预览' }).first();
await previewCard.click();
await sleep(6000);
await hold(page, 4000);

const video = page.locator('video').first();
if (await video.count()) {
  await video.evaluate((v) => { v.muted = false; v.currentTime = 0; return v.play?.(); });
  await sleep(1500);
  await hold(page, 9000);
  await video.evaluate((v) => { v.pause(); });
}
await hold(page, 2000);

const out = await saveVideo(ctx, page, 's03-resource-browse');
console.log('saved:', out);
await b.close();
