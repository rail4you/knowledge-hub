import {
  ChangeDetectionStrategy, Component, OnInit, inject, signal, AfterViewInit, OnDestroy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzCarouselModule } from 'ng-zorro-antd/carousel';
import { TenantInfoService } from '../../proxy/tenant-infos/tenant-info.service';
import { PortalService } from '../../proxy/portal/portal.service';
import type { TenantInfoDto, TenantKnowledgeGraphDto } from '../../proxy/tenant-infos/dtos/models';
import type { PortalHomeDataDto } from '../../proxy/portal/models';
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

@Component({
  selector: 'app-tenant-homepage',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzCarouselModule,
  ],
  templateUrl: './tenant-homepage.component.html',
  styleUrls: ['./tenant-homepage.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TenantHomepageComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly tenantInfoService = inject(TenantInfoService);
  private readonly portalService = inject(PortalService);

  readonly loading = signal(true);
  readonly tenantInfo = signal<TenantInfoDto | null>(null);
  readonly knowledgeGraph = signal<TenantKnowledgeGraphDto | null>(null);
  readonly portalData = signal<PortalHomeDataDto | null>(null);

  // Display constants
  readonly courseColors = ['#1a5fe0', '#0ea5e9', '#0891b2', '#16a34a', '#7c3aed', '#d97706', '#dc2626', '#059669'];
  readonly courseEmojis = ['📖', '📊', '🎯', '💡', '📝', '🌐', '🎨', '🔬'];

  // Graph lifecycle
  private chartInstance: echarts.ECharts | null = null;
  private kgRendered = false;
  private resizeHandler: (() => void) | null = null;

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
        setTimeout(() => this.renderGraphIfReady(), 200);
      },
      error: () => {},
    });
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
          fontSize: n.nodeType === 'tenant' ? 16 : n.nodeType === 'major' ? 13 : 10,
          fontWeight: 700,
          color: style.labelColor,
          textShadowBlur: 3,
          textShadowColor: 'rgba(0,0,0,0.55)',
          formatter: labelLen > 12 ? (n.name || '').slice(0, 12) + '…' : n.name,
        },
        emphasis: {
          label: { fontSize: n.nodeType === 'tenant' ? 18 : n.nodeType === 'major' ? 15 : 12 },
        },
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
    }));

    const option: any = {
      backgroundColor: 'transparent',
      tooltip: {
        trigger: 'item',
        backgroundColor: 'rgba(255,255,255,0.96)',
        borderColor: '#dde4ee',
        textStyle: { color: '#1f2937', fontSize: 13 },
        formatter: (p: any) => {
          const d = p.data || {};
          const m: Record<string, string> = { tenant: '📦 资源库', major: '📚 专业', course: '📖 课程' };
          return `<strong style="font-size:14px">${d.name || ''}</strong><br/>
                  <span style="color:#8a93a6;font-size:12px">${m[d.category] || '节点'}</span>`;
        },
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

  getCourseCount(kg: TenantKnowledgeGraphDto): number {
    return (kg.allNodes || []).filter(n => n.nodeType === 'course').length;
  }

  getMajorCount(kg: TenantKnowledgeGraphDto): number {
    return (kg.allNodes || []).filter(n => n.nodeType === 'major').length;
  }
}
