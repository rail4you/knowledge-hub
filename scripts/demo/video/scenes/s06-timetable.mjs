/**
 * 场景 s06 · 互动课堂课表（周 / 日 / 月三视图 + 悬浮信息）
 */
import { launch, newRecordingContext, saveVideo, login, ACCOUNTS, sleep } from '../lib/browser.mjs';
import { gotoApp, hold, slowHover } from '../lib/scene.mjs';

const b = await launch();
const { ctx, page } = await newRecordingContext(b, 's06-timetable');

await login(page, ACCOUNTS.teacher);
await gotoApp(page, '/learning/classroom-timetable', 6000);
await hold(page, 5000);

// 悬浮课程块 → tooltip（课程名 / 日期 / 时间 / 学校）
const item = page.locator('.tt-item, .timetable-item, [class*="item"]').filter({ hasText: 'Pandas' }).first();
if (await item.count()) {
  await slowHover(page, item, 22, 60);
  await hold(page, 4000);
} else {
  console.log('未找到课程块选择器，改为全页扫描');
  const boxes = await page.evaluate(() => {
    const out = [];
    document.querySelectorAll('*').forEach((el) => {
      if (el.children.length === 0 && el.textContent && el.textContent.includes('Pandas')) {
        const r = el.getBoundingClientRect();
        if (r.width > 40 && r.height > 10) out.push({ x: r.x + r.width / 2, y: r.y + r.height / 2, t: el.textContent.trim().slice(0, 20), cls: el.className.toString().slice(0, 60) });
      }
    });
    return out.slice(0, 5);
  });
  console.log('候选:', JSON.stringify(boxes));
  if (boxes.length) {
    await page.mouse.move(boxes[0].x - 200, boxes[0].y - 100, { steps: 10 });
    await sleep(400);
    await page.mouse.move(boxes[0].x, boxes[0].y, { steps: 24 });
    await sleep(1200);
    await hold(page, 4500);
  }
}

// 切到「日」
await page.getByText('日', { exact: true }).first().click();
await sleep(4500);
await hold(page, 7000);

// 切到「月」
await page.getByText('月', { exact: true }).first().click();
await sleep(4500);
await hold(page, 7000);

// 回到「周」
await page.getByText('周', { exact: true }).first().click();
await sleep(4000);
await hold(page, 5000);

// 顶部课程筛选
const courseSelect = page.locator('nz-select').first();
if (await courseSelect.count()) {
  await courseSelect.click();
  await sleep(1800);
  await hold(page, 2500);
  await page.keyboard.press('Escape');
  await sleep(800);
}
await hold(page, 3000);

const out = await saveVideo(ctx, page, 's06-timetable');
console.log('saved:', out);
await b.close();
