import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';

import { KnowledgePointDetail, KnowledgePointService } from '../../learning-analytics/services/knowledge-point.service';

/** 图谱节点（含布局坐标） */
interface GraphNode {
  id: string;
  name: string;
  detail: KnowledgePointDetail;
  isExternal: boolean;
  courseTitle?: string | null;
  isMerged: boolean;
  /** 拓扑层级（列） */
  level: number;
  /** 层内序号（行） */
  row: number;
  x: number;
  y: number;
  width: number;
  height: number;
}

/** 图谱边（含绘制路径） */
interface GraphEdge {
  fromId: string;
  toId: string;
  type: 'prerequisite' | 'merge';
  status: number;
  strength: number;
  note?: string | null;
  path: string;
}

const NODE_WIDTH = 176;
const NODE_HEIGHT = 54;
const COLUMN_GAP = 72;
const ROW_GAP = 16;
const PADDING = 32;

/**
 * 知识点图谱：以「先修关系」为主干、叠加「合并关系」。
 * 采用分层（拓扑）布局：层级由后端按最长路径计算，层内按名称排序；
 * 被合并（已归档）的知识点单独放到最末层，用灰色虚线指向标准知识点。
 *
 * 不用 mind-elixir 之类的脑图库，是因为先修关系是 DAG（多入度），
 * 分层 SVG 更贴合语义，也避免再引入一个重量级依赖。
 */
@Component({
  selector: 'app-knowledge-point-graph',
  standalone: true,
  imports: [CommonModule, FormsModule, NzIconModule, NzSpinModule, NzEmptyModule],
  templateUrl: './knowledge-point-graph.component.html',
  styleUrls: ['./knowledge-point-graph.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KnowledgePointGraphComponent {
  private readonly kpService = inject(KnowledgePointService);

  readonly courseId = input.required<string>();
  readonly refreshToken = input<number>(0);
  readonly nodeSelected = output<KnowledgePointDetail>();

  readonly loading = signal(false);
  readonly nodes = signal<GraphNode[]>([]);
  readonly edges = signal<GraphEdge[]>([]);
  readonly hoveredId = signal<string | null>(null);
  readonly zoom = signal(1);
  readonly externalCount = signal(0);
  readonly proposedCount = signal(0);
  readonly mergedCount = signal(0);
  /** 只显示已确认先修边（默认显示全部） */
  showProposedEdges = true;

  readonly width = computed(() => {
    const list = this.nodes();
    if (!list.length) return 0;
    return Math.max(...list.map(n => n.x + n.width)) + PADDING;
  });

  readonly height = computed(() => {
    const list = this.nodes();
    if (!list.length) return 0;
    return Math.max(...list.map(n => n.y + n.height)) + PADDING;
  });

  readonly viewBox = computed(() => `0 0 ${Math.max(this.width(), 10)} ${Math.max(this.height(), 10)}`);

  /** 高亮相关边（hover 或选中） */
  private readonly highlightedId = computed(() => this.hoveredId());

  constructor() {
    // courseId 或 refreshToken 变化时重新加载图谱
    effect(() => {
      const id = this.courseId();
      // 读取 token 以建立依赖
      this.refreshToken();
      if (!id) return;
      untracked(() => this.load(id));
    });
  }

  load(courseId: string): void {
    if (!courseId) return;
    this.loading.set(true);
    this.kpService.getGraph(courseId).subscribe({
      next: graph => {
        this.externalCount.set(graph.externalNodeCount ?? 0);
        this.proposedCount.set(graph.proposedEdgeCount ?? 0);
        this.mergedCount.set(graph.mergedNodeCount ?? 0);
        const { nodes, edges } = this.layout(graph.nodes ?? [], graph.edges ?? []);
        this.nodes.set(nodes);
        this.edges.set(edges);
        this.loading.set(false);
      },
      error: () => {
        this.nodes.set([]);
        this.edges.set([]);
        this.loading.set(false);
      },
    });
  }

  // ══════════════ 布局 ══════════════

  private layout(rawNodes: any[], rawEdges: any[]): { nodes: GraphNode[]; edges: GraphEdge[] } {
    const maxLevel = rawNodes.reduce((max, n) => Math.max(max, n.level ?? 0), 0);

    const nodes: GraphNode[] = [];
    // 分层：正常节点按 level 分列；已归档（被合并）节点统一放到最后一列
    const columns = new Map<number, any[]>();
    for (const n of rawNodes) {
      const merged = !!n.kp?.canonicalId;
      const level = merged ? maxLevel + 1 : (n.level ?? 0);
      const list = columns.get(level) ?? [];
      list.push({ raw: n, merged, level });
      columns.set(level, list);
    }

    for (const [level, list] of [...columns.entries()].sort((a, b) => a[0] - b[0])) {
      list.sort((a, b) => (a.raw.kp?.name ?? '').localeCompare(b.raw.kp?.name ?? '', 'zh-Hans-CN'));
      list.forEach((item, row) => {
        const kp: KnowledgePointDetail = item.raw.kp;
        nodes.push({
          id: kp.id,
          name: kp.name,
          detail: kp,
          isExternal: !!item.raw.isExternal,
          courseTitle: item.raw.courseTitle,
          isMerged: item.merged,
          level,
          row,
          x: PADDING + level * (NODE_WIDTH + COLUMN_GAP),
          y: PADDING + row * (NODE_HEIGHT + ROW_GAP),
          width: NODE_WIDTH,
          height: NODE_HEIGHT,
        });
      });
    }

    const byId = new Map(nodes.map(n => [n.id, n]));
    const edges: GraphEdge[] = [];
    for (const e of rawEdges) {
      const from = byId.get(e.fromId);
      const to = byId.get(e.toId);
      if (!from || !to) continue;

      edges.push({
        fromId: e.fromId,
        toId: e.toId,
        type: e.type,
        status: e.status,
        strength: e.strength,
        note: e.note,
        path: this.edgePath(from, to),
      });
    }

    return { nodes, edges };
  }

  /** 三次贝塞尔：源节点右侧 → 目标节点左侧 */
  private edgePath(from: GraphNode, to: GraphNode): string {
    const x1 = from.x + from.width;
    const y1 = from.y + from.height / 2;
    const x2 = to.x;
    const y2 = to.y + to.height / 2;

    // 同一列（合并边可能同列）或反向（脏数据）时，用绕行曲线，避免压线
    if (x2 <= x1 + 8) {
      const midY = Math.max(y1, y2) + NODE_HEIGHT;
      return `M ${x1} ${y1} C ${x1 + 40} ${midY}, ${x2 - 40} ${midY}, ${x2} ${y2}`;
    }

    const dx = Math.max(40, (x2 - x1) / 2);
    return `M ${x1} ${y1} C ${x1 + dx} ${y1}, ${x2 - dx} ${y2}, ${x2} ${y2}`;
  }

  // ══════════════ 交互 ══════════════

  visibleEdges(): GraphEdge[] {
    return this.showProposedEdges ? this.edges() : this.edges().filter(e => e.type === 'merge' || e.status === 1);
  }

  isEdgeActive(edge: GraphEdge): boolean {
    const id = this.highlightedId();
    return !!id && (edge.fromId === id || edge.toId === id);
  }

  select(node: GraphNode): void {
    this.hoveredId.set(node.id);
    this.nodeSelected.emit(node.detail);
  }

  zoomIn(): void {
    this.zoom.update(z => Math.min(2, Math.round((z + 0.1) * 10) / 10));
  }

  zoomOut(): void {
    this.zoom.update(z => Math.max(0.4, Math.round((z - 0.1) * 10) / 10));
  }

  resetZoom(): void {
    this.zoom.set(1);
  }

  nodeClass(node: GraphNode): string {
    if (node.isMerged) return 'is-merged';
    if (node.isExternal) return 'is-external';
    if (node.detail.status === 1) return 'is-pending';
    return '';
  }

  nodeMeta(node: GraphNode): string {
    if (node.isMerged) return `已合并 → ${node.detail.canonicalName ?? '标准知识点'}`;
    if (node.isExternal) return `外部前置 · ${node.courseTitle || '其他课程'}`;
    const parts = [`习题 ${node.detail.exerciseCount}`];
    if (node.detail.hasCustomThreshold) parts.push(`目标 ${Math.round(node.detail.targetLine * 100)}%`);
    return parts.join(' · ');
  }

  edgeClass(edge: GraphEdge): string {
    if (edge.type === 'merge') return 'is-merge';
    if (edge.status !== 1) return 'is-proposed';
    return 'is-confirmed';
  }
}
