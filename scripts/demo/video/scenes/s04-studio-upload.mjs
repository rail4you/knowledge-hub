/**
 * 场景 s04 · 名师工作室（成员上传资料）
 * 以工作室成员（ssw）身份进入 → 上传资料 → 从课程资源中选文件 → 提交 → 待审核
 */
import { launch, newRecordingContext, saveVideo, login, ACCOUNTS, sleep } from '../lib/browser.mjs';
import { gotoApp, hold } from '../lib/scene.mjs';

const b = await launch();
const { ctx, page } = await newRecordingContext(b, 's04-studio-upload');

await login(page, ACCOUNTS.teacher3); // ssw = 工作室成员
await gotoApp(page, '/learning/studios', 4000);
await hold(page, 3500);

// 进入工作室详情
await page.getByText('语文名师工作室', { exact: true }).first().click();
await sleep(4500);
await hold(page, 5000);

// 上传资料
await page.getByText('上传资料', { exact: true }).first().click();
await sleep(3500);
await hold(page, 5000);

// 选一份自己的课程资源（选尚未加入工作室的那份，避免唯一键冲突）
const pick = page.locator('.up__item').filter({ hasText: '（待审示例）' }).first();
await pick.click();
await sleep(2000);
await hold(page, 2500);

// 标题 + 说明
await page.locator('.ant-modal-content input').nth(1).fill('《春》第二课时课堂实录（成员上传演示）');
await sleep(1200);
await page.locator('.ant-modal-content textarea').first().fill('成员上传，等待领衔人审核后发布。');
await sleep(1500);
await hold(page, 3000);

// 提交
await page.getByText('提交', { exact: true }).first().click();
await sleep(4500);
await hold(page, 14000);

const out = await saveVideo(ctx, page, 's04-studio-upload');
console.log('saved:', out);
await b.close();
