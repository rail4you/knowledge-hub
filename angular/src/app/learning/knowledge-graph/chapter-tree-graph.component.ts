import {
  Component,
  Input,
  signal,
  ElementRef,
  viewChild,
  AfterViewInit,
  AfterViewChecked,
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
import type { ChapterDto } from '../../proxy/courses/dtos/models';

echarts.use([TreeChart, CanvasRenderer, TooltipComponent]);

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
        <!-- 顶部工具栏（位于图谱上方，文档流布局） -->
        <div class="kg-floating-toolbar kg-floating-toolbar--flat">
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

          <span class="kg-count" nz-tooltip nzTooltipTitle="共 {{ chapterCount() }} 个章节">
            {{ chapterCount() }} 节
          </span>
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
              <span><span nz-icon nzType="zoom-in" nzTheme="outline"></span> 滚轮缩放</span>
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
export class ChapterTreeGraphComponent implements AfterViewInit, AfterViewChecked, OnChanges, OnDestroy {
  @Input() chapters: ChapterDto[] = [];
  /** 课程名称（可选）：传入后作为图谱的根节点，将所有章节作为其子节点 */
  @Input() courseName: string = '';

  private readonly chartContainer = viewChild<ElementRef>('chartContainer');
  private chart: echarts.ECharts | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private nodePositions = new Map<string, { x: number; y: number }>();

  selectedNode = signal<any | null>(null);
  zoomPercent = signal<number>(100);
  /** 缩放下限 50% */
  private readonly MIN_ZOOM = 0.5;
  /** 缩放上限 130%（节点已改小，放大只拉开间距，上限太高会导致层间距过长、连线显长） */
  private readonly MAX_ZOOM = 1.3;
  /** 当前绝对缩放比例 — 通过 treeRoam 事件的 delta 参数累乘得到 */
  private currentAbsoluteZoom = 1;
  /** 首次适配是否已执行，避免后续 ngOnChanges 重新适配 */
  private hasInitiallyFit = false;
  /** 非叶子节点标签位置：固定 'right'。曾按缩放动态切换到 'bottom'，
   * 但 treeRoam 回调里任何 setOption 都会重建整棵树、丢弃用户展开状态，
   * 故缩放路径上禁止 setOption，此处保持固定。 */
  private nonLeafLabelPosition: 'right' | 'bottom' = 'right';
  /**
   * capture 阶段 wheel 监听器，挂在 chartContainer 上、echarts.init 之前挂载。
   * ECharts 把自己的 wheel 监听挂在内层 zrender canvas 上，且用 bubble 阶段；
   * 父元素 capture 阶段先于子元素 bubble 阶段触发，所以我们可以赶在 ECharts 之前
   * 看到 wheel 事件，并在到达边界时直接 stopImmediatePropagation，让 ECharts
   * 根本不会越界（避免视觉上出现"缩小到 45% 再弹回 50%"的闪烁）。
   */
  private readonly wheelGuard = (e: WheelEvent) => {
    // deltaY > 0 = 向下滚 = 缩小；deltaY < 0 = 向上滚 = 放大
    if (this.currentAbsoluteZoom <= this.MIN_ZOOM && e.deltaY > 0) {
      e.preventDefault();
      e.stopImmediatePropagation();
    } else if (this.currentAbsoluteZoom >= this.MAX_ZOOM && e.deltaY < 0) {
      e.preventDefault();
      e.stopImmediatePropagation();
    }
  };

  /** 搜索高亮节点 id */
  private highlightedSet = new Set<string>();

  /** 搜索词 */
  searchTerm = '';

  /** 节点色板（按深度循环） */
  private readonly palette = [
    '#087f7a', // 0 - primary
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
    // 如果有课程根节点，+1 计入
    if (this.courseName) maxCount = Math.max(maxCount, 1);
    return maxCount;
  });

  /** 数据实际的最大层级深度（不含课程根）：
   * 1 = 只有顶层；2 = 顶层+叶子；3+ = 还有更深的层级需要折叠。
   * 用于决定默认展开到哪一级、画布该多高。
   */
  dataDepth = computed(() => {
    let maxDepth = 1;
    const walk = (nodes: ChapterDto[] | undefined, d: number) => {
      if (!nodes) return;
      nodes.forEach(n => {
        maxDepth = Math.max(maxDepth, d);
        if (n.children) walk(n.children, d + 1);
      });
    };
    walk(this.chapters, 1);
    return maxDepth;
  });

  /** 二级章节数（顶层章节的直接子节点总数）— 用于判断展开三级后是否放得下 */
  secondLevelCount = computed(() => {
    let n = 0;
    for (const top of this.chapters || []) n += top.children?.length || 0;
    return n;
  });

  /** 图谱画布高度：
   * 默认展开到三级（课程根 + 一/二/三级），高度按三级可见行数撑高。
   * 行高按 24px 估算（节点 10~16px，行距约为节点 150%，文字不重叠），夹紧到 440~1300px。
   */
  chartMinHeight(): number {
    const total = this.countChapters(this.chapters);
    const dataDepth = this.dataDepth();
    const maxSiblings = this.maxSiblingCount();

    // 深层数据：可见行按默认三级展开视图估算（三级节点 + 无子节点的二级/一级兜底）。
    if (dataDepth >= 3) {
      const rows = this.defaultVisibleRows();
      // 每行约 24px + 上下留白，夹紧到 440~1300px
      return Math.max(440, Math.min(rows * 24 + 140, 1300));
    }
    if (total > 60) return 520;
    if (total > 30) return 480;

    // 2 级图谱：所有叶子纵向排列，需要按最多同级节点数撑高
    // 每个叶子 symbolSize=16，按约 150% 行距 ≈ 34px 估算，上下各留 35px 呼吸空间
    const neededForLeaves = maxSiblings * 34 + 70;
    return Math.max(300, Math.min(neededForLeaves, 600));
  }

  /**
   * 默认展开深度（课程根占第 0 层）：
   * - 数据本身只有 ≤2 级 → 全展开（-1），小图一眼看完；
   * - 深层数据（≥3 级）→ 固定展开到 3 级（视觉 4 层：课程根 + 一/二/三级）。
   * 三级节点多时靠画布撑高 + 小字号保证不重叠。
   */
  private defaultInitialDepth(): number {
    if (this.dataDepth() <= 2) return -1;
    return 3;
  }
  /**
   * 默认展开视图（三级）下的可见行数：
   * 三级节点总数 + 无子节点的二级兜底行 + 无子节点的一级兜底行。
   * 纵向画布高度与字号分档都按此估算，保证三级文字不重叠。
   */
  private defaultVisibleRows(): number {
    let third = 0;
    let childlessL2 = 0;
    for (const top of this.chapters || []) {
      const l2 = top.children || [];
      if (l2.length === 0) continue; // 无子节点的一级另行统计
      for (const mid of l2) {
        const l3 = mid.children || [];
        if (l3.length === 0) childlessL2++;
        else third += l3.length;
      }
    }
    const childlessTop = (this.chapters || []).filter(c => !(c.children?.length || 0)).length;
    return third + childlessL2 + childlessTop;
  }

  ngAfterViewInit() {
    this.initChart();
  }

  ngOnChanges(changes: SimpleChanges) {
    // courseName 可能比 chapters 先到/后到（如课程详情先回来），任一变化都需重建
    if ((changes['chapters'] || changes['courseName']) && this.chart) {
      // 切换课程时重置视图状态，避免旧课程的缩放残留
      if (changes['chapters']) {
        this.hasInitiallyFit = false;
        this.currentAbsoluteZoom = 1;
        this.zoomPercent.set(100);
      }
      this.updateChart();
    }
  }

  private chartInitAttempted = false;

  /**
   * 延迟初始化：ngAfterViewInit 时 chapters 可能还是空数组，
   * 数据到达后 view child 尚未更新。在 AfterViewChecked 中检查
   * 容器是否就绪，只会尝试一次。
   */
  ngAfterViewChecked() {
    if (this.chartInitAttempted) return;
    if (!this.chart && this.chapters?.length > 0) {
      const container = this.chartContainer()?.nativeElement;
      if (container) {
        this.chartInitAttempted = true;
        this.initChart();
      }
    }
  }

  ngOnDestroy() {
    this.resizeObserver?.disconnect();
    const container = this.chartContainer()?.nativeElement;
    if (container) {
      container.removeEventListener('wheel', this.wheelGuard, { capture: true });
    }
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

    // 必须先于 echarts.init 挂载 capture 阶段的 wheel 守卫，
    // 才能赶在 ECharts 内部的 wheel handler 之前截获事件。
    container.addEventListener('wheel', this.wheelGuard, {
      passive: false,
      capture: true,
    });

    this.chart = echarts.init(container, null, { renderer: 'canvas' });
    this.updateChart();

    this.chart.on('treeRoam', (params: any) => {
      // Tree 系列的 roam event 名是 'treeRoam'（不是 'graphRoam'）。
      // params.zoom 是相对增量：追踪值每事件夹紧一次，与 ECharts 内部 scaleLimit
      // 的逐事件夹紧是同初值、同运算，归纳可得两者恒一致，因此反复滚轮缩放不漂移。
      // 旧方案曾在越界时自派发一次反向 treeRoam 做“回滚”，该回滚事件与后续滚轮
      // 事件交错时会导致追踪值与真实视图错位（缩放忽大忽小、回不到原位），现已删除，
      // 越界夹紧完全交给 series.scaleLimit + 外层 wheel 守卫。
      if (params && typeof params.zoom === 'number') {
        this.currentAbsoluteZoom = Math.max(
          this.MIN_ZOOM,
          Math.min(this.MAX_ZOOM, this.currentAbsoluteZoom * params.zoom),
        );
        this.zoomPercent.set(Math.round(this.currentAbsoluteZoom * 100));
        // 注意：此处绝不能调用 chart.setOption（即使是 merge 模式）。
        // ECharts tree 系列每次 setOption 都会按 option.data + initialTreeDepth
        // 重建整棵树，用户通过点击节点产生的运行时展开/折叠状态（isExpand）
        // 会被丢弃、回退到初始折叠态。这就是“展开后一滚轮缩放就复原”的根因。
        // 非叶子标签位置固定为 'right'，靠 labelLayout.hideOverlap 避让，
        // 缩放只走 treeRoam action，不重建数据，展开状态自然保留。
      }
    });

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
    // 使用追踪值，初始为 1.0；通过 treeRoam 事件累乘 params.zoom（增量）更新
    this.zoomPercent.set(Math.round(this.currentAbsoluteZoom * 100));
  }

  /**
   * 根据节点总数计算合适的初始缩放比例。
   * 目标：节点越多越缩小，确保所有节点文字标签不重叠、保持合理间距，
   * 并且整个图谱能够在一页高度内完整呈现（避免用户上下滚动查看）。
   */
  private computeFitZoom(nodeCount: number, depth: number): number {
    // 综合考虑节点数量与树的深度（层级越多，水平方向越容易被压缩）
    const complexity = nodeCount * Math.max(depth, 1);
    let zoom: number;
    if (complexity <= 60) zoom = 1.0;       // 极少节点：默认 1.0
    else if (complexity <= 120) zoom = 0.9;  // 简单图谱（原 0.8）
    else if (complexity <= 250) zoom = 0.75; // 中等图谱
    else if (complexity <= 500) zoom = 0.6;  // 较多节点
    else if (complexity <= 900) zoom = 0.5;  // 大量节点
    else zoom = 0.45;                        // 极复杂图谱
    // 初始适配也必须落在 [50%, 130%] 范围内
    return Math.max(this.MIN_ZOOM, Math.min(this.MAX_ZOOM, zoom));
  }

  /** 计算树的最大深度（用于辅助缩放判断） */
  private computeTreeDepth(): number {
    let maxDepth = 1;
    const walk = (nodes: ChapterDto[] | undefined, d: number) => {
      if (!nodes) return;
      nodes.forEach(n => {
        maxDepth = Math.max(maxDepth, d + 1);
        walk(n.children, d + 1);
      });
    };
    walk(this.chapters, 1);
    // 如果以课程名为根，再加 1
    if (this.courseName) maxDepth += 1;
    return maxDepth;
  }

  /**
   * 初次渲染：小图谱才做居中适配；大图谱保持 zoom=1 从顶部铺开，
   * 避免“从画布中心缩小”导致的顶部大片空白。
   * 同一组件实例只执行一次。
   *
   * 注意：Tree 系列使用的是 'treeRoam' action（不是 'graphRoam'），
   * 见 ECharts 源码 roamHelper.js: `var type = seriesModel.subType + 'Roam';`
   */
  private scheduleInitialFit() {
    if (!this.chart || this.hasInitiallyFit) return;
    this.hasInitiallyFit = true;
    // 大图谱默认折叠 + 固定高度，不做缩放适配（保持顶部对齐、无空白）
    if (this.chapterCount() > 30) return;
    // 等 ECharts 完成布局与动画
    setTimeout(() => {
      if (!this.chart) return;
      const total = this.chapterCount();
      const depth = this.computeTreeDepth();
      const target = this.computeFitZoom(total, depth);
      const current = this.currentAbsoluteZoom;
      if (Math.abs(target - current) < 0.01) return;
      const ratio = target / current;
      try {
        this.chart!.dispatchAction({
          type: 'treeRoam',
          zoom: ratio,
          originX: this.chart!.getWidth() / 2,
          originY: this.chart!.getHeight() / 2,
        } as any);
        // treeRoam 事件回调会乘以 ratio 并更新指示器
      } catch {
        // ignore
      }
    }, 180);
  }

  // ============= 工具栏操作 =============
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

  /** 构建以课程名称为根的树图数据 */
  private buildTreeWithRoot(chapters: ChapterDto[]): any[] {
    if (!chapters || chapters.length === 0) return [{ name: '暂无章节', itemStyle: { color: '#94a3b8' } }];

    const rootName = this.courseName || '课程章节';
    const childNodes = [...chapters]
      .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0))
      .map(c => this.processChapter(c, 0));

    const subCount = childNodes.length;
    return [{
      name: rootName,
      meta: { kind: 'course', id: '', depth: 0, subChapterCount: subCount },
      extData: {
        kind: 'course', id: '', title: rootName,
        subChapterCount: subCount, subChapters: chapters,
      },
      itemStyle: {
        color: this.palette[0], borderColor: '#fff', borderWidth: 2,
        shadowBlur: 12, shadowColor: this.hexToRgba(this.palette[0], 0.45), shadowOffsetY: 2,
      },
      label: { fontSize: 14, fontWeight: 700, color: '#087f7a', width: 140, overflow: 'truncate' },
      children: childNodes,
    }];
  }

  // ============= 更新图表 =============
  private updateChart() {
    if (!this.chart) return;
    const treeData = this.courseName
      ? this.buildTreeWithRoot(this.chapters)
      : this.buildTreeData(this.chapters, 0);
    this.applyOption(treeData);
  }

  private applyOption(treeData: any[]) {
    if (!this.chart) return;
    const total = this.chapterCount();
    const dataDepth = this.dataDepth();
    const maxSiblings = this.maxSiblingCount();
    const isLarge = total > 60 || dataDepth >= 3;
    // 2 级图谱叶子特别多时也按"密集"处理，避免互相挤压
    const isFlatDense = dataDepth <= 2 && maxSiblings > 20;
    // 第三级可见行数：行数越多字号/节点越小，配合撑高的画布保证文字不重叠。
    const thirdLevelRows = this.defaultVisibleRows();
    // 层间距 = 可用宽度 / 层数：ECharts 会把树拉满 left~right 区间再均分层间距，
    // 留白越大、单段连线越短。默认展开到三级（视觉 4 层）时有 3 段，按视觉层数分档预留，
    // 把每段连线压到 ~110~180px；右侧同时给末层文字留足空间。
    const visualDepth = dataDepth + (this.courseName ? 1 : 0);
    let treeLeft: string | number;
    let treeRight: string;
    if (visualDepth <= 2) { treeLeft = '25%'; treeRight = '55%'; }
    else if (visualDepth === 3) { treeLeft = '15%'; treeRight = '50%'; }
    else if (visualDepth === 4) { treeLeft = '10%'; treeRight = '45%'; }
    else { treeLeft = '8%'; treeRight = '40%'; }
    // 分档：节点直径与字号接近（约 1:1），放大后比例依然协调；
    // 配合 nodeScaleRatio: 0，缩放只拉开间距、不放大节点，节点/文字比例恒定。
    // parentLabelWidth：父节点标签放在右侧、会伸进下一段间距里，必须小于段距，
    // 故比叶子标签截得更短；叶子标签可伸进右侧大留白，保持较宽。
    let symbolSize: number;
    let fontSize: number;
    let labelWidth: number;
    let parentLabelWidth: number;
    if (dataDepth >= 3 && thirdLevelRows > 50) {
      symbolSize = 10; fontSize = 11; labelWidth = 120; parentLabelWidth = 80;
    } else if (dataDepth >= 3 && thirdLevelRows > 30) {
      symbolSize = 12; fontSize = 11; labelWidth = 140; parentLabelWidth = 88;
    } else if (isLarge || isFlatDense) {
      symbolSize = 14; fontSize = 12; labelWidth = 160; parentLabelWidth = 96;
    } else {
      symbolSize = 16; fontSize = 13; labelWidth = 160; parentLabelWidth = 110;
    }
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
          top: 12,
          left: treeLeft,
          bottom: 12,
          // 见上方 treeLeft/treeRight：留白越大、层间距越短，避免连线显长。
          right: treeRight,
          symbol: 'circle',
          symbolSize,
          orient: 'LR',
          roam: true,
          // 原生缩放边界：ECharts 内部逐事件夹紧，视觉缩放永不越界；
          // 追踪值（currentAbsoluteZoom）在 treeRoam 回调里做同样夹紧，两者恒一致。
          scaleLimit: { min: this.MIN_ZOOM, max: this.MAX_ZOOM },
          // 缩放时节点保持屏幕尺寸不变（只拉开间距），文字字号同样不变，
          // 节点/文字比例恒定，避免放大后节点过大、比例失衡。
          nodeScaleRatio: 0,
          nodeDraggable: false,
          expandAndCollapse: true,
          // 默认展开层级见 defaultInitialDepth()：深层数据默认展开到三级
          //（课程根 + 一/二/三级，视觉 4 层），数据本身 ≤2 级时全展开。
          // 更深层级用户可点击节点自行展开（expandAndCollapse）。
          initialTreeDepth: this.defaultInitialDepth(),
          animationDuration: 600,
          animationDurationUpdate: 500,
          animationEasing: 'cubicOut',
          animationEasingUpdate: 'cubicInOut',
          // 边样式
          lineStyle: {
            color: '#cbd5e1',
            width: 1.4,
            curveness: 0.2,
            opacity: 0.85,
          },
          // 节点样式
          itemStyle: {
            color: this.palette[0],
            borderColor: '#fff',
            borderWidth: 2,
            shadowBlur: 8,
            shadowColor: 'rgba(15, 23, 42, 0.18)',
            shadowOffsetY: 2,
          },
          // 高亮
          emphasis: {
            focus: 'ancestor',
            scale: true,
            scaleSize: 2,
            itemStyle: {
              shadowBlur: 12,
              shadowColor: 'rgba(30, 108, 232, 0.5)',
              borderColor: '#fff',
              borderWidth: 2,
            },
            lineStyle: {
              width: 2.2,
              color: '#087f7a',
            },
          },
          // 文本（非叶子/父节点：标签伸进下一段间距，需截短以适配 ~110px 段距）
          label: {
            show: true,
            position: this.nonLeafLabelPosition,
            distance: 8,
            formatter: (params: any) => {
              const data = params.data as any;
              return data?.name || '';
            },
            fontSize,
            fontWeight: 600,
            color: '#183444',
            backgroundColor: 'transparent',
            overflow: 'truncate',
            width: parentLabelWidth,
          },
          // 叶子节点样式（叶子标签可伸进右侧大留白，保持较宽，悬停 tooltip 显示全名）
          leaves: {
            label: {
              show: true,
              position: 'right',
              distance: 10,
              formatter: (params: any) => {
                const data = params.data as any;
                return data?.name || '';
              },
              fontSize,
              fontWeight: 600,
              color: '#183444',
              overflow: 'truncate',
              width: labelWidth,
            },
          },
          // 标签自动避让：第三级节点密集时隐藏重叠文字，保证不出现压盖
          labelLayout: {
            hideOverlap: true,
          },
        },
      ],
    };
    this.chart.setOption(option, { notMerge: true });
    // 仅在初次构建（无用户搜索词）时自动适配缩放，避免干扰搜索/展开后的视图
    if (!this.searchTerm) {
      this.scheduleInitialFit();
    }
  }

  // ============= 构造树形数据 =============
  private buildTreeData(chapters: ChapterDto[], depth: number): any[] {
    if (!chapters || chapters.length === 0) {
      return [{ name: '暂无章节', itemStyle: { color: '#94a3b8' } }];
    }

    return [...chapters]
      .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0))
      .map(c => this.processChapter(c, depth));
  }

  private processChapter(chapter: ChapterDto, depth: number): any {
      const subCount = (chapter.children || []).length;
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
        // ⚠️ 注意：不要在这里设置 `collapsed` 字段！
        // ECharts 源码：node.isExpand = item && item.collapsed != null ? !item.collapsed : node.depth <= initialTreeDepth
        // 只要 collapsed 字段存在（即使是 false），就会走 !collapsed 分支，永远展开，
        // 完全覆盖 initialTreeDepth 的初始展开控制。展开/折叠只走 initialTreeDepth 与节点点击。
        itemStyle: {
          color,
          borderColor: isHighlighted ? '#f59e0b' : '#fff',
          borderWidth: isHighlighted ? 3 : 2,
          shadowBlur: isHighlighted ? 12 : 8,
          shadowColor: isHighlighted
            ? 'rgba(245, 158, 11, 0.55)'
            : this.hexToRgba(color, 0.35),
          shadowOffsetY: 2,
        },
      };

      if (chapter.children && chapter.children.length > 0) {
        const sortedChildren = [...chapter.children]
          .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0))
          .map(c => this.processChapter(c, depth + 1));
        node.children = sortedChildren;
      }

      return node;
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
