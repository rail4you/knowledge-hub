// ═══════════════════════════════════════════════════════════════
// 学生端导航元数据（唯一来源）
// ───────────────────────────────────────────────────────────────
// 改造前：侧边栏 navGroups、顶栏 allItems、游客 guestNavItems 三份手写
// 导航表 + 18 处从未被读取的 route.data，每加一个页面要改 4 处。
//
// 改造后：本文件是唯一来源。
//   · 路由表通过 data: STUDENT_NAV.xxx 引用本文件
//   · 侧边栏分组 / 移动抽屉 / 游客导航 全部由本文件派生
//   · 加页面 = 加一条路由 + 在本文件加一条元数据，其余自动生效
//
// 字段说明：
//   group        侧边栏分组标题；不填则不进侧边栏
//   order        侧边栏组内排序（小→大）
//   guest        游客可见（未登录时的「公开浏览」导航）
//   mobile       移动端抽屉位置：top=顶层项 / child=收进 mobileParent 组
//   mobileOrder  移动端排序（默认取 order）
//   mobileParent 移动端父项 key（见 MOBILE_GROUPS）
// ═══════════════════════════════════════════════════════════════

export interface StudentNavMeta {
  /** 菜单文案（同时作为面包屑标题） */
  name: string;
  /** ng-zorro 图标名 */
  icon: string;
  /** 侧边栏分组标题 */
  group?: string;
  /** 侧边栏组内排序 */
  order?: number;
  /** 游客可见 */
  guest?: boolean;
  /** 移动端抽屉位置 */
  mobile?: 'top' | 'child';
  /** 移动端排序 */
  mobileOrder?: number;
  /** 移动端父项 key */
  mobileParent?: string;
}

export interface StudentNavEntry {
  key: string;
  label: string;
  icon: string;
  route?: string;
  children?: StudentNavEntry[];
}

export interface StudentNavGroup {
  label: string;
  items: StudentNavEntry[];
}

/**
 * 移动端抽屉里的「虚拟分组」——它们本身不是页面，
 * 只是把若干子页面收成一组（点进去是二级列表）。
 */
export const MOBILE_GROUPS: Record<string, { label: string; icon: string }> = {
  practicums: { label: '实训', icon: 'experiment' },
  employment: { label: '就业服务', icon: 'idcard' },
  ai: { label: 'AI 功能', icon: 'robot' },
};

/**
 * 学生端导航元数据表。
 * key 与 student.routes.ts 中的路由 path 一一对应。
 *
 * 注意类型写法：先用 satisfies 校验字面量 key，再用 Record<..., StudentNavMeta>
 * 统一值的类型 —— 否则 TS 会把值推断成各不相同的字面量联合，
 * 读可选字段（group / guest / mobile…）时会报 "Property does not exist"。
 */
const STUDENT_NAV_RAW = {
  dashboard: { name: '学习工作台', icon: 'appstore', group: '学习空间', order: 10 },
  courses: {
    name: '课程中心',
    icon: 'read',
    group: '学习空间',
    order: 20,
    guest: true,
    mobile: 'top',
    mobileOrder: 3,
  },
  myLearning: {
    name: '我的学习',
    icon: 'line-chart',
    group: '学习空间',
    order: 30,
    mobile: 'top',
    mobileOrder: 6,
  },
  analysis: { name: '智学分析', icon: 'exclamation-circle', group: '学习空间', order: 40 },
  resources: {
    name: '资源库',
    icon: 'database',
    group: '学习空间',
    order: 50,
    guest: true,
    mobile: 'top',
    mobileOrder: 4,
  },
  microMajors: {
    name: '微专业',
    icon: 'trophy',
    group: '学习空间',
    order: 60,
    guest: true,
    mobile: 'top',
    mobileOrder: 1,
  },
  myMicroMajors: {
    name: '我的微专业',
    icon: 'audit',
    group: '学习空间',
    order: 70,
    mobile: 'top',
    mobileOrder: 2,
  },
  myCertificates: { name: '我的证书', icon: 'safety-certificate', group: '学习空间', order: 80 },
  agentTasks: {
    name: '课堂任务',
    icon: 'robot',
    group: '学习空间',
    order: 90,
    mobile: 'top',
    mobileOrder: 7,
  },
  practicums: {
    name: '实训项目',
    icon: 'experiment',
    group: '学习空间',
    order: 100,
    guest: true,
    mobile: 'child',
    mobileParent: 'practicums',
  },
  myPracticums: {
    name: '我的实训',
    icon: 'unordered-list',
    group: '学习空间',
    order: 110,
    mobile: 'child',
    mobileParent: 'practicums',
  },
  wasmCenter: {
    name: '仿真实训',
    icon: 'play-circle',
    group: '学习空间',
    order: 105,
    mobile: 'child',
    mobileParent: 'practicums',
  },
  aiChat: {
    name: 'AI 学习助手',
    icon: 'robot',
    group: '学习空间',
    order: 120,
    mobile: 'child',
    mobileParent: 'ai',
  },
  search: {
    name: '智能搜索',
    icon: 'search',
    group: '学习空间',
    order: 130,
    mobile: 'child',
    mobileParent: 'ai',
  },
  employmentJobs: {
    name: '就业中心',
    icon: 'idcard',
    group: '成长与服务',
    order: 10,
    guest: true,
    mobile: 'child',
    mobileParent: 'employment',
  },
  employmentResumes: {
    name: '我的简历',
    icon: 'file-text',
    group: '成长与服务',
    order: 20,
    mobile: 'child',
    mobileParent: 'employment',
  },
  employmentApplications: {
    name: '我的投递',
    icon: 'send',
    group: '成长与服务',
    order: 30,
    mobile: 'child',
    mobileParent: 'employment',
  },
  employmentOutcomes: {
    name: '我的就业去向',
    icon: 'compass',
    group: '成长与服务',
    order: 40,
    mobile: 'child',
    mobileParent: 'employment',
  },
  employmentGuidance: {
    name: '就业指导',
    icon: 'message',
    group: '成长与服务',
    order: 50,
    mobile: 'child',
    mobileParent: 'employment',
  },
  recruitmentLive: {
    name: '招聘面试',
    icon: 'video-camera',
    group: '成长与服务',
    order: 60,
    mobile: 'child',
    mobileParent: 'employment',
  },
  profile: { name: '个人中心', icon: 'user', group: '成长与服务', order: 70 },
  news: {
    name: '资讯中心',
    icon: 'file-text',
    group: '成长与服务',
    order: 80,
    guest: true,
    mobile: 'top',
    mobileOrder: 5,
  },

  // ── 以下条目不进侧边栏 / 移动抽屉，只用于面包屑标题 ──────────
  practice: { name: '专项练习', icon: 'edit' },
  favorites: { name: '我的收藏', icon: 'star' },
  specialEduResources: { name: '特教资源', icon: 'heart' },
  myIep: { name: '我的 IEP', icon: 'user' },
} satisfies Record<string, StudentNavMeta>;

export const STUDENT_NAV: Record<keyof typeof STUDENT_NAV_RAW, StudentNavMeta> = STUDENT_NAV_RAW;

/**
 * 元数据 key → 路由绝对路径。
 * 路由表与布局都依赖这张映射，改路由时只改这一处。
 */
export const STUDENT_NAV_ROUTES: Record<keyof typeof STUDENT_NAV, string> = {
  dashboard: '/student/dashboard',
  courses: '/student/courses',
  myLearning: '/student/my-learning',
  analysis: '/student/analysis',
  resources: '/student/resources',
  microMajors: '/student/micro-majors',
  myMicroMajors: '/student/my-micro-majors',
  myCertificates: '/student/my-certificates',
  agentTasks: '/student/agent-tasks',
  practicums: '/student/practicums',
  myPracticums: '/student/practicums/my',
  wasmCenter: '/student/wasm-center',
  aiChat: '/student/ai/chat',
  search: '/student/search',
  employmentJobs: '/student/employment/jobs',
  employmentResumes: '/student/employment/my-resumes',
  employmentApplications: '/student/employment/my-applications',
  employmentOutcomes: '/student/employment/my-outcomes',
  employmentGuidance: '/student/employment/guidance',
  recruitmentLive: '/student/recruitment-live',
  profile: '/student/profile',
  news: '/student/news',
  practice: '/student/practice',
  favorites: '/student/favorites',
  specialEduResources: '/student/special-edu/resources',
  myIep: '/student/special-edu/my-iep',
};

type NavKey = keyof typeof STUDENT_NAV;

function toEntry(key: NavKey): StudentNavEntry {
  const meta = STUDENT_NAV[key];
  return {
    key,
    label: meta.name,
    icon: meta.icon,
    route: STUDENT_NAV_ROUTES[key],
  };
}

/** 侧边栏分组（按 group 名称顺序出现，组内按 order） */
export function buildSidebarGroups(): StudentNavGroup[] {
  const groups = new Map<string, StudentNavEntry[]>();
  (Object.keys(STUDENT_NAV) as NavKey[]).forEach((key) => {
    const meta = STUDENT_NAV[key];
    if (!meta.group) return;
    const list = groups.get(meta.group) ?? [];
    list.push(toEntry(key));
    groups.set(meta.group, list);
  });

  return [...groups.entries()].map(([label, items]) => ({
    label,
    items: items.sort((a, b) => (STUDENT_NAV[a.key as NavKey].order ?? 0) - (STUDENT_NAV[b.key as NavKey].order ?? 0)),
  }));
}

/** 游客可见的公开浏览导航 */
export function buildGuestItems(): StudentNavEntry[] {
  return (Object.keys(STUDENT_NAV) as NavKey[])
    .filter((key) => STUDENT_NAV[key].guest)
    .map(toEntry);
}

/** 移动端抽屉的功能导航（顶层项 + 虚拟分组，子项收在分组里） */
export function buildMobileItems(): StudentNavEntry[] {
  const tops: StudentNavEntry[] = (Object.keys(STUDENT_NAV) as NavKey[])
    .filter((key) => STUDENT_NAV[key].mobile === 'top')
    .map(toEntry)
    .sort(
      (a, b) =>
        (STUDENT_NAV[a.key as NavKey].mobileOrder ?? STUDENT_NAV[a.key as NavKey].order ?? 0) -
        (STUDENT_NAV[b.key as NavKey].mobileOrder ?? STUDENT_NAV[b.key as NavKey].order ?? 0),
    );

  const groupChildren = new Map<string, StudentNavEntry[]>();
  (Object.keys(STUDENT_NAV) as NavKey[]).forEach((key) => {
    const meta = STUDENT_NAV[key];
    if (meta.mobile !== 'child' || !meta.mobileParent) return;
    const list = groupChildren.get(meta.mobileParent) ?? [];
    list.push(toEntry(key));
    groupChildren.set(meta.mobileParent, list);
  });

  // 虚拟分组按其在顶部排序中的位置插入：实训 / 就业服务 / AI 功能
  const groupOrder = ['practicums', 'employment', 'ai'];
  const virtual: StudentNavEntry[] = groupOrder
    .filter((g) => (groupChildren.get(g)?.length ?? 0) > 0)
    .map((g) => ({
      key: g,
      label: MOBILE_GROUPS[g].label,
      icon: MOBILE_GROUPS[g].icon,
      children: groupChildren.get(g),
    }));

  // 合并：保持与原实现一致的顺序 —— 微专业/我的微专业/课程中心/资源库/资讯中心/
  // 我的学习/课堂任务/实训/就业服务/AI 功能
  const ordered: StudentNavEntry[] = [];
  const pushVirtual = (g: string) => {
    const v = virtual.find(x => x.key === g);
    if (v) ordered.push(v);
  };
  const topOf = (k: NavKey) => tops.find(t => t.key === k);

  const seq: (NavKey | 'g-practicums' | 'g-employment' | 'g-ai')[] = [
    'microMajors',
    'myMicroMajors',
    'courses',
    'resources',
    'news',
    'myLearning',
    'agentTasks',
    'g-practicums',
    'g-employment',
    'g-ai',
  ];

  seq.forEach(s => {
    if (s === 'g-practicums') return pushVirtual('practicums');
    if (s === 'g-employment') return pushVirtual('employment');
    if (s === 'g-ai') return pushVirtual('ai');
    const t = topOf(s);
    if (t) ordered.push(t);
  });

  // 兜底：把上面序列没覆盖到的顶层项补到末尾，避免新增页面被漏掉
  tops.forEach(t => {
    if (!ordered.some(o => o.key === t.key)) ordered.push(t);
  });

  return ordered;
}

/** 全部可导航页面（用于面包屑标题匹配与「最具体路由」高亮） */
export function buildAllEntries(): StudentNavEntry[] {
  return (Object.keys(STUDENT_NAV) as NavKey[]).map(toEntry);
}
