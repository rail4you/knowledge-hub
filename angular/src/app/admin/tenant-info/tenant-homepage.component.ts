import {
  ChangeDetectionStrategy, Component, OnInit, inject, signal, AfterViewInit, OnDestroy,
} from '@angular/core';
import { CommonModule, ViewportScroller } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzCarouselModule } from 'ng-zorro-antd/carousel';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzAlertModule } from 'ng-zorro-antd/alert';
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

/** 节点视觉配置 */
const NODE_STYLE: Record<string, {
  color: string; glow: string; symbolSize: number; labelColor: string;
}> = {
  tenant: { color: '#e74c3c', glow: 'rgba(231,76,60,0.35)', symbolSize: 72, labelColor: '#fff' },
  major:  { color: '#2980b9', glow: 'rgba(41,128,185,0.25)', symbolSize: 48, labelColor: '#fff' },
  course: { color: '#27ae60', glow: 'rgba(39,174,96,0.2)',  symbolSize: 36, labelColor: '#fff' },
};

const EDGE_STYLE: Record<string, { color: string; width: number }> = {
  contains: { color: '#3498db', width: 2.5 },
  parallel: { color: '#f39c12', width: 1.8 },
  sequence: { color: '#e74c3c', width: 1.8 },
};

interface NavCard {
  key: string;
  title: string;
  desc: string;
  icon: string;
  gradient: string;
  targetId: string;
}

@Component({
  selector: 'app-tenant-homepage',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzCarouselModule,
    NzDrawerModule,
    NzAlertModule,
  ],
  templateUrl: './tenant-homepage.component.html',
  styleUrls: ['./tenant-homepage.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TenantHomepageComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly tenantInfoService = inject(TenantInfoService);
  private readonly portalService = inject(PortalService);
  private readonly courseService = inject(CourseService);
  private readonly scroller = inject(ViewportScroller);

  readonly loading = signal(true);
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

  // 顶部分区导航
  readonly navCards: NavCard[] = [
    {
      key: 'intro',
      title: '资源库简介',
      desc: '建设背景 · 目标定位',
      icon: '📘',
      gradient: 'linear-gradient(135deg, #4facfe 0%, #00f2fe 100%)',
      targetId: 'section-intro',
    },
    {
      key: 'construction',
      title: '专业建设',
      desc: '培养方案 · 教学标准',
      icon: '🏛️',
      gradient: 'linear-gradient(135deg, #43e97b 0%, #38f9d7 100%)',
      targetId: 'section-construction',
    },
    {
      key: 'courses',
      title: '学历课程',
      desc: '精品课程 · 在线学习',
      icon: '🎓',
      gradient: 'linear-gradient(135deg, #fa709a 0%, #fee140 100%)',
      targetId: 'section-courses',
    },
    {
      key: 'graph',
      title: '知识图谱',
      desc: '专业 · 课程 · 关联',
      icon: '🧠',
      gradient: 'linear-gradient(135deg, #a18cd1 0%, #fbc2eb 100%)',
      targetId: 'section-graph',
    },
  ];

  // Graph lifecycle
  private chartInstance: echarts.ECharts | null = null;
  private kgRendered = false;
  private resizeHandler: (() => void) | null = null;
  private nodeMap = new Map<string, TenantGraphNodeDto>();

  ngOnInit(): void {
    const tenantId = this.route.snapshot.paramMap.get('id');
    if (tenantId) {
      this.loadData(tenantId);
    } else {
      this.loading.set(false);
    }
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
      const style = NODE_STYLE[n.nodeType || 'course'] || NODE_STYLE.course;
      const labelLen = (n.name || '').length;
      return {
        id: n.id,
        name: n.name,
        symbolSize: style.symbolSize,
        category: n.nodeType === 'tenant' ? 0 : n.nodeType === 'major' ? 1 : 2,
        itemStyle: {
          color: style.color,
          shadowBlur: 16,
          shadowColor: style.glow,
          borderColor: 'rgba(255,255,255,0.35)',
          borderWidth: 2.5,
        },
        label: {
          show: true,
          position: 'inside',
          fontSize: n.nodeType === 'tenant' ? 14 : n.nodeType === 'major' ? 12 : 10,
          fontWeight: 700,
          color: style.labelColor,
          textShadowBlur: 3,
          textShadowColor: 'rgba(0,0,0,0.45)',
          formatter: labelLen > 12 ? (n.name || '').slice(0, 12) + '…' : n.name,
        },
        emphasis: {
          scale: true,
          label: { fontSize: n.nodeType === 'tenant' ? 16 : n.nodeType === 'major' ? 14 : 12 },
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
        curveness: 0.18,
        opacity: 0.7,
      },
      _rel: r,
    }));

    const option: any = {
      backgroundColor: 'transparent',
      tooltip: {
        trigger: 'item',
        backgroundColor: 'rgba(255,255,255,0.97)',
        borderColor: '#dde4ee',
        borderWidth: 1,
        padding: 0,
        textStyle: { color: '#1f2937', fontSize: 13 },
        extraCssText: 'box-shadow: 0 12px 36px rgba(15,23,42,0.15); border-radius: 12px; overflow: hidden;',
        formatter: (p: any) => this.graphTooltipHtml(p),
      },
      series: [{
        type: 'graph',
        layout: 'force',
        force: { repulsion: 500, gravity: 0.04, edgeLength: [80, 250], friction: 0.85, layoutAnimation: true },
        roam: true,
        draggable: true,
        categories: [{ name: '资源库' }, { name: '专业' }, { name: '课程' }],
        label: { show: false },
        edgeSymbol: ['none', 'arrow'],
        edgeSymbolSize: [4, 8],
        lineStyle: { curveness: 0.18, opacity: 0.6 },
        emphasis: {
          focus: 'adjacency',
          lineStyle: { width: 3, opacity: 1 },
          itemStyle: { shadowBlur: 24, shadowColor: 'rgba(0,0,0,0.3)' },
        },
        blur: { itemStyle: { opacity: 0.3 }, lineStyle: { opacity: 0.1 } },
        data: nodes,
        links,
      }],
    };

    this.chartInstance.setOption(option);
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
      const rtColor: Record<string, string> = { contains: '#3498db', parallel: '#f39c12', sequence: '#e74c3c' };
      return `
        <div style="padding:14px 18px; min-width:200px;">
          <div style="display:flex; align-items:center; gap:8px; margin-bottom:8px;">
            <span style="background:#f0f5ff; padding:3px 10px; border-radius:999px; font-size:12px; color:#2980b9;">${this.escapeHtml(src)}</span>
            <span style="color:${rtColor[rt] || '#5b6573'}; font-weight:700;">→</span>
            <span style="background:#f0fdf4; padding:3px 10px; border-radius:999px; font-size:12px; color:#16a34a;">${this.escapeHtml(tgt)}</span>
          </div>
          <div style="display:inline-block; padding:2px 10px; border-radius:999px; font-size:12px; color:#fff; background:${rtColor[rt] || '#5b6573'};">
            ${rtLabel[rt] || rt}
          </div>
          ${rel?.label ? `<div style="margin-top:8px; font-size:12px; color:#5b6573;">${this.escapeHtml(rel.label)}</div>` : ''}
        </div>`;
    }
    const n: TenantGraphNodeDto | undefined = p.data?._node;
    if (!n) return '';
    const m: Record<string, { label: string; bg: string; fg: string; icon: string }> = {
      tenant: { label: '资源库', bg: 'rgba(231,76,60,0.1)',  fg: '#e74c3c', icon: '📦' },
      major:  { label: '专业',   bg: 'rgba(41,128,185,0.1)', fg: '#2980b9', icon: '📚' },
      course: { label: '课程',   bg: 'rgba(39,174,96,0.1)',  fg: '#16a34a', icon: '📖' },
    };
    const meta = m[n.nodeType || 'course'] || m.course;
    const desc = n.description
      ? `<div style="margin-top:8px; font-size:12px; line-height:1.6; color:#5b6573;">${this.escapeHtml(n.description)}</div>`
      : '';
    const children = n.childrenCount > 0
      ? `<div style="display:inline-block; margin-top:10px; padding:3px 10px; border-radius:999px; font-size:11px; background:#eef5ff; color:#1e6ce8;">
           子节点 ${n.childrenCount}
         </div>`
      : '';
    return `
      <div style="padding:14px 18px; min-width:240px; max-width:320px;">
        <div style="display:flex; align-items:center; gap:10px; margin-bottom:6px;">
          <span style="font-size:22px;">${meta.icon}</span>
          <div>
            <div style="font-weight:700; font-size:14px; color:#1f2937;">${this.escapeHtml(n.name || '')}</div>
            <div style="display:inline-block; padding:1px 8px; border-radius:4px; font-size:11px; background:${meta.bg}; color:${meta.fg}; margin-top:2px;">
              ${meta.label}
            </div>
          </div>
        </div>
        ${desc}
        ${children}
      </div>`;
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