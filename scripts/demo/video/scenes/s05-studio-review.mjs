/**
 * 场景 s05 · 名师工作室（领衔人审核并发布 + 跨账号可见）
 * 左窗 A：领衔人 wwq → 通过并发布
 * 右窗 B：成员 ssw → 刷新后看到「已发布」
 */
import { launch, newRecordingContext, saveVideo, login, ACCOUNTS, sleep } from '../lib/browser.mjs';
import { gotoApp, hold } from '../lib/scene.mjs';

const b = await launch();
const A = await newRecordingContext(b, 's05-a');
const B = await newRecordingContext(b, 's05-b');

// 两个账号并行登录（避免右窗长时间白屏）
await Promise.all([login(A.page, ACCOUNTS.teacher), login(B.page, ACCOUNTS.teacher3)]);
await Promise.all([
  gotoApp(A.page, '/learning/studios', 3000),
  gotoApp(B.page, '/learning/studios', 3000),
]);
await Promise.all([
  A.page.getByText('语文名师工作室', { exact: true }).first().click(),
  B.page.getByText('语文名师工作室', { exact: true }).first().click(),
]);
await sleep(4500);
await hold(A.page, 4000);

// A：找到待审核资料并「通过并发布」
const publish = A.page.locator('button', { hasText: '通过并发布' }).first();
await publish.waitFor({ state: 'visible', timeout: 20000 });
await publish.scrollIntoViewIfNeeded();
await sleep(1500);
await publish.click();
await sleep(4000);
await hold(A.page, 9000);

// B：刷新 → 看到新发布
await B.page.reload({ waitUntil: 'domcontentloaded' });
await sleep(6000);
await hold(B.page, 18000);

// B：点「预览」看已发布资料
const previewBtn = B.page.locator('button', { hasText: '预览' }).first();
if (await previewBtn.count()) {
  await previewBtn.click();
  await sleep(4000);
  await hold(B.page, 14000);
}
await hold(A.page, 12000);

const outA = await saveVideo(A.ctx, A.page, 's05-a');
const outB = await saveVideo(B.ctx, B.page, 's05-b');
console.log('saved:', outA, outB);
await b.close();
