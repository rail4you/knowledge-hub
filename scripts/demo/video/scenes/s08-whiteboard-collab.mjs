/**
 * 场景 s08 · 教师教研白板（双人实时共创）
 * 左窗 A：教师 wwq → 加文本 → 改文本 → 拖动文本
 * 右窗 B：教师 ohw → 加便签 → 改便签；两端互相看到对方的编辑与光标
 */
import fs from 'node:fs';
import path from 'node:path';
import {
  launch, newRecordingContext, saveVideo, login, ACCOUNTS, sleep, OUT, getToken, fetchWhiteboard,
} from '../lib/browser.mjs';
import { hold } from '../lib/scene.mjs';

const state = JSON.parse(fs.readFileSync(path.join(OUT, 'state', 'whiteboard.json'), 'utf8'));
console.log('board:', state.url);

const b = await launch();
const A = await newRecordingContext(b, 's08-a');
const B = await newRecordingContext(b, 's08-b');

await Promise.all([login(A.page, ACCOUNTS.teacher), login(B.page, ACCOUNTS.teacher2)]);

await Promise.all([
  A.page.goto(state.url, { waitUntil: 'domcontentloaded', timeout: 90000 }),
  B.page.goto(state.url, { waitUntil: 'domcontentloaded', timeout: 90000 }),
]);
await sleep(10000);
await hold(A.page, 3000);
await hold(B.page, 3000);

// 1) A 添加文本
await A.page.getByText('文本', { exact: true }).first().click();
await sleep(2500);
await hold(A.page, 2500);
await hold(B.page, 2500);

// 2) A 在右侧面板改写文本
const ta = A.page.locator('.room__panel textarea').first();
if (await ta.count()) {
  await ta.fill('教学目标：体会比喻句的表达效果');
  await sleep(2500);
} else {
  console.log('⚠️ 未找到右侧属性面板 textarea');
}
await hold(A.page, 3000);
await hold(B.page, 3000);

// 3) 拖动文本（用 API 读真实坐标，保证拖到元素上）
const token = await getToken(ACCOUNTS.teacher);
let board = await fetchWhiteboard(state.id, token);
let el = (board.elements || []).map((e) => JSON.parse(e.json)).find((e) => e.type === 'text');
if (el) {
  const host = await A.page.locator('.room__stage').boundingBox();
  const from = { x: host.x + el.x + 12, y: host.y + el.y + 10 };
  console.log('拖动起点', from, '元素', el.x, el.y);
  await A.page.mouse.move(from.x - 150, from.y - 80, { steps: 12 });
  await sleep(600);
  await A.page.mouse.move(from.x, from.y, { steps: 20 });
  await sleep(800);
  await A.page.mouse.down();
  await sleep(400);
  await A.page.mouse.move(from.x + 90, from.y - 40, { steps: 24 });
  await sleep(600);
  await A.page.mouse.move(from.x + 180, from.y - 95, { steps: 24 });
  await sleep(800);
  await A.page.mouse.up();
  await sleep(2500);
} else {
  console.log('⚠️ API 未读到文本元素，跳过拖动');
}
await hold(A.page, 3000);
await hold(B.page, 3000);

// 4) B 新增便签并改内容
await B.page.getByText('便签', { exact: true }).first().click();
await sleep(2500);
const tb = B.page.locator('.room__panel textarea').first();
if (await tb.count()) {
  await tb.fill('建议增加朗读环节，落实语感训练');
  await sleep(2500);
}
await hold(A.page, 3500);
await hold(B.page, 3500);

// 5) 双向远程光标（鼠标在画布上缓慢移动）
const hostA = await A.page.locator('.room__stage').boundingBox();
await A.page.mouse.move(hostA.x + 200, hostA.y + 200, { steps: 30 });
await sleep(1200);
await A.page.mouse.move(hostA.x + 520, hostA.y + 320, { steps: 30 });
await sleep(1500);
const hostB = await B.page.locator('.room__stage').boundingBox();
await B.page.mouse.move(hostB.x + 700, hostB.y + 250, { steps: 30 });
await sleep(1500);
await B.page.mouse.move(hostB.x + 400, hostB.y + 420, { steps: 30 });
await sleep(1500);
await hold(A.page, 3000);
await hold(B.page, 3000);

// 6) A 从课程资源插入素材（视频通道：生产库里图片类型资源没有开放上传，见 README 已知缺口）
await A.page.getByText('从课程资源选视频', { exact: true }).first().click();
await sleep(5000);
await hold(A.page, 3000);
const item = A.page.locator('.picker__item').first();
if (await item.count()) {
  await item.click();
  await sleep(2000);
  await hold(A.page, 2000);
  const ok = A.page.getByText('插入到白板', { exact: true }).first();
  if (await ok.count()) await ok.click();
  await sleep(5000);
} else {
  console.log('⚠️ 素材选择器为空');
  await A.page.keyboard.press('Escape');
  await sleep(1500);
}
await hold(A.page, 4000);
await hold(B.page, 4000);

// 7) 保存后的持久化校验（API 看元素是否都在）
board = await fetchWhiteboard(state.id, token);
console.log('服务端元素数：', (board.elements || []).length);

const outA = await saveVideo(A.ctx, A.page, 's08-a');
const outB = await saveVideo(B.ctx, B.page, 's08-b');
console.log('saved:', outA, outB);
await b.close();
