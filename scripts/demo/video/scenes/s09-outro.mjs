/**
 * 场景 s09 · 收尾
 * 白板列表 → 资源中心（模块筛选）→ 结束
 */
import { launch, newRecordingContext, saveVideo, login, ACCOUNTS, sleep } from '../lib/browser.mjs';
import { gotoApp, hold } from '../lib/scene.mjs';

const b = await launch();
const { ctx, page } = await newRecordingContext(b, 's09-outro');

await login(page, ACCOUNTS.teacher);
await gotoApp(page, '/learning/whiteboards', 5000);
await hold(page, 5000);

await gotoApp(page, '/resources', 5000);
await hold(page, 4000);

// 按模块筛选收尾（展示模块化沉淀）
const moduleFilter = page.locator('.filter-select--module').first();
await moduleFilter.click();
await sleep(1800);
await page.locator('.ant-select-dropdown:not(.ant-select-dropdown-hidden) .ant-select-item-option', { hasText: '课标教材' }).first().click();
await sleep(4500);
await hold(page, 6000);

const out = await saveVideo(ctx, page, 's09-outro');
console.log('saved:', out);
await b.close();
