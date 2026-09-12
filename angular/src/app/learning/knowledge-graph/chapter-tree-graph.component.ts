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
          <span class="kg-toolbar__spacer"></span>
          <button class="kg-icon-btn" nz-tooltip nzTooltipTitle="展开全部" (click)="expandAll()">
            <span nz-icon nzType="expand" nzTheme="outline"></span>
          </button>
          <button class="kg-icon-btn" nz-tooltip nzTooltipTitle="展开到二级目录" (click)="expandToLevel2()">
            <span nz-icon nzType="apartment" nzTheme="outline"></span>
          </button>
          <button class="kg-icon-btn" nz-tooltip nzTooltipTitle="只看一级目录" (click)="collapseToLevel1()">
            <span nz-icon nzType="compress" nzTheme="outline"></span>
          </button>
          <button class="kg-icon-btn" nz-tooltip nzTooltipTitle="放大" (click)="zoomIn()">
            <span nz-icon nzType="zoom-in" nzTheme="outline"></span>
          </button>
          <button class="kg-icon-btn" nz-tooltip nzTooltipTitle="缩小" (click)="zoomOut()">
            <span nz-icon nzType="zoom-out" nzTheme="outline"></span>
          </button>
          <button class="kg-icon-btn" nz-tooltip nzTooltipTitle="适应视图 (100%)" (click)="fitView()">
            <span nz-icon nzType="fullscreen" nzTheme="outline"></span>
          </button>
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
  /** 缩放上限 150% */
  private readonly MAX_ZOOM = 1.5;
  /** 当前绝对缩放比例 — 通过 treeRoam 事件的 delta 参数累乘得到 */
  private currentAbsoluteZoom = 1;
  /** 首次适配是否已执行，避免后续 ngOnChanges 重新适配 */
  private hasInitiallyFit = false;
  /** 非叶子节点标签位置：固定 'right'。曾按缩放动态切换到 'bottom'，
   * 但 treeRoam 回调里任何 setOption 都会重建整棵树、丢弃用户展开状态，
   * 故缩放路径上禁止 setOption，此处保持固定。 */
  private nonLeafLabelPosition: 'right' | 'bottom' = 'right';
  /**
   * 下一次 treeRoam 是由本组件派发的"回滚"事件，用于把视觉缩放拉回边界。
   * 在该事件中不要再次累乘 currentAbsoluteZoom（否则会把刚夹紧的值再次乘偏）。
   */
  private skipNextRoamMultiply = false;
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

  /** 图谱画布高度（普通方法而非 computed：需每次读取最新的 forceExpandAll，
   * 保证点“展开全部/收起”后高度立即刷新）：
   * 固定展示 3 个层级（课程根 + 一级 + 二级），高度按第三级可见行数撑高，
   * 保证第三级节点再多、文字也不互相重叠。行高按 32px 估算，夹紧到 520~1400px。
   */
  chartMinHeight(): number {
    const total = this.countChapters(this.chapters);
    const dataDepth = this.dataDepth();
    const maxSiblings = this.maxSiblingCount();

    // 深层数据：可见行主要是二级节点（另加无子节点的一级兜底）；
    // 全展开时可见行按总数估算，避免展开后挤作一团。
    if (dataDepth >= 3) {
      let rows: number;
      if (this.forceExpandAll) {
        rows = Math.max(total, this.secondLevelCount());
      } else {
        const childlessTop = (this.chapters || []).filter(c => !(c.children?.length || 0)).length;
        rows = this.secondLevelCount() + childlessTop;
      }
      // 每行约 32px + 上下留白，夹紧到 520~1400px
      return Math.max(520, Math.min(rows * 32 + 180, 1400));
    }
    if (total > 60) return 620;
    if (total > 30) return 560;

    // 2 级图谱：所有叶子纵向排列，需要按最多同级节点数撑高
    // 每个叶子 symbolSize=36 + 间距 ≈ 50px，上下各留 40px 呼吸空间
    const neededForLeaves = maxSiblings * 50 + 80;
    return Math.max(360, Math.min(neededForLeaves, 640));
  }

  /**
   * 默认展开深度（固定 3 个层级，按**视觉层级**算，课程根占第 0 层）：
   * - 数据本身只有 ≤2 级 → 全展开（-1），小图一眼看完；
   * - 深层数据（≥3 级）→ 固定展开到 2 级（视觉 3 层），不再按节点数量
   *   自适应切换到 2 层。第三级节点多时靠画布撑高 + 小字号保证不重叠。
   * 用户点“展开全部”后 forceExpandAll=true 保持全展开。
   */
  private defaultInitialDepth(): number {
    if (this.dataDepth() <= 2) return -1;
    return 2;
  }
  /** 用户点过“展开全部”后保持全展开，不再被默认折叠覆盖 */
  private forceExpandAll = false;

  ngAfterViewInit() {
    this.initChart();
  }

  ngOnChanges(changes: SimpleChanges) {
    // courseName 可能比 chapters 先到/后到（如课程详情先回来），任一变化都需重建
    if ((changes['chapters'] || changes['courseName']) && this.chart) {
      // 切换课程时重置视图状态，避免旧课程的折叠/缩放残留
      if (changes['chapters']) {
        this.collapsedSet.clear();
        this.forceExpandAll = false;
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
      // params.zoom 是相对增量（ECharts RoamController 内部约定）。
      if (params && typeof params.zoom === 'number') {
        // 本组件自己发起的"回滚到边界"事件：视觉缩放已被拉回，
        // 不要再次累乘 currentAbsoluteZoom（currentAbsoluteZoom 已经是边界值）。
        if (this.skipNextRoamMultiply) {
          this.skipNextRoamMultiply = false;
          return;
        }

        const targetZoom = this.currentAbsoluteZoom * params.zoom;
        if (targetZoom < this.MIN_ZOOM || targetZoom > this.MAX_ZOOM) {
          // 超出 [50%, 150%] 范围：把 currentAbsoluteZoom 夹紧到边界，
          // 然后派发一次反向 treeRoam 把视觉缩放也拉回边界。
          const clamped = Math.max(this.MIN_ZOOM, Math.min(this.MAX_ZOOM, targetZoom));
          const revertRatio = clamped / targetZoom;
          this.currentAbsoluteZoom = clamped;
          this.zoomPercent.set(Math.round(clamped * 100));
          this.skipNextRoamMultiply = true;
          this.chart?.dispatchAction({
            type: 'treeRoam',
            zoom: revertRatio,
            originX: this.chart.getWidth() / 2,
            originY: this.chart.getHeight() / 2,
          } as any);
          return;
        }

        this.currentAbsoluteZoom = targetZoom;
        const pct = Math.round(targetZoom * 100);
        this.zoomPercent.set(pct);
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
    // 初始适配也必须落在 [50%, 150%] 范围内
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
    const current = this.currentAbsoluteZoom;
    // 工具栏 +/- 按钮也遵守 [50%, 150%] 范围
    const next = Math.max(this.MIN_ZOOM, Math.min(this.MAX_ZOOM, current * zoomDelta));
    const ratio = next / current;
    if (ratio === 1) return; // 已在边界，无变化
    this.chart.dispatchAction({
      type: 'treeRoam',
      zoom: ratio,
      originX: this.chart.getWidth() / 2,
      originY: this.chart.getHeight() / 2,
    } as any);
    // treeRoam 事件回调会更新 currentAbsoluteZoom 和 zoomPercent
  }

  fitView() {
    if (!this.chart) return;
    const current = this.currentAbsoluteZoom;
    if (Math.abs(current - 1) < 0.01) return;
    this.chart.dispatchAction({
      type: 'treeRoam',
      zoom: 1 / current,
      originX: this.chart.getWidth() / 2,
      originY: this.chart.getHeight() / 2,
    } as any);
  }

  expandAll() {
    this.collapsedSet.clear();
    this.forceExpandAll = true;
    this.updateChart();
  }

  /** 默认态：只展开到一级（课程根 + 一级章节），深层全部折叠 */
  collapseToLevel1() {
    this.collapsedSet.clear();
    this.forceExpandAll = false;
    const collect = (nodes: ChapterDto[], depth: number) => {
      nodes.forEach(n => {
        // depth 与 processChapter 一致：顶层 chapters=0。
        // 折叠所有带子节点的顶层章节（depth>=0），与 initialTreeDepth=1 对齐
        // （只展示课程根 + 一级，视觉 2 层）。
        if (depth >= 0 && n.children && n.children.length > 0) {
          this.collapsedSet.add(n.id!);
        }
        if (n.children) collect(n.children, depth + 1);
      });
    };
    collect(this.chapters, 0);
    this.updateChart();
  }

  /** 按需展开到二级（课程根 + 一级 + 二级）：折叠第三级及更深的节点 */
  expandToLevel2() {
    this.collapsedSet.clear();
    this.forceExpandAll = false;
    const collect = (nodes: ChapterDto[], depth: number) => {
      nodes.forEach(n => {
        // 只折叠 depth>=1 的非叶子节点（即二级章节的子树），与旧“只看两级”一致。
        if (depth >= 1 && n.children && n.children.length > 0) {
          this.collapsedSet.add(n.id!);
        }
        if (n.children) collect(n.children, depth + 1);
      });
    };
    collect(this.chapters, 0);
    // 如果数据本身只有 ≤2 级，无需折叠，直接全展开
    if (this.dataDepth() <= 2) this.collapsedSet.clear();
    this.updateChart();
  }

  /** 兼容旧模板/调用：等价于收起到一级 */
  collapseToLevel2() {
    this.collapseToLevel1();
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
        color: this.palette[0], borderColor: '#fff', borderWidth: 3,
        shadowBlur: 18, shadowColor: this.hexToRgba(this.palette[0], 0.45), shadowOffsetY: 3,
      },
      label: { fontSize: 14, fontWeight: 700, color: '#1e6ce8' },
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
    // 第三级可见行数：行数越多字号/节点越小，配合撑高的画布保证文字不重叠
    const childlessTop = (this.chapters || []).filter(c => !(c.children?.length || 0)).length;
    const thirdLevelRows = this.secondLevelCount() + childlessTop;
    // 分档：节点小一号，标签小一号，减少拥挤
    let symbolSize: number;
    let fontSize: number;
    let labelWidth: number;
    if (dataDepth >= 3 && thirdLevelRows > 50) {
      symbolSize = 22; fontSize = 11; labelWidth = 120;
    } else if (dataDepth >= 3 && thirdLevelRows > 30) {
      symbolSize = 24; fontSize = 11; labelWidth = 140;
    } else if (isLarge || isFlatDense) {
      symbolSize = 28; fontSize = 12; labelWidth = 160;
    } else {
      symbolSize = 36; fontSize = 13; labelWidth = 160;
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
          left: 4,
          bottom: 12,
          right: '22%',
          symbol: 'circle',
          symbolSize,
          orient: 'LR',
          roam: true,
          // 缩放时节点符号与坐标系同比例放大，避免边缘被拉得过长、浪费空间
          nodeScaleRatio: 1,
          nodeDraggable: false,
          expandAndCollapse: true,
          // 默认展开层级见 defaultInitialDepth()：固定展示 3 个层级
          //（课程根 + 一级 + 二级），不再按节点数量自适应切换。
          // 数据本身 ≤2 级时全展开。用户点“展开全部”后
          // forceExpandAll=true 保持全展开。
          initialTreeDepth: this.forceExpandAll ? -1 : this.defaultInitialDepth(),
          animationDuration: 600,
          animationDurationUpdate: 500,
          animationEasing: 'cubicOut',
          animationEasingUpdate: 'cubicInOut',
          // 边样式
          lineStyle: {
            color: '#cbd5e1',
            width: 1.4,
            curveness: 0.3,
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
            position: this.nonLeafLabelPosition,
            distance: 10,
            formatter: (params: any) => {
              const data = params.data as any;
              return data?.name || '';
            },
            fontSize,
            fontWeight: 600,
            color: '#1f2937',
            backgroundColor: 'transparent',
            overflow: 'truncate',
            width: labelWidth,
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
              fontSize,
              fontWeight: 600,
              color: '#1f2937',
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
        // ⚠️ 注意：不要无条件设置 `collapsed: false`！
        // ECharts 源码：node.isExpand = item && item.collapsed != null ? !item.collapsed : node.depth <= initialTreeDepth
        // 只要 collapsed 字段存在（即使是 false），就会走 !collapsed 分支，永远展开，
        // 完全覆盖 initialTreeDepth 的初始展开控制。
        // 只在用户主动折叠时才设 true，其他情况不写这个字段。
        ...(isCollapsed ? { collapsed: true } : {}),
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
