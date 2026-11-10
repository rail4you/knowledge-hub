/**
 * 场景 s07 · 教师教研白板（创建 + 邀请共创）
 * 左窗 A：教师 wwq 新建白板 → 邀请教师
 * 右窗 B：教师 ohw 刷新列表 → 看到「被邀请」→ 进入房间
 */
import fs from 'node:fs';
import path from 'node:path';
import { launch, newRecordingContext, saveVideo, login, ACCOUNTS, sleep, OUT } from '../lib/browser.mjs';
import { gotoApp, hold } from '../lib/scene.mjs';

const b = await launch();
const A = await newRecordingContext(b, 's07-a');
const B = await newRecordingContext(b, 's07-b');

// 并行登录两个账号
await Promise.all([login(A.page, ACCOUNTS.teacher), login(B.page, ACCOUNTS.teacher2)]);
await Promise.all([
  gotoApp(A.page, '/learning/whiteboards', 4000),
  gotoApp(B.page, '/learning/whiteboards', 4000),
]);
await hold(A.page, 3000);

await A.page.getByText('新建白板', { exact: true }).first().click();
await sleep(3000);
const inputs = A.page.locator('.ant-modal-content input');
await inputs.nth(0).fill('《春》第二课时研讨');
await sleep(1200);
await inputs.nth(1).fill('语文 · 七年级');
await sleep(1200);
await A.page.locator('.ant-modal-content textarea').first().fill('本次教研围绕《春》第二课时的教学目标与朗读环节展开。');
await sleep(1500);
await hold(A.page, 2000);
await A.page.getByText('创建并进入', { exact: true }).first().click();
await sleep(6000);
await hold(A.page, 4000);

// 白板房间 URL → 记录 id 供 s08 复用
const url = A.page.url();
const boardId = url.split('/').pop();
console.log('whiteboard id =', boardId);
fs.mkdirSync(path.join(OUT, 'state'), { recursive: true });
fs.writeFileSync(path.join(OUT, 'state', 'whiteboard.json'), JSON.stringify({ id: boardId, url }, null, 2));

// 邀请教师共创
await A.page.getByText('邀请教师共创', { exact: true }).first().click();
await sleep(3500);
await hold(A.page, 3000);
const inviteRow = A.page.locator('.ant-modal-content label, .ant-modal-content .invite-row').filter({ hasText: '吴海媛' }).first();
if (await inviteRow.count()) {
  await inviteRow.click();
} else {
  const cb = A.page.locator('.ant-modal-content input[type="checkbox"]').first();
  await cb.click();
}
await sleep(2000);
await hold(A.page, 2000);
await A.page.getByText('发送邀请', { exact: true }).first().click();
await sleep(4000);
await hold(A.page, 8000);

// B：成员刷新列表 → 看到被邀请的白板 → 进入
await hold(B.page, 2000);
await B.page.reload({ waitUntil: 'domcontentloaded' });
await sleep(6000);
await hold(B.page, 4000);

const boardCard = B.page.getByText('《春》第二课时研讨', { exact: true }).first();
if (await boardCard.count()) {
  await boardCard.click();
  await sleep(6000);
  await hold(B.page, 16000);
}
await hold(A.page, 8000);

const outA = await saveVideo(A.ctx, A.page, 's07-a');
const outB = await saveVideo(B.ctx, B.page, 's07-b');
console.log('saved:', outA, outB);
await b.close();
