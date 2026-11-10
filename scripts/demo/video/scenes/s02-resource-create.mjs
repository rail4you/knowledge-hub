/**
 * 场景 s02 · 课程资源管理（后台新建资源）
 * 资源中心 → 新建资源 → 所属模块 → 上传文件（限速，进度条可见）→ 保存 → 待审核
 */
import { launch, newRecordingContext, saveVideo, login, ACCOUNTS, sleep, getToken, deleteResourcesByName } from '../lib/browser.mjs';
import { gotoApp, hold, selectOption } from '../lib/scene.mjs';

const UPLOAD_FILE = process.env.DEMO_UPLOAD_FILE
  || '/Users/bai/projects/KnowledgeHub/scripts/demo/video/assets/特教课堂教学示范视频-演示.mp4';
const RESOURCE_NAME = '自闭症学生社交故事（演示）';

const b = await launch();
const { ctx, page } = await newRecordingContext(b, 's02-resource-create');

// 清理上一轮同名资源（避免列表里出现重复）
await deleteResourcesByName(RESOURCE_NAME, await getToken(ACCOUNTS.admin));

await login(page, ACCOUNTS.teacher);
await gotoApp(page, '/resources', 5000);
await hold(page, 3000);

// 打开「新建资源」
await page.getByText('新建资源', { exact: true }).first().click();
await sleep(3000);

// 资源名称
await page.locator('.ant-modal-content input[formcontrolname="name"]').first().fill(RESOURCE_NAME);
await sleep(1200);

// 资源类型 = 视频
await selectOption(page, page.locator('.ant-modal-content nz-select[formcontrolname="resourceType"]').first(), '视频');
await sleep(1000);

// 所属模块 = 教具辅具（演示重点：展开下拉展示十个栏目）
const moduleSelect = page.locator('.ant-modal-content nz-select[formcontrolname="module"]').first();
await moduleSelect.click();
await sleep(1800);
const moduleOptions = await page.evaluate(() =>
  Array.from(document.querySelectorAll('.ant-select-dropdown:not(.ant-select-dropdown-hidden) .ant-select-item-option'))
    .map((o) => o.innerText.trim())
    .filter(Boolean),
);
console.log('模块下拉选项：', JSON.stringify(moduleOptions));
await sleep(2500);
await page.locator('.ant-select-dropdown:not(.ant-select-dropdown-hidden) .ant-select-item-option', { hasText: '教具辅具' }).first().click();
await sleep(1500);

// 分类（必填，资源分类树）
const catSelect = page.locator('.ant-modal-content nz-tree-select').first();
await catSelect.click();
await sleep(1800);
const catOpt = page.locator('.ant-select-tree-node-content-wrapper', { hasText: '资料文件夹' }).first();
await catOpt.waitFor({ state: 'visible', timeout: 15000 });
await catOpt.click();
await sleep(1500);

// 描述
await page.locator('.ant-modal-content textarea[formcontrolname="description"]').first().fill('面向自闭症学生的社交故事示范课件，用于演示资源模块化归类与在线播放。');
await sleep(1000);

// 滚动到「上传文件」区域，让上传进度条出现在画面中
await page.mouse.move(960, 700);
for (let i = 0; i < 4; i++) {
  await page.mouse.wheel(0, 600);
  await sleep(700);
}
await sleep(1500);
const dropBox = await page.locator('.ant-modal-content input[type="file"]').first().boundingBox();
console.log('上传区位置:', JSON.stringify(dropBox));

// 上传文件（限速让进度条可见）
const cdp = await ctx.newCDPSession(page);
await cdp.send('Network.enable');
await cdp.send('Network.emulateNetworkConditions', {
  offline: false, latency: 60, downloadThroughput: -1, uploadThroughput: (900 * 1024) / 8,
});
await page.locator('.ant-modal-content input[type="file"]').first().setInputFiles(UPLOAD_FILE);
await sleep(6000);
await hold(page, 12000);
await cdp.send('Network.emulateNetworkConditions', {
  offline: false, latency: 0, downloadThroughput: -1, uploadThroughput: -1,
});
await sleep(4000);

// 保存
await page.getByText('保存', { exact: true }).first().click();
await sleep(5000);
await hold(page, 5000);

// 打开刚建的资源 → 提交审核 → 状态变为「待审核」
await page.getByText(RESOURCE_NAME, { exact: true }).first().click();
await sleep(4000);
await hold(page, 2500);
const submitBtn = page.locator('.drawer-actions .action-card', { hasText: '提交审核' }).first();
if (await submitBtn.count()) {
  await submitBtn.click();
  await sleep(3500);
  await hold(page, 4000);
}
// 关闭抽屉，回到列表看状态
await page.locator('.drawer-close-btn').first().click();
await sleep(3000);
await hold(page, 5000);

const out = await saveVideo(ctx, page, 's02-resource-create');
console.log('saved:', out);
await b.close();
