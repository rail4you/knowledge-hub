/**
 * 场景 s01 · 平台总览
 * 门户首页（公开） → 登录后的角色化工作台 → 资源中心（带侧边导航）
 */
import { launch, newRecordingContext, saveVideo, login, ACCOUNTS, sleep } from '../lib/browser.mjs';
import { gotoApp, hold, slowScroll, slowHover } from '../lib/scene.mjs';

const b = await launch();
const { ctx, page } = await newRecordingContext(b, 's01-overview');

// 1) 门户首页（未登录，公开门户）
await page.goto('https://www.iketong.com/', { waitUntil: 'domcontentloaded', timeout: 90000 });
await sleep(7000);
await slowScroll(page, 350);
await hold(page, 4000);

// 2) 登录 → 角色化工作台
await login(page, ACCOUNTS.teacher);
await hold(page, 8000);
await slowScroll(page, 300);
await hold(page, 5000);
await slowScroll(page, 400);
await hold(page, 5000);

// 3) 资源中心（含左侧导航）
await gotoApp(page, '/resources', 5000);
await hold(page, 8000);
await slowScroll(page, 400);
await hold(page, 6000);
await page.mouse.move(960, 540, { steps: 12 });
await hold(page, 7000);
await slowScroll(page, 500);
await hold(page, 5000);

const out = await saveVideo(ctx, page, 's01-overview');
console.log('saved:', out);
await b.close();
