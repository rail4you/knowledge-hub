/**
 * 页面顶部数据总览（stat）的持久化快照缓存。
 *
 * 背景：课程/资源库/资讯/我的学习等页面的统计数值是异步加载的，
 * 每次进入页面时信号先表现为 0、数据返回后再跳到真实值，产生「从 0 闪变到实际数值」的观感。
 * 这些数据多为「读多写少」、短时间基本不变，因此把上次看到的结果按页面缓存下来：
 * - 进入页面加载期间先回显缓存快照（消除 0 闪变）；
 * - 真实数据返回后（任意一项非 0）覆盖并刷新缓存。
 *
 * 用 localStorage 持久化，并带 TTL 防止展示过于陈旧的数据。
 */

export interface StatSnapshotItem {
  label: string;
  value: number;
  suffix?: string;
  icon?: string;
  color?: string;
}

const STORAGE_KEY = 'kh-student-stat-snapshots-v1';
/** 快照有效时长：超时后不再回显旧值（避免跨日展示陈旧数据）。 */
const SNAPSHOT_TTL_MS = 6 * 60 * 60 * 1000; // 6 小时
/** 防膨胀：最多保留的独立页面快照数。 */
const MAX_SNAPSHOTS = 40;

interface StatSnapshot {
  savedAt: number;
  entries: StatSnapshotItem[];
}

function readStore(): Record<string, StatSnapshot> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw);
    if (parsed && typeof parsed === 'object') return parsed as Record<string, StatSnapshot>;
    return {};
  } catch {
    return {};
  }
}

function writeStore(store: Record<string, StatSnapshot>): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(store));
  } catch {
    /* 存储不可用（隐私模式/超限）时静默忽略，不影响预览 */
  }
}

/** 读取某页面的统计快照；不存在、过期或数据异常时返回 null。 */
export function loadStatSnapshot(pageKey: string): StatSnapshotItem[] | null {
  if (!pageKey) return null;
  const store = readStore();
  const snap = store[pageKey];
  if (!snap || !Array.isArray(snap.entries) || snap.entries.length === 0) return null;
  if (Date.now() - snap.savedAt > SNAPSHOT_TTL_MS) return null;
  return snap.entries;
}

/** 保存某页面的统计快照。 */
export function saveStatSnapshot(pageKey: string, stats: StatSnapshotItem[]): void {
  if (!pageKey || !Array.isArray(stats) || stats.length === 0) return;
  const store = readStore();
  store[pageKey] = { savedAt: Date.now(), entries: stats };
  // 防膨胀：条目过多时清理最旧的
  const keys = Object.keys(store);
  if (keys.length > MAX_SNAPSHOTS) {
    keys.sort((a, b) => (store[a].savedAt || 0) - (store[b].savedAt || 0));
    keys.slice(0, keys.length - MAX_SNAPSHOTS).forEach((k) => delete store[k]);
  }
  writeStore(store);
}