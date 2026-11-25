import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { CourseService } from '../../proxy/courses/course.service';
import { CourseContextService } from '../course-context.service';
import { IdeologyCaseStatus, IdeologyGraphDto, IdeologyGraphNode, IdeologyService } from './ideology.service';

interface CourseOption {
  id: string;
  title?: string;
}

/** 画布上的节点（含坐标，纯前端布局计算）。 */
interface PositionedNode extends IdeologyGraphNode {
  column: 'chapter' | 'case' | 'element';
  x: number;
  y: number;
  width: number;
  height: number;
}

interface PositionedEdge {
  kind: string;
  from: string;
  to: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  highlighted: boolean;
}

const COLUMN_X = { chapter: 24, case: 360, element: 720 } as const;
const NODE_WIDTH = { chapter: 260, case: 280, element: 200 } as const;
const NODE_HEIGHT = 34;
const ROW_GAP = 14;
const TOP_PADDING = 40;

/**
 * 思政图谱：章节 ⇄ 案例 ⇄ 思政元素 三层关联图。
 *
 * <p>不引入 d3 / echarts：布局是确定性的三列（章节 | 案例 | 元素），
 * 自己算坐标就够了，也更好做中文排版与主题适配。</p>
 *
 * <p>图的增删关联不在这里做 —— 关联的唯一事实来源是案例本身
 * （改章节 = 改案例 ChapterId，改元素 = 改案例 ElementIds），
 * 点节点会高亮关联并在底部给出「去案例库编辑」的入口。</p>
 */
@Component({
  selector: 'app-ideology-graph',
  standalone: true,
  imports: [CommonModule, FormsModule, NzCardModule, NzButtonModule, NzSelectModule, NzTagModule, NzIconModule, NzTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ideology-graph.component.html',
  styleUrls: ['./ideology-pages.scss'],
})
export class IdeologyGraphComponent implements OnInit {
  private readonly service = inject(IdeologyService);
  private readonly courseService = inject(CourseService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly courses = signal<CourseOption[]>([]);
  readonly courseId = signal<string | null>(null);
  readonly graph = signal<IdeologyGraphDto | null>(null);
  readonly loading = signal(false);
  readonly selectedId = signal<string | null>(null);

  readonly columnTitles = { chapter: '课程章节', case: '思政案例', element: '思政元素' } as const;

  readonly canvas = computed(() => {
    const graph = this.graph();
    if (!graph) {
      return { width: 0, height: 0, nodes: [] as PositionedNode[], edges: [] as PositionedEdge[] };
    }

    const selected = this.selectedId();
    const nodes: PositionedNode[] = [];
    const grouped: Record<'chapter' | 'case' | 'element', IdeologyGraphNode[]> = {
      chapter: graph.chapters,
      case: graph.cases,
      element: graph.elements,
    };

    (['chapter', 'case', 'element'] as const).forEach(column => {
      grouped[column].forEach((node, index) => {
        nodes.push({
          ...node,
          column,
          x: COLUMN_X[column],
          y: TOP_PADDING + index * (NODE_HEIGHT + ROW_GAP),
          width: NODE_WIDTH[column],
          height: NODE_HEIGHT,
        });
      });
    });

    const byId = new Map(nodes.map(n => [n.id, n]));
    const edges: PositionedEdge[] = [];
    for (const edge of graph.edges) {
      const from = byId.get(edge.from);
      const to = byId.get(edge.to);
      if (!from || !to) continue;

      // 案例在中间列：章节边从案例左缘连到章节右缘，元素边从案例右缘连到元素左缘。
      const caseNode = from.column === 'case' ? from : to;
      const sideNode = from.column === 'case' ? to : from;
      const leftward = sideNode.column === 'chapter';

      edges.push({
        kind: edge.kind,
        from: edge.from,
        to: edge.to,
        x1: leftward ? caseNode.x : caseNode.x + caseNode.width,
        y1: caseNode.y + caseNode.height / 2,
        x2: leftward ? sideNode.x + sideNode.width : sideNode.x,
        y2: sideNode.y + sideNode.height / 2,
        highlighted: !!selected && (edge.from === selected || edge.to === selected),
      });
    }

    const rows = Math.max(grouped.chapter.length, grouped.case.length, grouped.element.length, 1);
    return {
      width: COLUMN_X.element + NODE_WIDTH.element + 24,
      height: TOP_PADDING * 2 + rows * (NODE_HEIGHT + ROW_GAP),
      nodes,
      edges,
    };
  });

  readonly selectedNode = computed(() => {
    const id = this.selectedId();
    return id ? this.canvas().nodes.find(n => n.id === id) ?? null : null;
  });

  ngOnInit(): void {
    const fromQuery = this.route.snapshot.queryParamMap.get('courseId');
    this.loadCourses(fromQuery || this.courseContext.currentCourseId());
  }

  private loadCourses(initial: string | null): void {
    this.courseService.getMyCourses({ maxResultCount: 200, skipCount: 0 }).subscribe({
      next: list => {
        this.courses.set((list.items || []).map(c => ({ id: c.id!, title: c.title })));
        const target =
          initial && this.courses().some(c => c.id === initial) ? initial : this.courses()[0]?.id ?? null;
        if (target) {
          this.selectCourse(target);
        }
      },
      error: () => this.courses.set([]),
    });
  }

  selectCourse(courseId: string): void {
    this.courseId.set(courseId);
    this.courseContext.setCourse(courseId);
    this.selectedId.set(null);
    this.reload();
  }

  onCourseChange(courseId: string): void {
    this.selectCourse(courseId);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { courseId },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  reload(): void {
    const courseId = this.courseId();
    if (!courseId) return;

    this.loading.set(true);
    this.service.getGraph(courseId).subscribe({
      next: graph => {
        this.graph.set(graph);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('读取思政图谱失败');
      },
    });
  }

  toggleSelect(id: string): void {
    this.selectedId.set(this.selectedId() === id ? null : id);
  }

  nodeClass(node: PositionedNode): Record<string, boolean> {
    const selected = this.selectedId();
    const related = this.isRelated(node.id);
    return {
      'graph-node': true,
      'graph-node--selected': selected === node.id,
      'graph-node--related': !!selected && selected !== node.id && related,
      'graph-node--draft': node.column === 'case' && node.status === IdeologyCaseStatus.Draft,
      'graph-node--gap': node.column === 'chapter' && this.isGap(node.id),
    };
  }

  isGap(chapterId: string): boolean {
    return (this.graph()?.gaps ?? []).some(g => g.id === chapterId);
  }

  private isRelated(id: string): boolean {
    const selected = this.selectedId();
    if (!selected) return false;
    return this.canvas().edges.some(
      e => (e.from === selected && e.to === id) || (e.to === selected && e.from === id),
    );
  }

  shortTitle(title: string): string {
    return title.length > 16 ? title.slice(0, 16) + '…' : title;
  }

  goCases(): void {
    this.router.navigate(['/learning/ideology-cases'], { queryParams: { courseId: this.courseId() } });
  }

  goCoverage(): void {
    this.router.navigate(['/learning/ideology-coverage'], { queryParams: { courseId: this.courseId() } });
  }

  readonly statusText = (status?: IdeologyCaseStatus | null): string => {
    if (status === IdeologyCaseStatus.Published) return '已发布';
    if (status === IdeologyCaseStatus.Draft) return '草稿';
    return '';
  };
}
