import { chromium } from 'playwright-core';
import fs from 'node:fs';
import path from 'node:path';

export const SITE = 'https://www.iketong.com';
export const TENANT = '启迪学校';
export const OUT = path.resolve('out');

export const ACCOUNTS = {
  admin: { user: 'qidi-admin', pass: '123456' },
  teacher: { user: 'wwq', pass: '123456' },
  teacher2: { user: 'ohw', pass: '123456' },
  teacher3: { user: 'ssw', pass: '123456' },
};

export const VIDEO_SIZE = { width: 1920, height: 1080 };

export async function launch({ headless = true } = {}) {
  return chromium.launch({
    channel: 'chrome',
    headless,
    args: ['--autoplay-policy=no-user-gesture-required', '--force-device-scale-factor=1'],
  });
}

/** 创建一个带录屏的上下文；返回 { ctx, page, videoPath } */
export async function newRecordingContext(browser, name, { size = VIDEO_SIZE, headless = true } = {}) {
  const dir = path.join(OUT, 'rawtmp');
  fs.mkdirSync(dir, { recursive: true });
  const ctx = await browser.newContext({
    viewport: size,
    deviceScaleFactor: 1,
    locale: 'zh-CN',
    timezoneId: 'Asia/Shanghai',
    recordVideo: { dir, size },
  });
  const page = await ctx.newPage();
  return { ctx, page };
}

/** 结束后保存视频到 out/raw/<name>.webm */
export async function saveVideo(ctx, page, name) {
  const video = page.video();
  await ctx.close();
  const dir = path.join(OUT, 'raw');
  fs.mkdirSync(dir, { recursive: true });
  const target = path.join(dir, `${name}.webm`);
  if (video) {
    await video.saveAs(target);
  }
  return target;
}

export async function login(page, account, { tenant = TENANT, attempts = 2 } = {}) {
  for (let attempt = 1; attempt <= attempts; attempt++) {
    await page.goto(`${SITE}/account/login`, { waitUntil: 'domcontentloaded', timeout: 90000 });
    await page.locator('#login-username').waitFor({ state: 'visible', timeout: 30000 });
    await sleep(1200);

    // 机构选择（登录页内联列出机构按钮）：等列表加载出来再选，选完校验
    for (let i = 0; i < 3; i++) {
      const trigger = page.locator('button.tenant-trigger');
      const current = (await trigger.innerText().catch(() => '')).trim();
      if (current.includes(tenant)) break;
      await trigger.click();
      await sleep(1000);
      const opt = page.locator('button.tenant-option', { hasText: tenant }).first();
      const found = await opt.waitFor({ state: 'visible', timeout: 8000 }).then(() => true).catch(() => false);
      if (found) {
        await opt.click();
      } else {
        await page.keyboard.press('Escape');
      }
      await sleep(900);
    }

    await page.locator('#login-username').fill(account.user);
    await page.locator('#login-password').fill(account.pass);
    await page.locator('button.submit-btn').click();

    // 等离开登录页（登录失败会留在 /account/login）
    const ok = await page.waitForFunction(() => !location.pathname.includes('/account/login'), null, { timeout: 25000 })
      .then(() => true).catch(() => false);
    await sleep(3000);
    if (ok) return;
    console.log(`⚠️ 登录失败（第 ${attempt} 次），重试：${account.user}`);
  }
  throw new Error(`登录失败：${account.user}`);
}

export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** 取 access_token（用于直接调 API 校验元素位置等） */
export async function getToken(account, tenant = TENANT) {
  const res = await fetch(`${SITE}/connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'Abp-TenantId': tenantIdOf(tenant) },
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: 'KnowledgeHub_App',
      scope: 'offline_access KnowledgeHub',
      username: account.user,
      password: account.pass,
    }).toString(),
  });
  const json = await res.json();
  return json.access_token;
}

/** 租户 Id（启迪学校 = qidi） */
const TENANT_IDS = { 启迪学校: '3a207891-a06c-8876-b34d-32a245adec63' };
export function tenantIdOf(tenant) {
  return TENANT_IDS[tenant] || tenant;
}

/** 调白板详情（拿元素坐标） */
export async function fetchWhiteboard(boardId, token) {
  const res = await fetch(`${SITE}/api/app/whiteboard/${boardId}`, {
    headers: { Authorization: `Bearer ${token}`, 'Abp-TenantId': tenantIdOf(TENANT) },
  });
  return res.json();
}

/** 清理演示残留资源（同名资源删掉，便于反复录制） */
export async function deleteResourcesByName(name, token) {
  const list = await fetch(`${SITE}/api/app/resource?maxResultCount=100&skipCount=0`, {
    headers: { Authorization: `Bearer ${token}`, 'Abp-TenantId': tenantIdOf(TENANT) },
  }).then((r) => r.json());
  for (const r of list.items || []) {
    if (r.name === name) {
      await fetch(`${SITE}/api/app/resource/${r.id}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}`, 'Abp-TenantId': tenantIdOf(TENANT) },
      });
      console.log('cleaned resource:', r.name);
    }
  }
}

/** 等待页面网络安静 */
export async function settle(page, ms = 2500) {
  try {
    await page.waitForLoadState('networkidle', { timeout: 15000 });
  } catch {
    /* ignore */
  }
  await sleep(ms);
}
