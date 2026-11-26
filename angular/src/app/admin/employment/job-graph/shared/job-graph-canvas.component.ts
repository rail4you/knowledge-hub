import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import type { JobCompetencyGraphDetailDto } from '../../../../proxy/employment/dtos/models';

/** 图谱节点（纯前端布局，只读展示） */
interface GraphNode {
  id: string;
  kind: 'job' | 'task' | 'competency' | 'course';
  x: number;
  y: number;
  w: number;
  h: number;
  title: string;
  subtitle?: string;
  achievement?: number | null;
  hasKnowledge?: boolean;
  isCurrent?: boolean;
}

interface GraphEdge {
  from: GraphNode;
  to: GraphNode;
}

interface GraphColumn {
  key: 'job' | 'task' | 'competency' | 'course';
  label: string;
  x: number;
  w: number;
}

/**
 * 岗位图谱画布（SVG 四列：岗位 → 核心任务 → 能力 → 课程）。
 *
 * 被两个页面复用：图谱入口页（右侧内联预览）与图谱详情页（顶部主视图）。
 *
 * 视觉约定（**颜色区分层级**，避免"一片灰看不出层次"）：
 * - 岗位：蓝　`job`；核心任务：紫　`task`；能力：绿（并按达成度调色）；课程：琥珀　`course`
 * - 每列有淡色列带 + 列名，横向即可看出"哪层是层"
 */
@Component({
  selector: 'app-job-graph-canvas',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './job-graph-canvas.component.html',
  styleUrls: ['./job-graph-canvas.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JobGraphCanvasComponent {
  readonly detail = input<JobCompetencyGraphDetailDto | null>(null);
  /** 当前课程（用于高亮课程节点） */
  readonly courseId = input<string | null>(null);
  /** 紧凑模式：入口页内联预览时压缩行距 */
  readonly compact = input(false);

  readonly layout = computed(() => {
    const detail = this.detail();
    if (!detail) {
      return { width: 0, height: 0, nodes: [] as GraphNode[], edges: [] as GraphEdge[], columns: [] as GraphColumn[] };
    }

    const rowGap = this.compact() ? 46 : 56;
    const nodeH = this.compact() ? 38 : 44;
    const top = 40; // 给列名留位置

    const colX = [24, 268, 556, 852];
    const nodeW = [200, 240, 240, 240];

    const columns: GraphColumn[] = [
      { key: 'job', label: '岗位', x: colX[0] - 12, w: nodeW[0] + 24 },
      { key: 'task', label: '核心任务', x: colX[1] - 12, w: nodeW[1] + 24 },
      { key: 'competency', label: '能力', x: colX[2] - 12, w: nodeW[2] + 24 },
      { key: 'course', label: '课程', x: colX[3] - 12, w: nodeW[3] + 24 },
    ];

    const nodes: GraphNode[] = [];
    const edges: GraphEdge[] = [];

    // ① 岗位
    const jobNode: GraphNode = {
      id: `job-${detail.id}`,
      kind: 'job',
      x: colX[0],
      y: top + 6,
      w: nodeW[0],
      h: nodeH + 12,
      title: detail.title,
      subtitle: detail.job?.companyName ?? '未关联岗位',
    };
    nodes.push(jobNode);

    // ② 核心任务
    const tasks = detail.tasks ?? [];
    const taskNodes = tasks.map((task, index) => {
      const node: GraphNode = {
        id: `task-${task.id}`,
        kind: 'task',
        x: colX[1],
        y: top + index * rowGap,
        w: nodeW[1],
        h: nodeH,
        title: task.title,
        subtitle: `权重 ${task.weight}`,
      };
      nodes.push(node);
      edges.push({ from: jobNode, to: node });
      return { task, node };
    });

    // ③ 能力（同一能力被多个任务引用时合并成一个节点）
    const competencyRows = new Map<
      string,
      {
        id: string;
        name: string;
        code?: string | null;
        level: number;
        achievement?: number | null;
        knowledgeCount: number;
        tasks: GraphNode[];
      }
    >();

    for (const { task, node: taskNode } of taskNodes) {
      for (const comp of task.competencies ?? []) {
        let row = competencyRows.get(comp.competencyId!);
        if (!row) {
          row = {
            id: comp.competencyId!,
            name: comp.name ?? '',
            code: comp.code,
            level: comp.requiredLevel ?? 1,
            achievement: comp.achievement,
            knowledgeCount: comp.knowledgeCount ?? 0,
            tasks: [],
          };
          competencyRows.set(comp.competencyId!, row);
        }
        row.tasks.push(taskNode);
        row.level = Math.max(row.level, comp.requiredLevel ?? 1);
        if (comp.knowledgeCount) row.knowledgeCount = comp.knowledgeCount;
        if (comp.achievement != null) row.achievement = comp.achievement;
      }
    }

    const competencyList = [...competencyRows.values()];
    const competencyNodes = competencyList.map((row, index) => {
      const node: GraphNode = {
        id: `comp-${row.id}`,
        kind: 'competency',
        x: colX[2],
        y: top + index * rowGap,
        w: nodeW[2],
        h: nodeH,
        title: (row.code ? `${row.code} ` : '') + row.name,
        subtitle:
          (row.knowledgeCount > 0 ? `知识点 ${row.knowledgeCount}` : '未挂知识点') +
          (row.achievement != null ? ` · 达成 ${Math.round(row.achievement * 100)}%` : ''),
        achievement: row.achievement ?? null,
        hasKnowledge: row.knowledgeCount > 0,
      };
      nodes.push(node);
      for (const taskNode of row.tasks) {
        edges.push({ from: taskNode, to: node });
      }
      return { row, node };
    });

    // ④ 课程（能力 → 支撑课程）
    const courseRows = new Map<string, { id: string; title: string; comps: GraphNode[] }>();
    for (const { row, node: compNode } of competencyNodes) {
      const rawCourses = tasks
        .flatMap(t => t.competencies ?? [])
        .filter(c => c.competencyId === row.id)
        .flatMap(c => c.courses ?? []);

      for (const c of rawCourses) {
        const key = c.courseId!;
        if (!courseRows.has(key)) {
          courseRows.set(key, { id: key, title: c.courseTitle ?? '', comps: [] });
        }
        const bucket = courseRows.get(key)!;
        if (!bucket.comps.some(x => x.id === compNode.id)) bucket.comps.push(compNode);
      }
    }

    const courseList = [...courseRows.values()];
    courseList.forEach((row, index) => {
      const node: GraphNode = {
        id: `course-${row.id}`,
        kind: 'course',
        x: colX[3],
        y: top + index * rowGap,
        w: nodeW[3],
        h: nodeH - 4,
        title: row.title,
        subtitle: this.courseId() === row.id ? '当前课程' : '',
        isCurrent: this.courseId() === row.id,
      };
      nodes.push(node);
      for (const compNode of row.comps) {
        edges.push({ from: compNode, to: node });
      }
    });

    const rows = Math.max(1, tasks.length, competencyList.length, courseList.length);
    return {
      width: colX[3] + nodeW[3] + 24,
      height: top + rows * rowGap + 16,
      nodes,
      edges,
      columns,
    };
  });

  /** SVG 里不做自动省略（中文字宽不稳），按字符数截断 */
  truncate(text: string | null | undefined, max: number): string {
    const value = text ?? '';
    return value.length > max ? value.slice(0, max - 1) + '…' : value;
  }
}
