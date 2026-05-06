import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  NgZone,
  OnChanges,
  OnDestroy,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import MindElixir, { LEFT, RIGHT, SIDE } from 'mind-elixir';
import type { NodeObj } from 'mind-elixir';
import type { ChapterDto } from '../../../proxy/courses/dtos/models';

interface NodeInfo {
  id: string;
  title: string;
  description: string;
  depth: number;
  sortOrder: number;
  childCount: number;
  isRoot: boolean;
}

@Component({
  selector: 'app-chapter-mind-map',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzCardModule, NzIconModule, NzTagModule],
  template: `
    <div class="graph-shell">
      <div class="graph-toolbar">
        <div class="toolbar-tip">
          <span nz-icon nzType="deployment-unit"></span>
          单击节点可展开或收起子节点，支持拖拽画布和滚轮缩放
        </div>
        <div class="toolbar-actions">
          <button nz-button nzSize="small" (click)="fitView()" title="适应画布">
            <span nz-icon nzType="border"></span>
            适应视图
          </button>
          <button nz-button nzSize="small" (click)="zoomIn()" title="放大">
            <span nz-icon nzType="plus"></span>
            放大
          </button>
          <button nz-button nzSize="small" (click)="zoomOut()" title="缩小">
            <span nz-icon nzType="minus"></span>
            缩小
          </button>
          <button nz-button nzSize="small" (click)="expandAll()" title="展开全部">
            <span nz-icon nzType="arrows-alt"></span>
            全部展开
          </button>
          <button nz-button nzSize="small" (click)="collapseAll()" title="收起全部">
            <span nz-icon nzType="shrink"></span>
            全部收起
          </button>
        </div>
      </div>

      <div class="graph-layout">
        <div class="graph-stage">
          <div #mindMapContainer class="mind-map-container"></div>
          @if (!initialized()) {
            <div class="graph-placeholder">
              <span nz-icon nzType="loading"></span>
              <span>知识图谱加载中...</span>
            </div>
          }
        </div>

        <aside class="graph-inspector">
          <nz-card nzTitle="节点信息" [nzSize]="'small'">
            @if (selectedNode(); as node) {
              <div class="detail-block">
                <div class="detail-title">{{ node.title }}</div>
                <div class="detail-tags">
                  <nz-tag [nzColor]="node.isRoot ? 'gold' : 'blue'">
                    {{ node.isRoot ? '课程根节点' : '章节节点' }}
                  </nz-tag>
                  <nz-tag>{{ '层级 ' + node.depth }}</nz-tag>
                  @if (node.childCount > 0) {
                    <nz-tag nzColor="green">{{ node.childCount + ' 个子节点' }}</nz-tag>
                  }
                </div>
              </div>

              <div class="detail-row">
                <div class="detail-label">排序</div>
                <div class="detail-value">{{ node.sortOrder }}</div>
              </div>

              <div class="detail-row">
                <div class="detail-label">说明</div>
                <div class="detail-value multiline">
                  {{ node.description || '暂无章节说明' }}
                </div>
              </div>
            } @else {
              <div class="empty-state">
                <span nz-icon nzType="node-index"></span>
                <p>点击任意节点可查看章节详情</p>
              </div>
            }
          </nz-card>
        </aside>
      </div>
    </div>
  `,
  styles: [`
    :host {
      display: block;
      height: 100%;
    }

    .graph-shell {
      height: 100%;
      display: flex;
      flex-direction: column;
      background:
        radial-gradient(circle at top left, rgba(85, 144, 255, 0.18), transparent 28%),
        radial-gradient(circle at bottom right, rgba(54, 207, 201, 0.16), transparent 24%),
        linear-gradient(180deg, #f7fbff 0%, #eef4fb 100%);
    }

    .graph-toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 14px 18px;
      border-bottom: 1px solid rgba(15, 50, 95, 0.08);
      background: rgba(255, 255, 255, 0.86);
      backdrop-filter: blur(18px);
    }

    .toolbar-tip {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      color: #37506e;
      font-size: 13px;
      font-weight: 500;
    }

    .toolbar-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      justify-content: flex-end;
    }

    .graph-layout {
      flex: 1;
      min-height: 0;
      display: grid;
      grid-template-columns: minmax(0, 1fr) 320px;
      gap: 16px;
      padding: 16px;
    }

    .graph-stage {
      position: relative;
      min-height: 0;
      border-radius: 24px;
      overflow: hidden;
      border: 1px solid rgba(116, 147, 185, 0.18);
      background: #ffffff;
      box-shadow: 0 24px 48px rgba(35, 74, 119, 0.12);
    }

    .mind-map-container {
      width: 100%;
      height: 100%;
    }

    .graph-placeholder {
      position: absolute;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 10px;
      color: #5f7690;
      font-size: 14px;
      background: rgba(255, 255, 255, 0.72);
      pointer-events: none;
    }

    .graph-inspector {
      min-width: 0;
    }

    .graph-inspector :is(.ant-card, .ant-card-body) {
      height: 100%;
    }

    .graph-inspector .ant-card {
      border-radius: 20px;
      border: 1px solid rgba(116, 147, 185, 0.18);
      box-shadow: 0 16px 36px rgba(35, 74, 119, 0.1);
      background: rgba(255, 255, 255, 0.92);
      backdrop-filter: blur(18px);
    }

    .detail-block {
      margin-bottom: 16px;
    }

    .detail-title {
      font-size: 18px;
      font-weight: 700;
      color: #12314f;
      line-height: 1.4;
      margin-bottom: 12px;
    }

    .detail-tags {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }

    .detail-row + .detail-row {
      margin-top: 16px;
    }

    .detail-label {
      margin-bottom: 6px;
      color: #6a829b;
      font-size: 12px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.08em;
    }

    .detail-value {
      color: #1c3652;
      font-size: 14px;
      line-height: 1.7;
    }

    .detail-value.multiline {
      white-space: pre-wrap;
      word-break: break-word;
    }

    .empty-state {
      height: 100%;
      min-height: 180px;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      color: #7d92a9;
      text-align: center;
      gap: 12px;
    }

    .empty-state span[nz-icon] {
      font-size: 30px;
      color: #91a7c0;
    }

    .empty-state p {
      margin: 0;
      font-size: 14px;
    }

    @media (max-width: 1100px) {
      .graph-layout {
        grid-template-columns: 1fr;
      }

      .graph-inspector {
        min-height: 220px;
      }
    }

    @media (max-width: 720px) {
      .graph-toolbar {
        align-items: flex-start;
        flex-direction: column;
      }

      .toolbar-actions {
        width: 100%;
        justify-content: flex-start;
      }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChapterMindMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  chapters = input<ChapterDto[]>([]);
  courseTitle = input<string>('');

  protected readonly selectedNode = signal<NodeInfo | null>(null);
  protected readonly initialized = signal(false);

  private readonly zone = inject(NgZone);
  private readonly containerRef = viewChild.required<ElementRef<HTMLDivElement>>('mindMapContainer');

  private me: InstanceType<typeof MindElixir> | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private initFailed = false;
  private scaleVal = 1;

  ngAfterViewInit() {
    this.zone.runOutsideAngular(() => {
      void this.initMindElixir();
      this.attachResizeObserver();
    });
  }

  ngOnChanges() {
    if (this.initFailed || !this.me) return;
    this.zone.runOutsideAngular(() => {
      this.refresh();
    });
  }

  ngOnDestroy() {
    this.resizeObserver?.disconnect();
    this.resizeObserver = null;
    if (this.me) {
      this.me.destroy();
      this.me = null;
    }
  }

  protected fitView() {
    if (!this.me) return;
    this.zone.runOutsideAngular(() => {
      this.me!.scaleFit();
    });
  }

  protected zoomIn() {
    if (!this.me) return;
    this.zone.runOutsideAngular(() => {
      const rect = this.containerRef().nativeElement.getBoundingClientRect();
      const cx = rect.width / 2;
      const cy = rect.height / 2;
      const newScale = this.scaleVal + 0.2;
      this.scaleVal = newScale;
      this.me!.scale(newScale, { x: cx, y: cy });
    });
  }


  protected zoomOut() {
    if (!this.me) return;
    this.zone.runOutsideAngular(() => {
      const rect = this.containerRef().nativeElement.getBoundingClientRect();
      const cx = rect.width / 2;
      const cy = rect.height / 2;
      const newScale = Math.max(0.3, this.scaleVal - 0.2);
      this.scaleVal = newScale;
      this.me!.scale(newScale, { x: cx, y: cy });
    });
  }

  protected expandAll() {
    if (!this.me) return;
    this.zone.runOutsideAngular(() => {
      const root = this.me!.findEle('course-root', this.containerRef().nativeElement);
      if (root) {
        this.me!.expandNodeAll(root, true);
      }
    });
  }

  protected collapseAll() {
    if (!this.me) return;
    this.zone.runOutsideAngular(() => {
      const root = this.me!.findEle('course-root', this.containerRef().nativeElement);
      if (root) {
        this.me!.expandNodeAll(root, false);
      }
    });
  }

  private async initMindElixir(): Promise<void> {
    const el = this.containerRef().nativeElement;

    try {
      this.me = new (MindElixir as any)({
        el,
        direction: SIDE,
        toolBar: false,
        contextMenu: false,
        nodeMenu: false,
        keypress: false,
        allowUndo: false,
        overflowHidden: true,
        scaleSensitivity: 0.5,
        scaleMax: 2.5,
        scaleMin: 0.3,
        handleWheel: true,
        editable: false,
        selectLocal: false,
      });

      this.me.install(() => {});

      this.bindEvents();

      const data = this.buildMindElixirData();
      (this.me as any).init(data);
      this.me.toCenter();

      this.initFailed = false;
      this.zone.run(() => this.initialized.set(true));
    } catch (err) {
      console.error('[ChapterMindMap] MindElixir init error:', err);
      this.initFailed = true;
    }
  }

  private bindEvents(): void {
    if (!this.me) return;

    const el = this.containerRef().nativeElement;

    el.addEventListener('selectNode', ((e: CustomEvent) => {
      const topic = e.detail as any;
      if (topic?.nodeObj) {
        const nodeObj = topic.nodeObj;
        const info = this.extractNodeInfo(nodeObj);
        this.zone.run(() => this.selectedNode.set(info));
      }
    }) as EventListener);

    el.addEventListener('unselectNode', (() => {
      this.zone.run(() => this.selectedNode.set(null));
    }) as EventListener);

    el.addEventListener('editNode', ((e: CustomEvent) => {
      const topic = e.detail as any;
      if (topic?.nodeObj) {
        const nodeObj = topic.nodeObj;
        const info = this.extractNodeInfo(nodeObj);
        this.zone.run(() => this.selectedNode.set(info));
      }
    }) as EventListener);
  }

  private extractNodeInfo(nodeObj: NodeObj): NodeInfo {
    return {
      id: (nodeObj as any).id || '',
      title: nodeObj.topic || '未命名节点',
      description: (nodeObj as any).note || '',
      depth: this.calcDepth(nodeObj),
      sortOrder: (nodeObj as any)._sortOrder ?? 0,
      childCount: nodeObj.children?.length ?? 0,
      isRoot: (nodeObj as any).id === 'course-root',
    };
  }

  private calcDepth(node: NodeObj): number {
    let depth = 0;
    let current: NodeObj | undefined = node;
    while (current?.parent) {
      depth++;
      current = current.parent;
    }
    return depth;
  }

  private buildMindElixirData() {
    const courseTitle = this.courseTitle().trim() || '课程知识图谱';
    const sortedChapters = [...this.chapters()].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));

    return {
      nodeData: {
        id: 'course-root',
        topic: courseTitle,
        note: '课程章节知识结构总览',
        expanded: true,
        children: sortedChapters.map((chapter, index) =>
          this.buildNodeObj(chapter, 1, `chapter-${index + 1}`)
        ),
      },
    };
  }

  private buildNodeObj(chapter: ChapterDto, depth: number, fallbackId: string): NodeObj {
    const nodeId = chapter.id || fallbackId;
    const title = chapter.title?.trim() || '未命名章节';
    const note = chapter.description?.trim() || '';
    const sortedChildren = [...(chapter.children ?? [])].sort(
      (a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0)
    );

    const node: NodeObj = {
      id: nodeId,
      topic: title,
      note,
      expanded: depth < 3,
      children: sortedChildren.map((child, index) =>
        this.buildNodeObj(child, depth + 1, `${nodeId}-${index + 1}`)
      ),
    };

    // Attach extra metadata via cast
    (node as any)._sortOrder = chapter.sortOrder ?? 0;
    (node as any)._depth = depth;

    return node;
  }

  private refresh(): void {
    if (!this.me) return;
    const data = this.buildMindElixirData();
    (this.me as any).refresh(data);
    this.me.toCenter();
  }

  private attachResizeObserver(): void {
    if (typeof ResizeObserver === 'undefined') return;

    this.resizeObserver = new ResizeObserver(() => {
      if (this.me) {
        this.me.scaleFit();
      }
    });

    this.resizeObserver.observe(this.containerRef().nativeElement);
  }
}
