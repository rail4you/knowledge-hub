import { SITE, sleep, settle } from './browser.mjs';

/** 打开应用内路由并等待渲染稳定 */
export async function gotoApp(page, path, wait = 3500) {
  await page.goto(`${SITE}${path}`, { waitUntil: 'domcontentloaded', timeout: 90000 });
  await settle(page, wait);
}

/** 停留（保持录屏继续） */
export const hold = sleep;

/** 按可见文本点击（精确匹配优先） */
export async function clickText(page, text, { exact = true, nth = 0, timeout = 20000 } = {}) {
  const loc = page.getByText(text, { exact }).nth(nth);
  await loc.waitFor({ state: 'visible', timeout });
  await loc.click();
}

/** 在 nz-select 中按文本选择选项 */
export async function selectOption(page, selectLocator, optionText) {
  await selectLocator.click();
  await sleep(1200);
  const opt = page.locator('.ant-select-dropdown:visible .ant-select-item-option', { hasText: optionText }).first();
  await opt.waitFor({ state: 'visible', timeout: 15000 });
  await opt.click();
  await sleep(600);
}

/** 鼠标在元素上缓慢移动（让观众看清动作） */
export async function slowHover(page, locator, steps = 18, pause = 35) {
  const box = await locator.boundingBox();
  if (!box) return;
  const x = box.x + box.width / 2;
  const y = box.y + box.height / 2;
  await page.mouse.move(x - 120, y - 60, { steps: 8 });
  await sleep(300);
  await page.mouse.move(x, y, { steps });
  await sleep(pause);
}

/** 缓慢滚动 */
export async function slowScroll(page, dy = 400, steps = 24) {
  await page.mouse.wheel(0, dy);
  await sleep(1200);
}
