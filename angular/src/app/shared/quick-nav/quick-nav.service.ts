import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

/**
 * 快速导航（Command Palette）数据项。
 * - `group`：父模块名（用于分组展示与按组名匹配）；
 * - `pinyin`：完整拼音（小写，空格分词），例如 "ziyuan ku"；
 * - `acronym`：首字母缩写（小写，无空格），例如 "zyk"。
 * 匹配时对 `group + label + pinyin + acronym + description + route` 做不区分大小写的子串匹配。
 */
export interface QuickNavItem {
  group: string;
  groupIcon: string;
  label: string;
  route: string;
  description: string;
  pinyin: string;
  acronym: string;
}

/** 全局导航表：覆盖资源、AI、课程、就业、实训、资讯、用户、租户/设置、评估等后台入口。 */
const QUICK_NAV_ITEMS: QuickNavItem[] = [
  // ── 资源管理 ─────────────────────────────────────────────
  { group: '资源管理',       groupIcon: 'folder-open',  label: '资源库',         route: '/resources',                          description: '资源列表与审核',           pinyin: 'ziyuan ku',          acronym: 'zyk'  },
  { group: '资源管理',       groupIcon: 'folder-open',  label: '我的收藏',       route: '/favorites',                          description: '收藏的资源',             pinyin: 'wode shoucang',      acronym: 'wdsc' },
  { group: '资源管理',       groupIcon: 'folder-open',  label: '文档搜索',       route: '/search',                             description: '全文搜索',               pinyin: 'wendang sousuo',     acronym: 'wdss' },
  { group: '资源管理',       groupIcon: 'folder-open',  label: '搜索历史',       route: '/my/search-history',                  description: '我的搜索记录',           pinyin: 'sousuo lishi',       acronym: 'ssls' },
  { group: '资源管理',       groupIcon: 'folder-open',  label: '检索统计',       route: '/admin/search-statistics',            description: '检索量与活跃用户',       pinyin: 'jiansuo tongji',     acronym: 'jst'  },
  { group: '资源管理',       groupIcon: 'folder-open',  label: '资源进度',       route: '/admin/resource-progress',            description: '资源处理进度',           pinyin: 'ziyuan jindu',       acronym: 'zyjd' },
  { group: '资源管理',       groupIcon: 'folder-open',  label: '资源任务',       route: '/admin/resource-tasks',               description: '媒体 / 索引 / 视频任务',  pinyin: 'ziyuan renwu',       acronym: 'zyrw' },
  { group: '资源管理',       groupIcon: 'folder-open',  label: 'Meilisearch',    route: '/admin/meilisearch',                  description: '搜索引擎仪表盘',         pinyin: 'meilisearch',        acronym: 'mls'  },

  // ── AI 管理 ───────────────────────────────────────────────
  { group: 'AI 管理',        groupIcon: 'robot',        label: 'AI 对话',        route: '/ai/chat',                            description: '智能对话',               pinyin: 'ai duihua',          acronym: 'aidh' },
  { group: 'AI 管理',        groupIcon: 'robot',        label: '教案生成',       route: '/ai/lesson-plan',                     description: '智能备课',               pinyin: 'jiaoan shengcheng',  acronym: 'jasc' },
  { group: 'AI 管理',        groupIcon: 'robot',        label: '案例分析',       route: '/ai/case-analysis',                   description: '智能案例',               pinyin: 'anli fenxi',         acronym: 'alfx' },
  { group: 'AI 管理',        groupIcon: 'robot',        label: '职业规划',       route: '/ai/career-guidance',                 description: 'AI 职业指导',            pinyin: 'zhiye guihua',       acronym: 'zygh' },
  { group: 'AI 管理',        groupIcon: 'robot',        label: '习题生成',       route: '/ai/exercise-generate',               description: 'AI 习题生成',            pinyin: 'xiti shengcheng',    acronym: 'xtsc' },
  { group: 'AI 管理',        groupIcon: 'robot',        label: '图片生成',       route: '/ai/image-generation',                description: 'AI 图片生成',            pinyin: 'tupian shengcheng',  acronym: 'tpsc' },
  { group: 'AI 管理',        groupIcon: 'robot',        label: '视频生成',       route: '/ai/video-generation',                description: 'AI 视频生成',            pinyin: 'shipin shengcheng',  acronym: 'spsc' },
  { group: 'AI 管理',        groupIcon: 'robot',        label: '模型管理',       route: '/ai/model-management',                description: 'AI 模型配置',            pinyin: 'moxing guanli',      acronym: 'mxgl' },
  { group: 'AI 管理',        groupIcon: 'robot',        label: 'AI 任务中心',    route: '/ai/tasks',                           description: 'AI 生成任务',            pinyin: 'ai renwu zhongxin',  acronym: 'airw' },
  { group: 'AI 管理',        groupIcon: 'robot',        label: '智能体管理',     route: '/teaching/agents',                    description: '智能体配置',             pinyin: 'zhinengti guanli',   acronym: 'zntgl' },
  { group: 'AI 管理',        groupIcon: 'robot',        label: '智能体任务',     route: '/teaching/agent-tasks',               description: '智能体任务分配',         pinyin: 'zhinengti renwu',    acronym: 'zntrw' },

  // ── 专业和课程 ───────────────────────────────────────────
  { group: '专业和课程',     groupIcon: 'read',         label: '课程列表',       route: '/learning/course-list',               description: '课程总览',               pinyin: 'kecheng liebiao',    acronym: 'kclb' },
  { group: '专业和课程',     groupIcon: 'read',         label: '学生选课',       route: '/learning/student-enrollment',        description: '学生选课管理',           pinyin: 'xuesheng xuanke',    acronym: 'xsxk' },
  { group: '专业和课程',     groupIcon: 'read',         label: '章节管理',       route: '/learning/chapter-management',        description: '课程章节',               pinyin: 'zhangjie guanli',    acronym: 'zjgl' },
  { group: '专业和课程',     groupIcon: 'read',         label: '习题管理',       route: '/learning/exercise-management',       description: '课程习题',               pinyin: 'xiti guanli',        acronym: 'xtgl' },
  { group: '专业和课程',     groupIcon: 'read',         label: '章节习题',       route: '/learning/chapter-exercise',          description: '章节关联习题',           pinyin: 'zhangjie xiti',      acronym: 'zjxt' },
  { group: '专业和课程',     groupIcon: 'read',         label: '课程资源',       route: '/learning/course-resource',           description: '课程资源',               pinyin: 'kecheng ziyuan',     acronym: 'kczy' },
  { group: '专业和课程',     groupIcon: 'read',         label: '章节资源',       route: '/learning/chapter-resource',          description: '章节资源',               pinyin: 'zhangjie ziyuan',    acronym: 'zjzy' },
  { group: '专业和课程',     groupIcon: 'read',         label: '学习统计',       route: '/learning/learning-statistics',       description: '学习数据',               pinyin: 'xuexi tongji',       acronym: 'xxtj' },
  { group: '专业和课程',     groupIcon: 'read',         label: '微专业',         route: '/micro-majors',                       description: '微专业总览',             pinyin: 'wei zhuanye',        acronym: 'wzy'  },
  { group: '专业和课程',     groupIcon: 'read',         label: '微专业管理',     route: '/admin/micro-majors',                 description: '微专业后台',             pinyin: 'wei zhuanye guanli', acronym: 'wzygl' },
  { group: '专业和课程',     groupIcon: 'read',         label: '专业管理',       route: '/admin/majors',                       description: '专业后台',               pinyin: 'zhuanye guanli',     acronym: 'zygl' },
  { group: '专业和课程',     groupIcon: 'read',         label: '院校信息',       route: '/admin/tenant-info',                  description: '本院校信息',             pinyin: 'yuanxiao xinxi',     acronym: 'yxxx' },

  // ── 就业管理 ─────────────────────────────────────────────
  { group: '就业管理',       groupIcon: 'idcard',       label: '职位管理',       route: '/admin/employment/jobs',              description: '招聘职位',               pinyin: 'zhiwei guanli',      acronym: 'zwgl' },
  { group: '就业管理',       groupIcon: 'idcard',       label: '面试管理',       route: '/admin/employment/interviews',        description: '面试安排',               pinyin: 'mianshi guanli',     acronym: 'msgl' },
  { group: '就业管理',       groupIcon: 'idcard',       label: '就业统计',       route: '/admin/employment/statistics',        description: '就业数据',               pinyin: 'jiuye tongji',       acronym: 'jytj' },
  { group: '就业管理',       groupIcon: 'idcard',       label: '就业成果',       route: '/admin/employment/outcomes',          description: '就业成果管理',           pinyin: 'jiuye chengguo',     acronym: 'jycg' },
  { group: '就业管理',       groupIcon: 'idcard',       label: '招聘直播',       route: '/admin/recruitment-live',             description: '直播招聘',               pinyin: 'zhaopin zhibo',      acronym: 'zpzb' },
  { group: '就业管理',       groupIcon: 'idcard',       label: '就业指导',       route: '/employment/my-guidance',             description: '我的就业指导',           pinyin: 'jiuye zhidao',       acronym: 'jyzd' },

  // ── 实训 ─────────────────────────────────────────────────
  { group: '实训',           groupIcon: 'experiment',   label: '实训项目',       route: '/admin/practicum/projects',           description: '实训项目管理',           pinyin: 'shixun xiangmu',     acronym: 'sxxm' },
  { group: '实训',           groupIcon: 'experiment',   label: '实训任务',       route: '/admin/practicum/tasks',              description: '实训任务',               pinyin: 'shixun renwu',       acronym: 'sxrw' },
  { group: '实训',           groupIcon: 'experiment',   label: '仿真实训',       route: '/admin/practicum/simulations',        description: '仿真系统',               pinyin: 'fangzhen shixun',    acronym: 'fzsx' },
  { group: '实训',           groupIcon: 'experiment',   label: '智能体聊天',     route: '/admin/practicum/chat',               description: '实训智能体',             pinyin: 'zhinengti liaotian', acronym: 'zntlt' },

  // ── 资讯 ─────────────────────────────────────────────────
  { group: '资讯管理',       groupIcon: 'file-text',    label: '资讯管理',       route: '/admin/news',                         description: '后台资讯',               pinyin: 'zixun guanli',       acronym: 'zxg'  },

  // ── 用户 ─────────────────────────────────────────────────
  { group: '用户管理',       groupIcon: 'team',         label: '用户管理',       route: '/identity/users',                     description: '用户列表',               pinyin: 'yonghu guanli',      acronym: 'yhgl' },
  { group: '用户管理',       groupIcon: 'team',         label: '角色管理',       route: '/identity/roles',                     description: '角色与权限',             pinyin: 'jiaose guanli',      acronym: 'jsgl' },
  { group: '用户管理',       groupIcon: 'team',         label: '用户导入',       route: '/identity/users/import',              description: '批量导入用户',           pinyin: 'yonghu daoru',       acronym: 'yhdr' },

  // ── 租户与设置 ───────────────────────────────────────────
  { group: '租户与设置',     groupIcon: 'setting',      label: '租户管理',       route: '/multi-school-admin',                 description: '多租户管理',             pinyin: 'zuhu guanli',        acronym: 'zhgl' },
  { group: '租户与设置',     groupIcon: 'setting',      label: '版本管理',       route: '/admin/edition',                      description: '版本与套餐',             pinyin: 'banben guanli',      acronym: 'bbgl' },
  { group: '租户与设置',     groupIcon: 'setting',      label: '联盟管理',       route: '/admin/alliance',                     description: '联盟设置',               pinyin: 'lianmeng guanli',    acronym: 'lmgl' },
  { group: '租户与设置',     groupIcon: 'setting',      label: '品牌定制',       route: '/admin/branding',                     description: 'LOGO 与品牌',            pinyin: 'pinpai dingzhi',     acronym: 'ppdz' },
  { group: '租户与设置',     groupIcon: 'setting',      label: '账户有效期',     route: '/admin/account-validity',             description: '账户到期管理',           pinyin: 'zhanghu youxiaoqi',  acronym: 'zhyxq' },
  { group: '租户与设置',     groupIcon: 'setting',      label: '专项教育',       route: '/admin/special-education',            description: '特教后台',               pinyin: 'zhuanxiang jiaoyu',  acronym: 'zxjy' },
  { group: '租户与设置',     groupIcon: 'setting',      label: '语音助手',       route: '/admin/voice-assistant',              description: '语音助手配置',           pinyin: 'yuyin zhushou',      acronym: 'yyzs' },

  // ── 评估与认证 ───────────────────────────────────────────
  { group: '评估与认证',     groupIcon: 'safety-certificate', label: '双高项目库', route: '/assessment/double-high/projects',   description: '双高项目',               pinyin: 'shuanggao xiangmuku', acronym: 'sgxm' },
  { group: '评估与认证',     groupIcon: 'safety-certificate', label: '双高报告中心', route: '/assessment/double-high/report-center', description: '双高报告',             pinyin: 'shuanggao baogao',  acronym: 'sgbg' },

  // ── 工作台 ──────────────────────────────────────────────
  { group: '工作台',         groupIcon: 'dashboard',    label: '系统工作台',     route: '/admin/workbench',                    description: '管理端首页',             pinyin: 'xitong gongzuo',     acronym: 'xtgz' },
];

/**
 * 全局快速导航服务（Command Palette）。
 * 单一实例由根组件 `QuickNavComponent` 订阅，状态以 signal 形式暴露给任意调用方。
 */
@Injectable({ providedIn: 'root' })
export class QuickNavService {
  private readonly router = inject(Router);

  readonly open = signal(false);
  readonly search = signal('');
  readonly index = signal(0);

  readonly items = QUICK_NAV_ITEMS;

  /**
   * 按 `search` 过滤后的项，按相关度从高到低排序。
   * 评分规则：
   *   - 拼音起始 / 词首 100 / 90
   *   - 缩写起始 80
   *   - 中文起始 70
   *   - 任意子串 50
   * 输入为空时按 group 顺序原样返回，便于用户扫读。
   */
  readonly filtered = computed<{ item: QuickNavItem; score: number }[]>(() => {
    const q = this.search().trim().toLowerCase();
    if (!q) {
      return this.items.map((item, idx) => ({ item, score: -idx }));
    }
    const ranked: { item: QuickNavItem; score: number }[] = [];
    for (let idx = 0; idx < this.items.length; idx++) {
      const item = this.items[idx];
      const pinyin = item.pinyin.toLowerCase();
      const acro = item.acronym.toLowerCase();
      const label = item.label.toLowerCase();
      const group = item.group.toLowerCase();
      const haystack = `${pinyin} ${acro} ${label} ${group} ${item.description} ${item.route}`.toLowerCase();

      let score = -Infinity;
      if (pinyin.startsWith(q)) {
        score = Math.max(score, 100);
      } else if (pinyin.split(/\s+/).some(w => w.startsWith(q))) {
        score = Math.max(score, 90);
      } else if (acro.startsWith(q)) {
        score = Math.max(score, 85);
      } else if (group.startsWith(q) || label.startsWith(q)) {
        score = Math.max(score, 70);
      } else if (haystack.includes(q)) {
        score = Math.max(score, 50);
      }
      if (score > -Infinity) {
        ranked.push({ item, score: score - idx * 0.001 });
      }
    }
    ranked.sort((a, b) => b.score - a.score);
    return ranked;
  });

  toggle(): void {
    if (this.open()) {
      this.close();
    } else {
      this.show();
    }
  }

  show(): void {
    this.open.set(true);
    this.search.set('');
    this.index.set(0);
  }

  close(): void {
    this.open.set(false);
    this.search.set('');
    this.index.set(0);
  }

  /** 跳转到指定项并关闭面板。 */
  goTo(item: QuickNavItem): void {
    if (!item?.route) {
      return;
    }
    this.close();
    this.router.navigateByUrl(item.route);
  }

  /** 处理面板内的键盘事件：↑/↓ 调整选中，Enter 跳转。 */
  handleKeydown(event: KeyboardEvent, list: { item: QuickNavItem }[]): void {
    if (!list.length) {
      return;
    }
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.index.update(i => Math.min(i + 1, list.length - 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.index.update(i => Math.max(i - 1, 0));
    } else if (event.key === 'Enter') {
      const target = list[this.index()] ?? list[0];
      if (target) {
        event.preventDefault();
        this.goTo(target.item);
      }
    } else if (event.key === 'Home') {
      event.preventDefault();
      this.index.set(0);
    } else if (event.key === 'End') {
      event.preventDefault();
      this.index.set(list.length - 1);
    }
  }
}