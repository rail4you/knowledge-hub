/**
 * 工具：为演示账号上传一份「图片」资源（白板选素材用）
 * 用法：node upload-image-resource.mjs
 */
import { launch, login, ACCOUNTS, sleep, getToken, deleteResourcesByName } from '../lib/browser.mjs';
import { gotoApp, hold, selectOption } from '../lib/scene.mjs';

const NAME = '特教课堂插图（演示）';
const FILE = '/Users/bai/projects/KnowledgeHub/scripts/demo/video/assets/特教课堂插图-演示.png';

await deleteResourcesByName(NAME, await getToken(ACCOUNTS.admin));

const b = await launch();
const ctx = await b.newContext({ viewport: { width: 1920, height: 1080 }, locale: 'zh-CN' });
const page = await ctx.newPage();
await login(page, ACCOUNTS.teacher);
await gotoApp(page, '/resources', 5000);

await page.getByText('新建资源', { exact: true }).first().click();
await sleep(3000);
await page.locator('.ant-modal-content input[formcontrolname="name"]').first().fill(NAME);
await sleep(800);
await selectOption(page, page.locator('.ant-modal-content nz-select[formcontrolname="resourceType"]').first(), '文档');
await sleep(800);
await selectOption(page, page.locator('.ant-modal-content nz-select[formcontrolname="module"]').first(), '课程资源');
await sleep(800);

// 分类（必填）
await page.locator('.ant-modal-content nz-tree-select').first().click();
await sleep(1500);
const cat = page.locator('.ant-select-tree-node-content-wrapper', { hasText: '资料文件夹' }).first();
if (await cat.count()) await cat.click();
await sleep(1000);

await page.locator('.ant-modal-content textarea[formcontrolname="description"]').first().fill('教研白板演示用插图素材。');
await sleep(500);
await page.locator('.ant-modal-content input[type="file"]').first().setInputFiles(FILE);
await sleep(7000);
const saveBtn = page.locator('.ant-modal-footer button').filter({ hasText: '保存' }).first();
await saveBtn.scrollIntoViewIfNeeded();
await sleep(500);
await saveBtn.click({ force: true });
await sleep(5000);
await hold(page, 1500);

const list = await page.evaluate(() => document.body.innerText.includes('特教课堂插图（演示）'));
console.log('created:', list);
await ctx.close();
await b.close();
