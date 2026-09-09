import {
  ChangeDetectionStrategy, Component, OnInit, inject, signal, computed, AfterViewInit, OnDestroy,
} from '@angular/core';
import { CommonModule, ViewportScroller } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { SiteBrandComponent } from '../../shared/branding/site-brand.component';
import { SiteFooterComponent } from '../../shared/branding/site-footer.component';
import { LearningService } from '../../proxy/learning/learning.service';
import { TenantInfoService } from '../../proxy/tenant-infos/tenant-info.service';
import { PortalService } from '../../proxy/portal/portal.service';
import { CourseService } from '../../proxy/courses/course.service';
import type { TenantInfoDto, TenantKnowledgeGraphDto, TenantGraphNodeDto } from '../../proxy/tenant-infos/dtos/models';
import type { PortalHomeDataDto, CourseBriefDto } from '../../proxy/portal/models';
import type { CourseDetailDto, ChapterDto } from '../../proxy/courses/dtos/models';
import * as echarts from 'echarts/core';
import { GraphChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { TooltipComponent } from 'echarts/components';

echarts.use([GraphChart, CanvasRenderer, TooltipComponent]);

/** 节点视觉配置 — 小圆点 + 下方悬浮信息卡（白底/描边/投影/圆角，标题与内容区分） */
const NODE_BORDER: Record<string, string> = {
  tenant: '#3730a3',
  major: '#0d9488',
  course: '#b45309',
};
/** 卡片内类型 tag 配色 */
const TAG_STYLE: Record<string, { bg: string; fg: string }> = {
  tenant: { bg: '#e9e8fa', fg: '#3730a3' },
  major: { bg: '#dcf3ef', fg: '#0d9488' },
  course: { bg: '#faecd4', fg: '#b45309' },
};
const TYPE_LABEL: Record<string, string> = {
  tenant: '资源库',
  major: '专业',
  course: '课程',
};

const EDGE_STYLE: Record<string, { color: string; width: number }> = {
  contains: { color: '#c3cedd', width: 1 },
  parallel: { color: '#d4dce6', width: 1 },
  sequence: { color: '#c3cedd', width: 1 },
};

interface NavCard {
  key: string;
  title: string;
  desc: string;
  icon: string;
  targetId: string;
}

@Component({
  selector: 'app-tenant-homepage',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzSelectModule,
    NzSpinModule,
    NzDrawerModule,
    NzAlertModule,
    SiteBrandComponent,
    SiteFooterComponent,
  ],
  templateUrl: './tenant-homepage.component.html',
  styleUrls: ['./tenant-homepage.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TenantHomepageComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly configState = inject(ConfigStateService);
  private readonly tenantInfoService = inject(TenantInfoService);
  private readonly portalService = inject(PortalService);
  private readonly courseService = inject(CourseService);
  private readonly learningService = inject(LearningService);
  private readonly scroller = inject(ViewportScroller);

  readonly loading = signal(true);
  readonly userName = signal('');

  get isLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  login(): void {
    this.authService.navigateToLogin();
  }

  logout(): void {
    this.authService.logout().subscribe();
  }

  /** 当前登录学生已选课程 id 集合（选过的课程显示徽章、点击直接跳转详情页） */
  readonly enrolledCourseIds = signal<Set<string>>(new Set());
  readonly tenantInfo = signal<TenantInfoDto | null>(null);
  readonly knowledgeGraph = signal<TenantKnowledgeGraphDto | null>(null);
  readonly portalData = signal<PortalHomeDataDto | null>(null);

  // Course preview drawer state
  readonly previewOpen = signal(false);
  readonly previewCourse = signal<CourseBriefDto | null>(null);
  readonly previewDetail = signal<CourseDetailDto | null>(null);
  readonly previewLoading = signal(false);

  // 课程封面渐变色板
  readonly courseColors = ['#1a5fe0', '#0ea5e9', '#0891b2', '#16a34a', '#7c3aed', '#d97706', '#dc2626', '#059669'];
  readonly courseEmojis = ['📖', '📊', '🎯', '💡', '📝', '🌐', '🎨', '🔬'];

  // 学历课程体系 —— 难度分类筛选（对应课程 Difficulty 属性）
  readonly difficultyOptions = [
    { value: 1, label: '入门' },
    { value: 2, label: '初级' },
    { value: 3, label: '中级' },
    { value: 4, label: '高级' },
    { value: 5, label: '专家' },
  ];
  readonly difficultyFilter = signal(0);
  readonly filteredCourses = computed<CourseBriefDto[]>(() => {
    const courses = this.portalData()?.featuredCourses || [];
    const diff = this.difficultyFilter();
    return diff === 0 ? courses : courses.filter(c => (c.difficulty ?? 1) === diff);
  });

  getDifficultyCount(diff: number): number {
    const courses = this.portalData()?.featuredCourses || [];
    return diff === 0 ? courses.length : courses.filter(c => (c.difficulty ?? 1) === diff).length;
  }

  setDifficultyFilter(diff: number): void {
    this.difficultyFilter.set(diff);
  }

  // Hero 背景：复用首页租户卡片同一张封面（coverImageList[0]），静态单张不轮播
  readonly heroCover = computed(() => this.tenantInfo()?.coverImageList?.[0] || null);

  // 顶部分区导航
  readonly navCards: NavCard[] = [
    {
      key: 'intro',
      title: '资源库简介',
      desc: '建设背景 · 目标定位',
      icon: '📘',
      targetId: 'section-intro',
    },
    {
      key: 'construction',
      title: '专业建设',
      desc: '培养方案 · 教学标准',
      icon: '🏛️',
      targetId: 'section-construction',
    },
    {
      key: 'courses',
      title: '学历课程',
      desc: '精品课程 · 在线学习',
      icon: '🎓',
      targetId: 'section-courses',
    },
    {
      key: 'graph',
      title: '知识图谱',
      desc: '专业 · 课程 · 关联',
      icon: '🧠',
      targetId: 'section-graph',
    },
  ];

  // Graph lifecycle
  private chartInstance: echarts.ECharts | null = null;
  private kgRendered = false;
  private resizeHandler: (() => void) | null = null;
  private nodeMap = new Map<string, TenantGraphNodeDto>();

  ngOnInit(): void {
    const cu = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    if (typeof cu?.['userName'] === 'string') this.userName.set(cu['userName'] as string);
    const tenantId = this.route.snapshot.paramMap.get('id');
    if (tenantId) {
      this.loadData(tenantId);
    } else {
      this.loading.set(false);
    }
    this.loadEnrolledCourses();
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.renderGraphIfReady(), 400);
  }

  ngOnDestroy(): void {
    if (this.resizeHandler) window.removeEventListener('resize', this.resizeHandler);
    if (this.chartInstance) this.chartInstance.dispose();
  }

  private loadData(tenantId: string): void {
    this.loading.set(true);
    this.kgRendered = false;
    this.tenantInfoService.getByTenantId(tenantId).subscribe({
      next: (info) => {
        this.tenantInfo.set(info);
        this.loadPortalData(tenantId);
        this.loadGraph(tenantId);
      },
      error: () => this.loadPortalData(tenantId),
    });
  }

  private loadPortalData(tenantId: string): void {
    this.portalService.getHomeData(tenantId).subscribe({
      next: (data) => { this.portalData.set(data); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  private loadGraph(tenantId: string): void {
    this.tenantInfoService.getKnowledgeGraph(tenantId).subscribe({
      next: (kg) => {
        this.knowledgeGraph.set(kg);
        this.nodeMap = new Map((kg.allNodes || []).map(n => [n.id, n]));
        setTimeout(() => this.renderGraphIfReady(), 200);
      },
      error: () => {},
    });
  }

  // ═══ Section navigation ═══

  scrollToSection(targetId: string): void {
    if (!targetId) return;
    this.scroller.scrollToAnchor(targetId);
    const el = document.getElementById(targetId);
    if (el) {
      const top = el.getBoundingClientRect().top + window.scrollY - 80;
      window.scrollTo({ top, behavior: 'smooth' });
    }
  }

  // ═══ Course preview drawer ═══

  /** 已选课 → 直接跳转学生课程详情页（带 tenantId，让课程详情页返回资源库而非课程中心）；未选课 → 打开右侧预览抽屉 */
  handleCourseClick(course: CourseBriefDto): void {
    if (!course?.id) return;
    if (this.isEnrolled(course.id)) {
      this.previewOpen.set(false);
      const tenantId = this.route.snapshot.paramMap.get('id');
      const queryParams: Record<string, string> = {};
      if (tenantId) queryParams['tenantId'] = tenantId;
      this.router.navigate(['/student/courses', course.id], { queryParams });
      return;
    }
    this.openCoursePreview(course);
  }

  /** 当前学生是否已选这门课 */
  isEnrolled(courseId: string | undefined): boolean {
    if (!courseId) return false;
    return this.enrolledCourseIds().has(courseId);
  }

  /** 拉取当前登录学生的已选课程列表，用于徽章展示与点击跳转判断 */
  private loadEnrolledCourses(): void {
    if (!this.authService.isAuthenticated) return;
    this.learningService.getMyCourses().subscribe({
      next: (list) => {
        const ids = (list || [])
          .map(m => m.courseId)
          .filter((id): id is string => !!id);
        this.enrolledCourseIds.set(new Set(ids));
      },
      error: () => {
        // 静默失败：未登录或获取失败时，所有课程按未选课处理
      },
    });
  }

  openCoursePreview(course: CourseBriefDto): void {
    if (!course?.id) return;
    this.previewCourse.set(course);
    this.previewDetail.set(null);
    this.previewLoading.set(true);
    this.previewOpen.set(true);
    this.courseService.getDetail(course.id).subscribe({
      next: (detail) => {
        this.previewDetail.set(detail);
        this.previewLoading.set(false);
      },
      error: () => this.previewLoading.set(false),
    });
  }

  closeCoursePreview(): void {
    this.previewOpen.set(false);
    setTimeout(() => {
      this.previewCourse.set(null);
      this.previewDetail.set(null);
    }, 250);
  }

  /** Flatten nested chapter tree for the drawer display. */
  flattenChapters(chapters: ChapterDto[] | undefined, depth = 0): Array<ChapterDto & { depth: number }> {
    if (!chapters) return [];
    const out: Array<ChapterDto & { depth: number }> = [];
    for (const ch of chapters) {
      out.push({ ...ch, depth });
      if (ch.children?.length) out.push(...this.flattenChapters(ch.children, depth + 1));
    }
    return out;
  }

  // ═══ Graph ═══

  private renderGraphIfReady(): void {
    const kg = this.knowledgeGraph();
    if (!kg || this.kgRendered) return;
    this.renderGraph(kg);
  }

  private renderGraph(kg: TenantKnowledgeGraphDto): void {
    const container = document.getElementById('tenant-graph-chart');
    if (!container) return;
    this.kgRendered = true;

    const existing = echarts.getInstanceByDom(container);
    if (existing) existing.dispose();

    this.chartInstance = echarts.init(container, undefined, {
      devicePixelRatio: window.devicePixelRatio || 1,
    });

    const nodes: any[] = (kg.allNodes || []).map(n => {
      const t = n.nodeType || 'course';
      const dot = NODE_BORDER[t] || NODE_BORDER.course;
      const tag = TAG_STYLE[t] || TAG_STYLE.course;
      const title = this.sanitizeRich(n.name || '');
      const typeLabel = TYPE_LABEL[t] || '课程';
      const subFull = n.childrenCount > 0
        ? `下级 ${n.childrenCount}`
        : this.sanitizeRich(n.description || '');
      const sub = subFull.length > 12 ? subFull.slice(0, 12) + '…' : subFull;
      return {
        id: n.id,
        name: n.name,
        symbol: 'circle',
        symbolSize: 12,
        category: t === 'tenant' ? 0 : t === 'major' ? 1 : 2,
        itemStyle: {
          color: dot,
          borderColor: '#ffffff',
          borderWidth: 2,
        },
        label: {
          show: true,
          position: 'bottom',
          distance: 8,
          backgroundColor: '#ffffff',
          borderColor: '#e2e8f0',
          borderWidth: 1,
          borderRadius: 8,
          padding: [8, 10, 7, 10],
          shadowBlur: 8,
          shadowColor: 'rgba(15,23,42,0.12)',
          shadowOffsetY: 2,
          formatter: [`{t|${title}}`, `{tag|${typeLabel}} {m|${sub}}`].join('\n'),
          rich: {
            t: { fontSize: 12, fontWeight: 700, color: '#1f2d3d', lineHeight: 18, width: 132, overflow: 'truncate' },
            tag: { fontSize: 10, color: tag.fg, backgroundColor: tag.bg, borderRadius: 4, padding: [1, 6] },
            m: { fontSize: 10, color: '#8a93a6', lineHeight: 16 },
          },
        },
        emphasis: {
          scale: 1.3,
          label: { borderColor: dot, borderWidth: 1.5 },
        },
        _node: n,
      };
    });
    const links: any[] = (kg.relations || []).map(r => ({
      source: r.sourceId,
      target: r.targetId,
      lineStyle: {
        color: (EDGE_STYLE[r.relationType || 'contains'] || EDGE_STYLE.contains).color,
        width: (EDGE_STYLE[r.relationType || 'contains'] || EDGE_STYLE.contains).width,
        curveness: 0.05,
        opacity: 0.9,
      },
      _rel: r,
    }));

    const option: any = {
      backgroundColor: 'transparent',
      tooltip: {
        trigger: 'item',
        backgroundColor: '#ffffff',
        borderColor: '#e4e8ee',
        borderWidth: 1,
        padding: 0,
        textStyle: { color: '#1f2d3d', fontSize: 12 },
        extraCssText: 'box-shadow: 0 8px 24px rgba(15,23,42,0.12); border-radius: 8px; overflow: hidden;',
        formatter: (p: any) => this.graphTooltipHtml(p),
      },
      series: [{
        type: 'graph',
        layout: 'force',
        force: { repulsion: 420, gravity: 0.05, edgeLength: [160, 320], friction: 0.9, layoutAnimation: true },
        roam: true,
        draggable: true,
        cursor: 'pointer',
        categories: [{ name: '资源库' }, { name: '专业' }, { name: '课程' }],
        label: { show: false },
        edgeSymbol: ['none', 'none'],
        lineStyle: { curveness: 0.05, opacity: 0.9 },
        emphasis: {
          focus: 'adjacency',
          lineStyle: { width: 1.5, opacity: 1 },
          itemStyle: { shadowBlur: 6, shadowColor: 'rgba(15,23,42,0.2)' },
        },
        blur: { itemStyle: { opacity: 0.3 }, lineStyle: { opacity: 0.1 } },
        data: nodes,
        links,
      }],
    };

    this.chartInstance.setOption(option);
    // 点击节点高亮其关联，再次点击空白处取消
    this.chartInstance.off('click');
    this.chartInstance.on('click', (p: any) => {
      if (!this.chartInstance) return;
      if (p.dataType === 'node') {
        this.chartInstance.dispatchAction({ type: 'focusNodeAdjacency', seriesIndex: 0, dataIndex: p.dataIndex });
      } else {
        this.chartInstance.dispatchAction({ type: 'unfocusNodeAdjacency', seriesIndex: 0 });
      }
    });
    this.resizeHandler = () => this.chartInstance?.resize();
    window.addEventListener('resize', this.resizeHandler);
  }

  private graphTooltipHtml(p: any): string {
    if (p.dataType === 'edge') {
      const rel = p.data?._rel;
      const src = this.nodeMap.get(rel?.sourceId)?.name || rel?.sourceId || '';
      const tgt = this.nodeMap.get(rel?.targetId)?.name || rel?.targetId || '';
      const rt = rel?.relationType || 'contains';
      const rtLabel: Record<string, string> = { contains: '包含', parallel: '并列', sequence: '先后' };
      return `
        <div style="padding:10px 12px; min-width:180px; max-width:280px;">
          <div style="font-size:12.5px; color:#1f2d3d; line-height:1.6;">
            <span style="font-weight:600;">${this.escapeHtml(src)}</span>
            <span style="color:#8a93a6; margin:0 6px;">→</span>
            <span style="font-weight:600;">${this.escapeHtml(tgt)}</span>
          </div>
          <div style="margin-top:6px; padding-top:6px; border-top:1px solid #eef1f6; font-size:11px; color:#8a93a6;">
            关系 · ${rtLabel[rt] || rt}${rel?.label ? ` · ${this.escapeHtml(rel.label)}` : ''}
          </div>
        </div>`;
    }
    const n: TenantGraphNodeDto | undefined = p.data?._node;
    if (!n) return '';
    const m: Record<string, { label: string; color: string }> = {
      tenant: { label: '资源库', color: '#3730a3' },
      major:  { label: '专业',   color: '#0d9488' },
      course: { label: '课程',   color: '#b45309' },
    };
    const meta = m[n.nodeType || 'course'] || m.course;
    const desc = n.description
      ? `<div style="margin-top:6px; font-size:12px; line-height:1.6; color:#5b6573;">${this.escapeHtml(n.description)}</div>`
      : '';
    const children = n.childrenCount > 0
      ? `<div style="margin-top:6px; padding-top:6px; border-top:1px solid #eef1f6; font-size:11px; color:#8a93a6;">
           下级节点 ${n.childrenCount} 个 · 点击高亮关联
         </div>`
      : `<div style="margin-top:6px; padding-top:6px; border-top:1px solid #eef1f6; font-size:11px; color:#8a93a6;">
           点击高亮关联
         </div>`;
    return `
      <div style="padding:10px 12px; min-width:220px; max-width:300px;">
        <div style="display:flex; align-items:center; gap:7px;">
          <span style="width:8px; height:8px; border-radius:50%; background:${meta.color}; flex-shrink:0;"></span>
          <span style="font-weight:700; font-size:13px; color:#1f2d3d; overflow:hidden; text-overflow:ellipsis; white-space:nowrap;">${this.escapeHtml(n.name || '')}</span>
          <span style="margin-left:auto; font-size:11px; color:#8a93a6; flex-shrink:0;">${meta.label}</span>
        </div>
        ${desc}
        ${children}
      </div>`;
  }

  /** 去掉会影响 ECharts 富文本解析的字符（{ } 与换行） */
  private sanitizeRich(s: string): string {
    return (s || '').replace(/[{}\r\n]+/g, ' ').replace(/\s{2,}/g, ' ').trim();
  }

  private escapeHtml(s: string): string {
    return s
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  // ═══ Graph summary helpers ═══

  getCourseCount(kg: TenantKnowledgeGraphDto): number {
    return (kg.allNodes || []).filter(n => n.nodeType === 'course').length;
  }

  getMajorCount(kg: TenantKnowledgeGraphDto): number {
    return (kg.allNodes || []).filter(n => n.nodeType === 'major').length;
  }

  getRelationCount(kg: TenantKnowledgeGraphDto): number {
    return (kg.relations || []).length;
  }
}