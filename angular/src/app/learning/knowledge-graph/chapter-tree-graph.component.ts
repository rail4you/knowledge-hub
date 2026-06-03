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
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzInputModule } from 'ng-zorro-antd/input';
import * as echarts from 'echarts/core';
import { TreeChart, GraphChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import {
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  ToolboxComponent,
} from 'echarts/components';

echarts.use([
  TreeChart,
  GraphChart,
  CanvasRenderer,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  ToolboxComponent,
]);

interface KnowledgeResourceDto {
  id: string;
  name: string;
  description?: string;
  importanceLevel?: string;
  difficulty?: number;
  sortOrder?: number;
}

interface ChapterDto {
  id: string;
  courseId?: string;
  parentId?: string;
  title?: string;
  description?: string;
  sortOrder?: number;
  knowledgeResources?: KnowledgeResourceDto[];
  children?: ChapterDto[];
}

type LayoutOrient = 'LR' | 'RL' | 'TB' | 'BT';
type LayoutMode = 'tree' | 'radial';

@Component({
  selector: 'app-chapter-tree-graph',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzTooltipModule,
    NzDividerModule,
    NzEmptyModule,
    NzRadioModule,
    NzInputModule,
  ],
  template: `
    <div class="kg-shell" [class.kg-shell--has-detail]="!!selectedNode()">
      <!-- 画布主区 -->
      <div class="kg-main">
        <!-- 浮动工具栏 (glassmorphism) -->
        <div class="kg-floating-toolbar">
          <div class="kg-toolbar__title">
            <span class="kg-toolbar__icon">
              <span nz-icon nzType="apartment" nzTheme="outline"></span>
            </span>
            <div class="kg-toolbar__title-text">
              <strong>课程知识图谱</strong>
              <span class="kg-toolbar__subtitle">交互式导航 · 拖拽 / 滚轮 / 点击</span>
            </div>
          </div>

          <div class="kg-toolbar__divider"></div>

          <div class="kg-toolbar__stats">
            <span class="kg-stat-pill">
              <span nz-icon nzType="folder" nzTheme="outline"></span>
              {{ chapterCount() }} 章
            </span>
            <span class="kg-stat-pill kg-stat-pill--accent">
              <span nz-icon nzType="bulb" nzTheme="outline"></span>
              {{ knowledgeCount() }} 知识点
            </span>
          </div>

          <div class="kg-toolbar__divider"></div>

          <nz-input-group
            [nzPrefix]="searchPrefix"
            class="kg-search"
            nzSize="small"
          >
            <input
              nz-input
              placeholder="搜索节点..."
              [(ngModel)]="searchTerm"
              (ngModelChange)="onSearchChange()"
            />
          </nz-input-group>
          <ng-template #searchPrefix>
            <span nz-icon nzType="search"></span>
          </ng-template>

          <div class="kg-toolbar__divider"></div>

          <nz-radio-group
            [(ngModel)]="layoutMode"
            (ngModelChange)="onLayoutChange()"
            nzSize="small"
            nzButtonStyle="solid"
            nz-tooltip
            nzTooltipTitle="切换布局"
          >
            <label nz-radio-button nzValue="tree">树形</label>
            <label nz-radio-button nzValue="radial">辐射</label>
          </nz-radio-group>

          @if (layoutMode === 'tree') {
            <nz-radio-group
              [(ngModel)]="orient"
              (ngModelChange)="onOrientChange()"
              nzSize="small"
              nzButtonStyle="solid"
              nz-tooltip
              nzTooltipTitle="切换方向"
            >
              <label nz-radio-button nzValue="LR">→</label>
              <label nz-radio-button nzValue="RL">←</label>
              <label nz-radio-button nzValue="TB">↓</label>
              <label nz-radio-button nzValue="BT">↑</label>
            </nz-radio-group>
          }

          <div class="kg-toolbar__divider"></div>

          <div class="kg-toolbar__actions">
            <button
              class="kg-icon-btn"
              nz-tooltip
              nzTooltipTitle="放大"
              (click)="zoomIn()"
            >
              <span nz-icon nzType="plus" nzTheme="outline"></span>
            </button>
            <button
              class="kg-icon-btn"
              nz-tooltip
              nzTooltipTitle="缩小"
              (click)="zoomOut()"
            >
              <span nz-icon nzType="minus" nzTheme="outline"></span>
            </button>
            <button
              class="kg-icon-btn"
              nz-tooltip
              nzTooltipTitle="适配视图"
              (click)="fitView()"
            >
              <span nz-icon nzType="scan" nzTheme="outline"></span>
            </button>
            <button
              class="kg-icon-btn"
              nz-tooltip
              nzTooltipTitle="展开全部"
              (click)="expandAll()"
            >
              <span nz-icon nzType="arrows-alt" nzTheme="outline"></span>
            </button>
            <button
              class="kg-icon-btn"
              nz-tooltip
              nzTooltipTitle="折叠全部"
              (click)="collapseAll()"
            >
              <span nz-icon nzType="shrink" nzTheme="outline"></span>
            </button>
          </div>
        </div>

        <!-- 图谱画布 -->
        <div class="kg-canvas-wrap">
          <!-- 网格背景 -->
          <div class="kg-grid" aria-hidden="true"></div>

          @if (!chapters || chapters.length === 0) {
            <div class="kg-empty">
              <span nz-icon nzType="apartment" nzTheme="outline" class="kg-empty__icon"></span>
              <p>暂无章节数据用于构建知识图谱</p>
            </div>
          } @else {
            <div #chartContainer class="kg-canvas"></div>

            <!-- 浮层：缩放比例 -->
            <div class="kg-zoom-indicator">
              <span class="kg-zoom-indicator__bar" [style.width.%]="zoomPercent()"></span>
              <span nz-icon nzType="zoom-in" nzTheme="outline"></span>
              <span>{{ zoomPercent() }}%</span>
            </div>

            <!-- 浮层：操作提示 -->
            <div class="kg-hint">
              <span><span nz-icon nzType="mouse" nzTheme="outline"></span> 滚轮缩放</span>
              <span class="kg-hint__sep">·</span>
              <span><span nz-icon nzType="drag" nzTheme="outline"></span> 拖拽移动</span>
              <span class="kg-hint__sep">·</span>
              <span><span nz-icon nzType="select" nzTheme="outline"></span> 点击节点</span>
              <span class="kg-hint__sep">·</span>
              <span><span nz-icon nzType="double-right" nzTheme="outline"></span> 双击聚焦</span>
            </div>

            <!-- 缩略图 -->
            <div class="kg-minimap" #minimapContainer>
              <div class="kg-minimap__title">
                <span nz-icon nzType="compass" nzTheme="outline"></span>
                导航
              </div>
              <div class="kg-minimap__canvas" #minimapCanvas></div>
              <div
                class="kg-minimap__viewport"
                [style.left.%]="minimapViewport().left"
                [style.top.%]="minimapViewport().top"
                [style.width.%]="minimapViewport().width"
                [style.height.%]="minimapViewport().height"
              ></div>
            </div>
          }
        </div>

        <!-- 底部图例 -->
        <div class="kg-legend">
          <span class="kg-legend__title">难度</span>
          <span class="kg-legend__item">
            <span class="kg-legend__dot" style="background: #34d399;"></span>
            入门
          </span>
          <span class="kg-legend__item">
            <span class="kg-legend__dot" style="background: #22c55e;"></span>
            初级
          </span>
          <span class="kg-legend__item">
            <span class="kg-legend__dot" style="background: #3b82f6;"></span>
            中级
          </span>
          <span class="kg-legend__item">
            <span class="kg-legend__dot" style="background: #f59e0b;"></span>
            高级
          </span>
          <span class="kg-legend__item">
            <span class="kg-legend__dot" style="background: #ef4444;"></span>
            专家
          </span>
          <nz-divider nzType="vertical" class="kg-toolbar__divider"></nz-divider>
          <span class="kg-legend__title">类型</span>
          <span class="kg-legend__item">
            <span class="kg-legend__square kg-legend__square--parent"></span>
            父章节
          </span>
          <span class="kg-legend__item">
            <span class="kg-legend__square kg-legend__square--child"></span>
            子章节
          </span>
          <span class="kg-legend__item">
            <span class="kg-legend__circle"></span>
            知识点
          </span>
        </div>
      </div>

      <!-- 选中节点详情面板 -->
      @if (selectedNode(); as node) {
        <aside class="kg-detail">
          <div class="kg-detail__head" [style.background]="getHeaderGradient(node)">
            <div class="kg-detail__head-pattern" aria-hidden="true">
              <span class="kg-pattern-dot kg-pattern-dot--1"></span>
              <span class="kg-pattern-dot kg-pattern-dot--2"></span>
              <span class="kg-pattern-dot kg-pattern-dot--3"></span>
            </div>
            <div class="kg-detail__head-top">
              <span class="kg-detail__type" [class]="'kg-detail__type--' + node.kind">
                <span nz-icon [nzType]="node.kind === 'chapter' ? 'folder' : 'bulb'" nzTheme="outline"></span>
                {{ getKindLabel(node) }}
              </span>
              <button class="kg-detail__close" (click)="clearSelection()" nz-tooltip nzTooltipTitle="关闭 (Esc)">
                <span nz-icon nzType="close" nzTheme="outline"></span>
              </button>
            </div>
            <h3 class="kg-detail__title">{{ node.title }}</h3>
            @if (node.subtitle) {
              <p class="kg-detail__subtitle">{{ node.subtitle }}</p>
            }
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
            @if (node.kind === 'chapter') {
              <section class="kg-detail__section">
                <h4 class="kg-detail__label">
                  <span nz-icon nzType="bar-chart" nzTheme="outline"></span>
                  统计
                </h4>
                <div class="kg-detail__stats">
                  <div class="kg-detail__stat">
                    <strong>{{ node.subChapterCount || 0 }}</strong>
                    <span>子章节</span>
                  </div>
                  <div class="kg-detail__stat">
                    <strong>{{ node.knowledgeCount || 0 }}</strong>
                    <span>知识点</span>
                  </div>
                </div>
              </section>
            }
            @if (node.kind === 'knowledge') {
              <section class="kg-detail__section">
                <h4 class="kg-detail__label">
                  <span nz-icon nzType="thunderbolt" nzTheme="outline"></span>
                  属性
                </h4>
                <div class="kg-detail__chips">
                  <span
                    class="kg-detail__chip"
                    [style.background]="getDifficultyBg(node.difficulty || 1)"
                    [style.color]="getDifficultyColor(node.difficulty || 1)"
                  >
                    <span nz-icon nzType="thunderbolt" nzTheme="fill"></span>
                    {{ getDifficultyLabel(node.difficulty || 1) }}
                  </span>
                  @if (node.importance) {
                    <span
                      class="kg-detail__chip"
                      [style.background]="getImportanceBg(node.importance)"
                      [style.color]="getImportanceColor(node.importance)"
                    >
                      <span nz-icon nzType="star" nzTheme="fill"></span>
                      {{ getImportanceLabel(node.importance) }}
                    </span>
                  }
                </div>
              </section>
            }
            @if (node.kind === 'chapter' && node.knowledges && node.knowledges.length > 0) {
              <section class="kg-detail__section">
                <h4 class="kg-detail__label">
                  <span nz-icon nzType="bulb" nzTheme="outline"></span>
                  包含知识点 ({{ node.knowledges.length }})
                </h4>
                <ul class="kg-detail__list">
                  @for (k of node.knowledges; track k.id) {
                    <li
                      class="kg-detail__list-item"
                      (click)="selectKnowledge(k)"
                      [style.borderLeftColor]="getDifficultyColor(k.difficulty || 1)"
                    >
                      <span class="kg-detail__list-name">{{ k.name }}</span>
                      <span
                        class="kg-detail__list-tag"
                        [style.color]="getDifficultyColor(k.difficulty || 1)"
                      >
                        {{ getDifficultyLabel(k.difficulty || 1) }}
                      </span>
                    </li>
                  }
                </ul>
              </section>
            }
            @if (node.kind === 'chapter' && node.subChapters && node.subChapters.length > 0) {
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
                      [style.borderLeftColor]="'#1e6ce8'"
                    >
                      <span class="kg-detail__list-name">{{ c.title }}</span>
                      @if ((c.knowledgeResources?.length || 0) > 0) {
                        <span class="kg-detail__list-tag">
                          {{ c.knowledgeResources?.length }} 知识点
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
  private readonly minimapContainer = viewChild<ElementRef>('minimapContainer');
  private readonly minimapCanvas = viewChild<ElementRef>('minimapCanvas');
  private chart: echarts.ECharts | null = null;
  private minimapChart: echarts.ECharts | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private nodePositions = new Map<string, { x: number; y: number }>();

  selectedNode = signal<any | null>(null);
  zoomPercent = signal<number>(100);
  minimapViewport = signal({ left: 0, top: 0, width: 30, height: 30 });

  /** 折叠状态：记录被手动折叠的节点 id */
  private collapsedSet = new Set<string>();
  /** 搜索高亮节点 id */
  private highlightedSet = new Set<string>();

  /** 布局方向 */
  orient: LayoutOrient = 'LR';
  /** 布局模式 */
  layoutMode: LayoutMode = 'tree';
  /** 搜索词 */
  searchTerm = '';

  // ============= 派生统计 =============
  chapterCount = computed(() => this.countChapters(this.chapters));
  knowledgeCount = computed(() => this.countKnowledges(this.chapters));

  ngAfterViewInit() {
    this.initChart();
    // 等下一帧初始化 minimap（避免容器尚未 layout 完成）
    requestAnimationFrame(() => this.initMinimap());
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['chapters'] && this.chart) {
      this.updateChart();
      this.updateMinimap();
    }
  }

  ngOnDestroy() {
    this.resizeObserver?.disconnect();
    this.chart?.dispose();
    this.minimapChart?.dispose();
    this.chart = null;
    this.minimapChart = null;
  }

  @HostListener('window:resize')
  onWindowResize() {
    this.chart?.resize();
    this.minimapChart?.resize();
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    this.clearSelection();
  }

  // ============= 初始化图表 =============
  private initChart() {
    const container = this.chartContainer()?.nativeElement;
    if (!container) return;

    this.chart = echarts.init(container, null, {
      renderer: 'canvas',
    });
    this.updateChart();

    // 监听缩放/平移
    this.chart.on('graphRoam', () => {
      this.updateZoomPercent();
      this.updateMinimapViewport();
    });

    // 点击节点
    this.chart.on('click', (params: any) => {
      if (params.dataType === 'node') {
        const nodeData = params.data as any;
        if (nodeData?.extData) {
          this.selectedNode.set(nodeData.extData);
        }
      }
    });

    // 双击节点 → 居中聚焦
    this.chart.on('dblclick', (params: any) => {
      if (params.dataType === 'node' && this.chart) {
        this.focusNode(params.data?.id);
      }
    });

    // hover
    this.chart.on('mouseover', { dataType: 'node' }, () => {
      if (container) container.style.cursor = 'pointer';
    });
    this.chart.on('mouseout', { dataType: 'node' }, () => {
      if (container) container.style.cursor = 'grab';
    });

    // 自适应容器
    this.resizeObserver = new ResizeObserver(() => {
      this.chart?.resize();
      this.updateMinimapViewport();
    });
    this.resizeObserver.observe(container);
  }

  private initMinimap() {
    const canvas = this.minimapCanvas()?.nativeElement;
    if (!canvas) return;
    this.minimapChart = echarts.init(canvas, null, { renderer: 'canvas' });
    this.updateMinimap();
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
    // 重置为 100%
    this.chart.dispatchAction({
      type: 'graphRoam',
      zoom: 1 / (this.zoomPercent() / 100),
      originX: this.chart.getWidth() / 2,
      originY: this.chart.getHeight() / 2,
    } as any);
    this.zoomPercent.set(100);
  }

  /** 聚焦到指定节点 */
  focusNode(nodeId?: string) {
    if (!this.chart) return;
    const targetId = nodeId || this.selectedNode()?.id;
    if (!targetId) return;
    const pos = this.nodePositions.get(targetId);
    if (!pos) return;
    this.chart.dispatchAction({
      type: 'graphRoam',
      zoom: 1.2,
      targetX: pos.x,
      targetY: pos.y,
    } as any);
  }

  expandAll() {
    this.collapsedSet.clear();
    this.refreshChart();
  }

  collapseAll() {
    this.collapsedSet.clear();
    const collect = (nodes: ChapterDto[]) => {
      nodes.forEach(n => {
        if ((n.children && n.children.length > 0) ||
            (n.knowledgeResources && n.knowledgeResources.length > 0)) {
          this.collapsedSet.add(n.id!);
        }
        if (n.children) collect(n.children);
      });
    };
    collect(this.chapters);
    this.refreshChart();
  }

  onOrientChange() {
    this.refreshChart();
  }

  onLayoutChange() {
    this.refreshChart();
  }

  onSearchChange() {
    this.highlightedSet.clear();
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.trim().toLowerCase();
      const match = (n: ChapterDto) =>
        (n.title || '').toLowerCase().includes(term) ||
        (n.description || '').toLowerCase().includes(term);
      const matchKn = (k: KnowledgeResourceDto) =>
        (k.name || '').toLowerCase().includes(term) ||
        (k.description || '').toLowerCase().includes(term);
      const walk = (nodes: ChapterDto[], parents: string[] = []) => {
        nodes.forEach(n => {
          const path = [...parents, n.id!];
          if (match(n)) {
            path.forEach(id => this.highlightedSet.add(id));
            this.highlightedSet.add(n.id!);
          }
          (n.knowledgeResources || []).forEach(k => {
            if (matchKn(k)) {
              path.forEach(id => this.highlightedSet.add(id));
              this.highlightedSet.add(n.id!);
            }
          });
          if (n.children) walk(n.children, path);
        });
      };
      walk(this.chapters);
    }
    this.refreshChart();
  }

  private refreshChart() {
    if (!this.chart) return;
    if (this.layoutMode === 'radial') {
      this.applyRadialOption();
    } else {
      const treeData = this.buildTreeData(this.chapters);
      this.applyTreeOption(treeData);
    }
    this.updateMinimap();
  }

  clearSelection() {
    this.selectedNode.set(null);
  }

  // ============= 详情面板操作 =============
  selectKnowledge(k: KnowledgeResourceDto) {
    this.selectedNode.set({
      kind: 'knowledge',
      id: k.id,
      title: k.name,
      description: k.description,
      difficulty: k.difficulty,
      importance: k.importanceLevel,
      subtitle: '知识点',
    });
    this.focusNode(k.id);
  }

  selectChapter(c: ChapterDto) {
    const knowledges = (c.knowledgeResources || []).map(k => k);
    const subChapters = (c.children || []).map(x => x);
    this.selectedNode.set({
      kind: 'chapter',
      id: c.id,
      title: c.title,
      description: c.description,
      subChapterCount: subChapters.length,
      knowledgeCount: knowledges.length,
      knowledges,
      subChapters,
      subtitle: '章节',
    });
    this.focusNode(c.id);
  }

  // ============= 工具方法 =============
  getKindLabel(node: any): string {
    return node.kind === 'chapter' ? '章节' : '知识点';
  }

  getHeaderGradient(node: any): string {
    if (node.kind === 'chapter') {
      return 'linear-gradient(135deg, #1e6ce8 0%, #0c4cb8 100%)';
    }
    const diff = node.difficulty || 1;
    if (diff <= 2) return 'linear-gradient(135deg, #34d399 0%, #10b981 100%)';
    if (diff === 3) return 'linear-gradient(135deg, #3b82f6 0%, #1e6ce8 100%)';
    if (diff === 4) return 'linear-gradient(135deg, #fbbf24 0%, #f59e0b 100%)';
    return 'linear-gradient(135deg, #fb7185 0%, #ef4444 100%)';
  }

  getDifficultyColor(d: number): string {
    const colors = ['#34d399', '#22c55e', '#3b82f6', '#f59e0b', '#ef4444'];
    return colors[Math.min(Math.max(d - 1, 0), 4)] || '#94a3b8';
  }

  getDifficultyBg(d: number): string {
    return this.getDifficultyColor(d) + '22';
  }

  getDifficultyLabel(d: number): string {
    const labels = ['入门', '初级', '中级', '高级', '专家'];
    return labels[Math.min(Math.max(d - 1, 0), 4)] || '未设置';
  }

  getImportanceColor(level?: string): string {
    const map: Record<string, string> = {
      core: '#ef4444',
      important: '#f59e0b',
      normal: '#1e6ce8',
      extended: '#10b981',
    };
    return map[level || 'normal'] || '#1e6ce8';
  }

  getImportanceBg(level?: string): string {
    return this.getImportanceColor(level) + '22';
  }

  getImportanceLabel(level?: string): string {
    const map: Record<string, string> = {
      core: '核心',
      important: '重要',
      normal: '一般',
      extended: '拓展',
    };
    return map[level || 'normal'] || '一般';
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

  private countKnowledges(list: ChapterDto[]): number {
    let total = 0;
    const walk = (arr: ChapterDto[]) => {
      arr.forEach(n => {
        total += (n.knowledgeResources || []).length;
        if (n.children) walk(n.children);
      });
    };
    walk(list || []);
    return total;
  }

  // ============= 树形 option =============
  private updateChart() {
    this.refreshChart();
  }

  private applyTreeOption(treeData: any[]) {
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
          'border-radius: 10px; box-shadow: 0 10px 32px rgba(0,0,0,0.28); backdrop-filter: blur(8px);',
        formatter: (params: any) => {
          const data = params.data as any;
          if (!data) return '';
          const meta = data.meta || {};
          if (meta.kind === 'knowledge') {
            return `
              <div style="font-weight:600;font-size:13px;margin-bottom:4px;">${escapeHtml(data.name)}</div>
              <div style="opacity:.85;font-size:11px;">${this.getDifficultyLabel(meta.difficulty || 1)} · 知识点</div>
            `;
          }
          return `
            <div style="font-weight:600;font-size:13px;margin-bottom:4px;">${escapeHtml(data.name)}</div>
            <div style="opacity:.85;font-size:11px;">${meta.subChapterCount || 0} 子章节 · ${meta.knowledgeCount || 0} 知识点</div>
          `;
        },
      },
      series: [
        {
          type: 'tree',
          name: '知识图谱',
          data: treeData,
          top: '3%',
          left: '2%',
          bottom: '3%',
          right: '18%',
          symbol: this.getSymbol,
          symbolSize: this.getSymbolSize,
          orient: this.orient,
          roam: true,
          nodeDraggable: false,
          initialTreeDepth: 2,
          expandAndCollapse: true,
          initialExpandDepth: 2,
          animationDuration: 700,
          animationDurationUpdate: 500,
          animationEasing: 'cubicOut',
          animationEasingUpdate: 'cubicInOut',
          // 边样式
          lineStyle: {
            color: 'source',
            width: 1.8,
            curveness: 0.55,
            opacity: 0.75,
          },
          // 父节点（章节）样式
          itemStyle: {
            color: '#1e6ce8',
            borderColor: '#fff',
            borderWidth: 3,
            borderRadius: 12,
            shadowBlur: 16,
            shadowColor: 'rgba(30, 108, 232, 0.45)',
            shadowOffsetY: 4,
          },
          // 高亮
          emphasis: {
            focus: 'ancestor',
            scale: true,
            itemStyle: {
              shadowBlur: 24,
              shadowColor: 'rgba(30, 108, 232, 0.65)',
              borderColor: '#fff',
              borderWidth: 4,
            },
            lineStyle: {
              width: 2.6,
              opacity: 1,
            },
          },
          // 文本（章节）
          label: {
            show: true,
            position: this.getLabelPosition(),
            distance: 8,
            formatter: (params: any) => this.formatChapterLabel(params),
            fontSize: 12,
            fontWeight: 600,
            color: '#1f2937',
            backgroundColor: 'rgba(255, 255, 255, 0.92)',
            padding: [4, 10],
            borderRadius: 6,
            borderColor: 'rgba(30, 108, 232, 0.18)',
            borderWidth: 1,
            shadowBlur: 4,
            shadowColor: 'rgba(15, 23, 42, 0.08)',
            shadowOffsetY: 1,
          },
          // 叶子节点（知识点）
          leaves: {
            label: {
              show: true,
              position: this.getLeafLabelPosition(),
              distance: 8,
              formatter: (params: any) => this.formatKnowledgeLabel(params),
              fontSize: 11,
              fontWeight: 600,
              color: '#fff',
              backgroundColor: 'transparent',
              borderWidth: 0,
              padding: 0,
            },
            itemStyle: {
              color: '#f59e0b',
              borderColor: '#fff',
              borderWidth: 3,
              borderRadius: 16,
              shadowBlur: 12,
              shadowColor: 'rgba(245, 158, 11, 0.5)',
              shadowOffsetY: 3,
            },
          },
        },
      ],
    };
    this.chart.setOption(option, { notMerge: true });
    // 收集节点位置（用于双击聚焦）
    this.collectNodePositions();
    this.updateMinimapViewport();
  }

  // ============= 辐射 option =============
  private applyRadialOption() {
    if (!this.chart) return;
    // 辐射布局使用 graph 系列的 circular layout
    const nodes: any[] = [];
    const links: any[] = [];
    const categories: any[] = [{ name: '核心' }, { name: '章节' }, { name: '知识点' }];

    let idx = 0;
    const visit = (chapter: ChapterDto, parentId?: string) => {
      const myId = `ch_${chapter.id}`;
      nodes.push({
        id: myId,
        name: chapter.title || '未命名',
        symbolSize: 56,
        category: 1,
        itemStyle: { color: '#1e6ce8' },
        label: { show: true, formatter: () => truncate(chapter.title || '', 10) },
        extData: { kind: 'chapter', id: chapter.id, title: chapter.title, description: chapter.description, subChapterCount: (chapter.children || []).length, knowledgeCount: (chapter.knowledgeResources || []).length, knowledges: chapter.knowledgeResources || [], subChapters: chapter.children || [], subtitle: '章节' },
      });
      if (parentId) {
        links.push({ source: parentId, target: myId });
      }
      idx++;
      (chapter.children || []).forEach(c => visit(c, myId));
      (chapter.knowledgeResources || []).forEach(k => {
        const knId = `kn_${k.id}`;
        nodes.push({
          id: knId,
          name: k.name,
          symbolSize: 18 + (k.difficulty || 1) * 3,
          category: 2,
          itemStyle: { color: this.getDifficultyColor(k.difficulty || 1) },
          label: { show: true, position: 'right', formatter: () => truncate(k.name, 8) },
          extData: { kind: 'knowledge', id: k.id, title: k.name, description: k.description, difficulty: k.difficulty, importance: k.importanceLevel, subtitle: '知识点' },
        });
        links.push({ source: myId, target: knId });
      });
    };
    this.chapters.forEach(c => visit(c));

    const option: echarts.EChartsCoreOption = {
      tooltip: {
        trigger: 'item',
        backgroundColor: 'rgba(15, 23, 42, 0.94)',
        borderWidth: 0,
        textStyle: { color: '#fff', fontSize: 12 },
        padding: [10, 14],
        extraCssText: 'border-radius: 10px;',
        formatter: (p: any) => {
          if (p.dataType === 'node') {
            const d = p.data;
            return `<div style="font-weight:600;font-size:13px;">${escapeHtml(d.name)}</div>`;
          }
          return '';
        },
      },
      legend: { show: false },
      animationDuration: 800,
      animationEasing: 'cubicOut',
      series: [
        {
          type: 'graph',
          layout: 'force',
          roam: true,
          draggable: true,
          data: nodes,
          links,
          categories,
          symbolSize: 32,
          label: { show: true, position: 'right', color: '#1f2937', fontSize: 11, fontWeight: 600 },
          force: { repulsion: 220, edgeLength: [60, 140], gravity: 0.05 },
          lineStyle: { color: 'source', width: 1.4, curveness: 0.25, opacity: 0.6 },
          itemStyle: { borderColor: '#fff', borderWidth: 2, shadowBlur: 10, shadowColor: 'rgba(15, 23, 42, 0.12)' },
          emphasis: { focus: 'adjacency', lineStyle: { width: 2.4 }, itemStyle: { shadowBlur: 18, shadowColor: 'rgba(30, 108, 232, 0.5)' } },
        },
      ],
    };
    this.chart.setOption(option, { notMerge: true });
    this.collectNodePositions();
    this.updateMinimapViewport();
  }

  private collectNodePositions() {
    if (!this.chart) return;
    this.nodePositions.clear();
    const opt = this.chart.getOption() as any;
    const series = opt?.series?.[0];
    if (!series) return;
    const nodes = (series.data || []).concat(series.nodes || []);
    nodes.forEach((n: any) => {
      if (n?.x != null && n?.y != null) {
        this.nodePositions.set(n.id, { x: n.x, y: n.y });
      }
    });
  }

  // ============= 自定义节点外观 =============
  private getSymbol = (_: any, params: any) => {
    const data = params?.data as any;
    if (!data) return 'roundRect';
    const meta = data.meta || {};
    if (meta.kind === 'knowledge') return 'circle';
    return 'roundRect';
  };

  private getSymbolSize = (_: any, params: any) => {
    const data = params?.data as any;
    if (!data) return [120, 40];
    const meta = data.meta || {};
    if (meta.kind === 'knowledge') {
      const d = meta.difficulty || 1;
      return 18 + d * 3;
    }
    const baseW = Math.min(Math.max((data.name?.length || 4) * 13 + 32, 100), 220);
    return [baseW, 40];
  };

  private getLabelPosition(): any {
    if (this.orient === 'LR') return 'right';
    if (this.orient === 'RL') return 'left';
    if (this.orient === 'TB') return 'bottom';
    return 'top';
  }

  private getLeafLabelPosition(): any {
    if (this.orient === 'LR') return 'right';
    if (this.orient === 'RL') return 'left';
    if (this.orient === 'TB') return 'bottom';
    return 'top';
  }

  private formatChapterLabel(params: any): string {
    const data = params.data as any;
    const name = data.name || '';
    const meta = data.meta || {};
    if (this.searchTerm && this.highlightedSet.has(meta.id)) {
      return `{hl|●} {name|${escapeHtml(truncate(name, 14))}}`;
    }
    return `{icon|●} {name|${escapeHtml(truncate(name, 14))}}`;
  }

  private formatKnowledgeLabel(params: any): string {
    const data = params.data as any;
    return `{name|${escapeHtml(truncate(data.name || '', 14))}}`;
  }

  // ============= 构造树形数据 =============
  private buildTreeData(chapters: ChapterDto[]): any[] {
    if (!chapters || chapters.length === 0) {
      return [{ name: '暂无章节', itemStyle: { color: '#94a3b8' } }];
    }

    const processChapter = (chapter: ChapterDto, isSub: boolean): any => {
      const subCount = (chapter.children || []).length;
      const knCount = (chapter.knowledgeResources || []).length;
      const isCollapsed = this.collapsedSet.has(chapter.id!);
      const isHighlighted = this.highlightedSet.has(chapter.id!);

      const node: any = {
        name: chapter.title || '未命名章节',
        id: `ch_${chapter.id}`,
        meta: {
          kind: 'chapter',
          id: chapter.id,
          isSub,
          subChapterCount: subCount,
          knowledgeCount: knCount,
        },
        extData: {
          kind: 'chapter',
          id: chapter.id,
          title: chapter.title,
          description: chapter.description,
          subChapterCount: subCount,
          knowledgeCount: knCount,
          knowledges: chapter.knowledgeResources || [],
          subChapters: chapter.children || [],
          subtitle: '章节',
        },
        collapsed: isCollapsed,
        itemStyle: isHighlighted
          ? { ...this.getChapterItemStyle(chapter, isSub), borderColor: '#f59e0b', borderWidth: 4, shadowColor: 'rgba(245, 158, 11, 0.7)' }
          : this.getChapterItemStyle(chapter, isSub),
      };

      if (chapter.children && chapter.children.length > 0) {
        const sortedChildren = [...chapter.children]
          .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0))
          .map(c => processChapter(c, true));
        node.children = sortedChildren;
      }

      if (chapter.knowledgeResources && chapter.knowledgeResources.length > 0) {
        const kns = chapter.knowledgeResources
          .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0))
          .map(kr => this.buildKnowledgeNode(kr));
        if (!node.children) {
          node.children = kns;
        } else {
          node.children = node.children.concat(kns);
        }
      }

      if (!node.children) {
        node.children = [];
      }

      return node;
    };

    return [...chapters]
      .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0))
      .map(c => processChapter(c, false));
  }

  private buildKnowledgeNode(kr: KnowledgeResourceDto): any {
    const d = kr.difficulty || 1;
    const isHighlighted = this.highlightedSet.has(kr.id!);
    return {
      name: kr.name,
      id: `kn_${kr.id}`,
      meta: {
        kind: 'knowledge',
        id: kr.id,
        difficulty: d,
        importance: kr.importanceLevel,
      },
      extData: {
        kind: 'knowledge',
        id: kr.id,
        title: kr.name,
        description: kr.description,
        difficulty: d,
        importance: kr.importanceLevel,
        subtitle: '知识点',
      },
      itemStyle: {
        color: this.getDifficultyColor(d),
        borderColor: isHighlighted ? '#f59e0b' : '#fff',
        borderWidth: isHighlighted ? 4 : 3,
        borderRadius: 16,
        shadowBlur: isHighlighted ? 18 : 12,
        shadowColor: isHighlighted ? 'rgba(245, 158, 11, 0.7)' : this.hexToRgba(this.getDifficultyColor(d), 0.55),
        shadowOffsetY: 3,
      },
    };
  }

  private getChapterItemStyle(chapter: ChapterDto, isSub: boolean): any {
    if (isSub) {
      return {
        color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [
          { offset: 0, color: '#10b981' },
          { offset: 1, color: '#047857' },
        ]),
        borderColor: '#fff',
        borderWidth: 3,
        borderRadius: 12,
        shadowBlur: 14,
        shadowColor: 'rgba(16, 185, 129, 0.45)',
        shadowOffsetY: 3,
      };
    }
    return {
      color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [
        { offset: 0, color: '#1e6ce8' },
        { offset: 1, color: '#0c4cb8' },
      ]),
      borderColor: '#fff',
      borderWidth: 3,
      borderRadius: 12,
      shadowBlur: 16,
      shadowColor: 'rgba(30, 108, 232, 0.45)',
      shadowOffsetY: 4,
    };
  }

  private hexToRgba(hex: string, alpha: number): string {
    if (!hex || hex[0] !== '#') return `rgba(148, 163, 184, ${alpha})`;
    const h = hex.replace('#', '');
    const r = parseInt(h.substring(0, 2), 16);
    const g = parseInt(h.substring(2, 4), 16);
    const b = parseInt(h.substring(4, 6), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }

  // ============= Minimap =============
  private updateMinimap() {
    if (!this.minimapChart) return;
    const treeData = this.buildTreeData(this.chapters);
    const option: echarts.EChartsCoreOption = {
      tooltip: { show: false },
      animation: false,
      series: [
        {
          type: 'tree',
          data: treeData,
          top: 5,
          left: 5,
          bottom: 5,
          right: 5,
          symbol: 'circle',
          symbolSize: 4,
          orient: 'LR',
          roam: false,
          nodeDraggable: false,
          expandAndCollapse: false,
          initialTreeDepth: -1,
          lineStyle: { color: '#94a3b8', width: 1, opacity: 0.5 },
          itemStyle: { color: '#1e6ce8', borderWidth: 0 },
          leaves: { itemStyle: { color: '#f59e0b' } },
          label: { show: false },
        },
      ],
    };
    this.minimapChart.setOption(option, { notMerge: true });
    // minimap 渲染完成后，更新可视区框
    setTimeout(() => this.updateMinimapViewport(), 100);
  }

  private updateMinimapViewport() {
    if (!this.chart) return;
    const opt = this.chart.getOption() as any;
    const series = opt?.series?.[0];
    if (!series) return;
    const dataArr: any[] = series.data || series.nodes || [];
    if (dataArr.length === 0) return;

    // 计算主图所有节点边界
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    dataArr.forEach((n: any) => {
      if (n?.x != null && n?.y != null) {
        if (n.x < minX) minX = n.x;
        if (n.x > maxX) maxX = n.x;
        if (n.y < minY) minY = n.y;
        if (n.y > maxY) maxY = n.y;
      }
    });
    if (!isFinite(minX)) return;
    const padding = 60;
    minX -= padding; minY -= padding;
    maxX += padding; maxY += padding;
    const totalW = maxX - minX;
    const totalH = maxY - minY;

    // 当前视口
    const zoom = series.zoom ?? 1;
    const panX = series.pan?.[0] ?? 0;
    const panY = series.pan?.[1] ?? 0;
    const viewW = this.chart.getWidth() / zoom;
    const viewH = this.chart.getHeight() / zoom;
    // 视口在节点坐标系中的位置
    const centerX = (this.chart.getWidth() / 2 - panX) / zoom;
    const centerY = (this.chart.getHeight() / 2 - panY) / zoom;
    const viewLeft = centerX - viewW / 2;
    const viewTop = centerY - viewH / 2;

    const leftPct = Math.max(0, ((viewLeft - minX) / totalW) * 100);
    const topPct = Math.max(0, ((viewTop - minY) / totalH) * 100);
    const widthPct = Math.min(100, (viewW / totalW) * 100);
    const heightPct = Math.min(100, (viewH / totalH) * 100);

    this.minimapViewport.set({
      left: leftPct,
      top: topPct,
      width: widthPct,
      height: heightPct,
    });
  }
}

function truncate(s: string, n: number): string {
  if (!s) return '';
  if (s.length <= n) return s;
  return s.slice(0, n - 1) + '…';
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}
