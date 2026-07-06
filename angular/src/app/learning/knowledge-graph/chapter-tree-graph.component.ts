import {
  Component,
  Input,
  signal,
  ElementRef,
  viewChild,
  AfterViewInit,
  OnChanges,
  SimpleChanges,
  OnDestroy,
  ChangeDetectionStrategy,
  HostListener,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import * as echarts from 'echarts/core';
import { TreeChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { TooltipComponent } from 'echarts/components';

echarts.use([TreeChart, CanvasRenderer, TooltipComponent]);

interface ChapterDto {
  id: string;
  courseId?: string;
  parentId?: string;
  title?: string;
  description?: string;
  sortOrder?: number;
  children?: ChapterDto[];
}

@Component({
  selector: 'app-chapter-tree-graph',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzIconModule,
    NzTooltipModule,
    NzDividerModule,
    NzEmptyModule,
    NzInputModule,
  ],
  template: `
    <div class="kg-shell" [class.kg-shell--has-detail]="!!selectedNode()">
      <!-- 画布主区 -->
      <div class="kg-main">
        <!-- 浮动工具栏 -->
        <div class="kg-floating-toolbar">
          <div class="kg-toolbar__title">
            <span class="kg-toolbar__icon">
              <span nz-icon nzType="apartment" nzTheme="outline"></span>
            </span>
            <div class="kg-toolbar__title-text">
              <strong>章节图谱</strong>
              <span class="kg-toolbar__subtitle">共 {{ chapterCount() }} 章</span>
            </div>
          </div>

          <div class="kg-toolbar__divider"></div>

          <nz-input-group
            [nzPrefix]="searchPrefix"
            class="kg-search"
            nzSize="small"
          >
            <input
              nz-input
              placeholder="搜索章节..."
              [(ngModel)]="searchTerm"
              (ngModelChange)="onSearchChange()"
            />
          </nz-input-group>
          <ng-template #searchPrefix>
            <span nz-icon nzType="search"></span>
          </ng-template>

          @if (searchTerm) {
            <button
              class="kg-icon-btn kg-icon-btn--ghost"
              nz-tooltip
              nzTooltipTitle="清除搜索"
              (click)="clearSearch()"
            >
              <span nz-icon nzType="close" nzTheme="outline"></span>
            </button>
          }

        </div>

        <!-- 图谱画布 -->
        <div class="kg-canvas-wrap">
          <!-- 网格背景 -->
          <div class="kg-grid" aria-hidden="true"></div>

          @if (!chapters || chapters.length === 0) {
            <div class="kg-empty">
              <span nz-icon nzType="apartment" nzTheme="outline" class="kg-empty__icon"></span>
              <p>暂无章节数据用于构建图谱</p>
            </div>
          } @else {
            <div
              #chartContainer
              class="kg-canvas"
              [style.min-height.px]="chartMinHeight()"
            ></div>

            <!-- 浮层：缩放比例 -->
            @if (zoomPercent() !== 100) {
              <div class="kg-zoom-indicator">
                <span class="kg-zoom-indicator__bar" [style.width.%]="zoomPercent()"></span>
                <span nz-icon nzType="zoom-in" nzTheme="outline"></span>
                <span>{{ zoomPercent() }}%</span>
              </div>
            }

            <!-- 浮层：操作提示 -->
            <div class="kg-hint">
              <span><span nz-icon nzType="mouse" nzTheme="outline"></span> 滚轮缩放</span>
              <span class="kg-hint__sep">·</span>
              <span><span nz-icon nzType="drag" nzTheme="outline"></span> 拖拽移动</span>
              <span class="kg-hint__sep">·</span>
              <span><span nz-icon nzType="select" nzTheme="outline"></span> 点击查看</span>
            </div>
          }
        </div>
      </div>

      <!-- 选中节点详情面板 -->
      @if (selectedNode(); as node) {
        <aside class="kg-detail">
          <div class="kg-detail__head">
            <div class="kg-detail__head-pattern" aria-hidden="true">
              <span class="kg-pattern-dot kg-pattern-dot--1"></span>
              <span class="kg-pattern-dot kg-pattern-dot--2"></span>
              <span class="kg-pattern-dot kg-pattern-dot--3"></span>
            </div>
            <div class="kg-detail__head-top">
              <span class="kg-detail__type">
                <span nz-icon nzType="folder" nzTheme="outline"></span>
                章节
              </span>
              <button class="kg-detail__close" (click)="clearSelection()" nz-tooltip nzTooltipTitle="关闭 (Esc)">
                <span nz-icon nzType="close" nzTheme="outline"></span>
              </button>
            </div>
            <h3 class="kg-detail__title">{{ node.title }}</h3>
          </div>
          <div class="kg-detail__body">
            @if (node.description) {
              <section class="kg-detail__section">
                <h4 class="kg-detail__label">
                  <span nz-icon nzType="file-text" nzTheme="outline"></span>
                  简介
                </h4>
                <p class="kg-detail__text">{{ node.description }}</p>
              </section>
            }
            <section class="kg-detail__section">
              <h4 class="kg-detail__label">
                <span nz-icon nzType="bar-chart" nzTheme="outline"></span>
                统计
              </h4>
              <div class="kg-detail__stat">
                <strong>{{ node.subChapterCount || 0 }}</strong>
                <span>子章节</span>
              </div>
            </section>
            @if (node.subChapters && node.subChapters.length > 0) {
              <section class="kg-detail__section">
                <h4 class="kg-detail__label">
                  <span nz-icon nzType="folder" nzTheme="outline"></span>
                  子章节 ({{ node.subChapters.length }})
                </h4>
                <ul class="kg-detail__list">
                  @for (c of node.subChapters; track c.id) {
                    <li
                      class="kg-detail__list-item"
                      (click)="selectChapter(c)"
                    >
                      <span class="kg-detail__list-name">{{ c.title }}</span>
                      @if ((c.children?.length || 0) > 0) {
                        <span class="kg-detail__list-tag">
                          {{ c.children?.length }} 子章节
                        </span>
                      }
                    </li>
                  }
                </ul>
              </section>
            }
          </div>
        </aside>
      }
    </div>
  `,
  styleUrls: ['./chapter-tree-graph.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChapterTreeGraphComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() chapters: ChapterDto[] = [];

  private readonly chartContainer = viewChild<ElementRef>('chartContainer');
  private chart: echarts.ECharts | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private nodePositions = new Map<string, { x: number; y: number }>();

  selectedNode = signal<any | null>(null);
  zoomPercent = signal<number>(100);

  /** 折叠状态：记录被手动折叠的节点 id */
  private collapsedSet = new Set<string>();
  /** 搜索高亮节点 id */
  private highlightedSet = new Set<string>();

  /** 搜索词 */
  searchTerm = '';

  /** 节点色板（按深度循环） */
  private readonly palette = [
    '#1e6ce8', // 0 - primary
    '#0ea5e9', // 1 - sky
    '#10b981', // 2 - emerald
    '#f59e0b', // 3 - amber
    '#8b5cf6', // 4 - violet
  ];

  chapterCount = computed(() => this.countChapters(this.chapters));

  /** 最大同层节点数 — 用于动态计算图谱高度，避免子节点多时拥挤 */
  maxSiblingCount = computed(() => {
    let maxCount = 0;
    const walk = (nodes: ChapterDto[]) => {
      maxCount = Math.max(maxCount, nodes.length);
      nodes.forEach(n => n.children && walk(n.children));
    };
    walk(this.chapters || []);
    return maxCount;
  });

  /** 图谱画布最小高度：按总节点数计算，确保 ECharts 树图完整渲染不被裁剪 */
  chartMinHeight = computed(() => {
    const total = this.countChapters(this.chapters);
    // 每个节点约 56px（symbolSize 28 + 标签高度 + 间距）
    const neededHeight = total * 56;
    return Math.max(600, Math.min(neededHeight, 5000));
  });

  ngAfterViewInit() {
    this.initChart();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['chapters'] && this.chart) {
      this.updateChart();
    }
  }

  ngOnDestroy() {
    this.resizeObserver?.disconnect();
    this.chart?.dispose();
    this.chart = null;
  }

  @HostListener('window:resize')
  onWindowResize() {
    this.chart?.resize();
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    this.clearSelection();
  }

  // ============= 初始化图表 =============
  private initChart() {
    const container = this.chartContainer()?.nativeElement;
    if (!container) return;

    this.chart = echarts.init(container, null, { renderer: 'canvas' });
    this.updateChart();

    this.chart.on('graphRoam', () => this.updateZoomPercent());

    this.chart.on('click', (params: any) => {
      if (params.dataType === 'node') {
        const nodeData = params.data as any;
        if (nodeData?.extData) {
          this.selectedNode.set(nodeData.extData);
        }
      }
    });

    this.chart.on('mouseover', { dataType: 'node' }, () => {
      if (container) container.style.cursor = 'pointer';
    });
    this.chart.on('mouseout', { dataType: 'node' }, () => {
      if (container) container.style.cursor = 'grab';
    });

    this.resizeObserver = new ResizeObserver(() => this.chart?.resize());
    this.resizeObserver.observe(container);
  }

  private updateZoomPercent() {
    if (!this.chart) return;
    const opt = this.chart.getOption() as any;
    const series = opt?.series?.[0];
    if (!series) return;
    const scale = (series.zoom ?? 1) * 100;
    this.zoomPercent.set(Math.round(scale));
  }

  // ============= 工具栏操作 =============
  zoomIn() {
    if (!this.chart) return;
    this.dispatchZoom(1.25);
  }

  zoomOut() {
    if (!this.chart) return;
    this.dispatchZoom(1 / 1.25);
  }

  private dispatchZoom(zoomDelta: number) {
    if (!this.chart) return;
    const current = this.zoomPercent() / 100;
    const next = Math.max(0.3, Math.min(2.5, current * zoomDelta));
    const ratio = next / current;
    this.chart.dispatchAction({
      type: 'graphRoam',
      zoom: ratio,
      originX: this.chart.getWidth() / 2,
      originY: this.chart.getHeight() / 2,
    } as any);
    this.updateZoomPercent();
  }

  fitView() {
    if (!this.chart) return;
    this.chart.dispatchAction({
      type: 'graphRoam',
      zoom: 1 / (this.zoomPercent() / 100),
      originX: this.chart.getWidth() / 2,
      originY: this.chart.getHeight() / 2,
    } as any);
    this.zoomPercent.set(100);
  }

  expandAll() {
    this.collapsedSet.clear();
    this.updateChart();
  }

  collapseAll() {
    this.collapsedSet.clear();
    const collect = (nodes: ChapterDto[]) => {
      nodes.forEach(n => {
        if (n.children && n.children.length > 0) {
          this.collapsedSet.add(n.id!);
        }
        if (n.children) collect(n.children);
      });
    };
    collect(this.chapters);
    this.updateChart();
  }

  clearSearch() {
    this.searchTerm = '';
    this.onSearchChange();
  }

  onSearchChange() {
    this.highlightedSet.clear();
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.trim().toLowerCase();
      const match = (n: ChapterDto) =>
        (n.title || '').toLowerCase().includes(term) ||
        (n.description || '').toLowerCase().includes(term);
      const walk = (nodes: ChapterDto[], parents: string[] = []) => {
        nodes.forEach(n => {
          const path = [...parents, n.id!];
          if (match(n)) {
            path.forEach(id => this.highlightedSet.add(id));
            this.highlightedSet.add(n.id!);
          }
          if (n.children) walk(n.children, path);
        });
      };
      walk(this.chapters);
    }
    this.updateChart();
  }

  clearSelection() {
    this.selectedNode.set(null);
  }

  selectChapter(c: ChapterDto) {
    const subChapters = (c.children || []).map(x => x);
    this.selectedNode.set({
      kind: 'chapter',
      id: c.id,
      title: c.title,
      description: c.description,
      subChapterCount: subChapters.length,
      subChapters,
      subtitle: '章节',
    });
  }

  // ============= 统计 =============
  private countChapters(list: ChapterDto[]): number {
    let total = 0;
    const walk = (arr: ChapterDto[]) => {
      arr.forEach(n => {
        total++;
        if (n.children) walk(n.children);
      });
    };
    walk(list || []);
    return total;
  }

  // ============= 更新图表 =============
  private updateChart() {
    if (!this.chart) return;
    const treeData = this.buildTreeData(this.chapters, 0);
    this.applyOption(treeData);
  }

  private applyOption(treeData: any[]) {
    if (!this.chart) return;
    const option: echarts.EChartsCoreOption = {
      tooltip: {
        trigger: 'item',
        triggerOn: 'mousemove',
        backgroundColor: 'rgba(15, 23, 42, 0.94)',
        borderWidth: 0,
        textStyle: { color: '#fff', fontSize: 12 },
        padding: [10, 14],
        extraCssText:
          'border-radius: 10px; box-shadow: 0 10px 32px rgba(0,0,0,0.28);',
        formatter: (params: any) => {
          const data = params.data as any;
          if (!data) return '';
          const meta = data.meta || {};
          const subCount = meta.subChapterCount || 0;
          return `
            <div style="font-weight:600;font-size:13px;margin-bottom:4px;">${escapeHtml(data.name)}</div>
            <div style="opacity:.85;font-size:11px;">${subCount} 子章节</div>
          `;
        },
      },
      series: [
        {
          type: 'tree',
          name: '章节图谱',
          data: treeData,
          top: '5%',
          left: '2%',
          bottom: '5%',
          right: '22%',
          symbol: 'circle',
          symbolSize: 28,
          orient: 'LR',
          roam: true,
          nodeDraggable: false,
          initialTreeDepth: 1,
          expandAndCollapse: true,
          initialExpandDepth: 1,
          animationDuration: 600,
          animationDurationUpdate: 500,
          animationEasing: 'cubicOut',
          animationEasingUpdate: 'cubicInOut',
          // 边样式
          lineStyle: {
            color: '#cbd5e1',
            width: 1.4,
            curveness: 0.5,
            opacity: 0.85,
          },
          // 节点样式
          itemStyle: {
            color: this.palette[0],
            borderColor: '#fff',
            borderWidth: 3,
            shadowBlur: 12,
            shadowColor: 'rgba(15, 23, 42, 0.18)',
            shadowOffsetY: 3,
          },
          // 高亮
          emphasis: {
            focus: 'ancestor',
            scale: true,
            scaleSize: 6,
            itemStyle: {
              shadowBlur: 18,
              shadowColor: 'rgba(30, 108, 232, 0.5)',
              borderColor: '#fff',
              borderWidth: 4,
            },
            lineStyle: {
              width: 2.2,
              color: '#1e6ce8',
            },
          },
          // 文本
          label: {
            show: true,
            position: 'right',
            distance: 10,
            formatter: (params: any) => {
              const data = params.data as any;
              return data?.name || '';
            },
            fontSize: 12,
            fontWeight: 600,
            color: '#1f2937',
            backgroundColor: 'transparent',
          },
          // 叶子节点样式
          leaves: {
            label: {
              show: true,
              position: 'right',
              distance: 10,
              formatter: (params: any) => {
                const data = params.data as any;
                return data?.name || '';
              },
              fontSize: 12,
              fontWeight: 600,
              color: '#1f2937',
            },
          },
        },
      ],
    };
    this.chart.setOption(option, { notMerge: true });
  }

  // ============= 构造树形数据 =============
  private buildTreeData(chapters: ChapterDto[], depth: number): any[] {
    if (!chapters || chapters.length === 0) {
      return [{ name: '暂无章节', itemStyle: { color: '#94a3b8' } }];
    }

    const processChapter = (chapter: ChapterDto, depth: number): any => {
      const subCount = (chapter.children || []).length;
      const isCollapsed = this.collapsedSet.has(chapter.id!);
      const isHighlighted = this.highlightedSet.has(chapter.id!);
      const isRoot = depth === 0;
      const color = this.palette[depth % this.palette.length];

      const node: any = {
        name: chapter.title || '未命名章节',
        meta: {
          kind: 'chapter',
          id: chapter.id,
          depth,
          subChapterCount: subCount,
        },
        extData: {
          kind: 'chapter',
          id: chapter.id,
          title: chapter.title,
          description: chapter.description,
          subChapterCount: subCount,
          subChapters: chapter.children || [],
          subtitle: '章节',
        },
        collapsed: isCollapsed,
        itemStyle: {
          color,
          borderColor: isHighlighted ? '#f59e0b' : '#fff',
          borderWidth: isHighlighted ? 5 : 3,
          shadowBlur: isHighlighted ? 18 : 12,
          shadowColor: isHighlighted
            ? 'rgba(245, 158, 11, 0.55)'
            : this.hexToRgba(color, 0.35),
          shadowOffsetY: 3,
        },
      };

      if (chapter.children && chapter.children.length > 0) {
        const sortedChildren = [...chapter.children]
          .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0))
          .map(c => processChapter(c, depth + 1));
        node.children = sortedChildren;
      }

      return node;
    };

    return [...chapters]
      .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0))
      .map(c => processChapter(c, depth));
  }

  private hexToRgba(hex: string, alpha: number): string {
    if (!hex || hex[0] !== '#') return `rgba(148, 163, 184, ${alpha})`;
    const h = hex.replace('#', '');
    const r = parseInt(h.substring(0, 2), 16);
    const g = parseInt(h.substring(2, 4), 16);
    const b = parseInt(h.substring(4, 6), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}
