import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzMessageService } from 'ng-zorro-antd/message';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import {
  PracticumEnrollmentDto,
  PracticumGuidanceRecordDto,
  PracticumTimelineItemDto,
} from '../../proxy/practicums/dtos/models';
import { PracticumEnrollmentStatus } from '../../proxy/practicums/enums/practicum-enrollment-status.enum';
import { ClientCacheService } from '../../shared/cache/client-cache.service';

@Component({
  selector: 'app-student-my-practicums',
  standalone: true,
  imports: [
    CommonModule, DatePipe, RouterModule,
    NzIconModule, NzSpinModule, NzEmptyModule, NzModalModule,
  ],
  templateUrl: './student-my-practicums.component.html',
  styleUrls: ['./student-my-practicums.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyPracticumsComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly cache = inject(ClientCacheService);

  readonly items = signal<PracticumEnrollmentDto[]>([]);
  readonly loading = signal(true);

  readonly guidanceVisible = signal(false);
  readonly guidanceLoading = signal(false);
  readonly guidanceItems = signal<PracticumGuidanceRecordDto[]>([]);
  guidanceTitle = '';

  readonly timelineVisible = signal(false);
  readonly timelineLoading = signal(false);
  readonly timelineItems = signal<PracticumTimelineItemDto[]>([]);
  timelineTitle = '';

  // ─── 统计汇总 ───────────────────────────
  readonly summary = signal({
    total: 0, enrolled: 0, inProgress: 0, submitted: 0, reviewed: 0, completed: 0, cancelled: 0,
  });

  /** 数据总览（对齐原型 .grid.four .metric） */
  readonly metrics = computed(() => {
    const s = this.summary();
    const scored = this.items().filter(i => i.finalScore != null);
    const avgScore = scored.length
      ? Math.round(scored.reduce((a, b) => a + (b.finalScore ?? 0), 0) / scored.length)
      : 0;
    return [
      { label: '参与实训', value: s.total, icon: 'experiment', hint: '累计报名项目' },
      { label: '进行中', value: s.inProgress + s.submitted + s.reviewed, icon: 'play-circle', hint: '含待评阅与已评阅' },
      { label: '已完成', value: s.completed, icon: 'check-circle', hint: '可查看最终成绩' },
      { label: '平均成绩', value: avgScore, icon: 'trophy', hint: scored.length ? `基于 ${scored.length} 个已评分项目` : '暂无已评分项目' },
    ];
  });

  /** 状态分布统计（原型 list-row + progress 风格） */
  readonly statusStats = computed(() => {
    const items = this.items();
    const total = items.length || 1;
    const defs: { status: PracticumEnrollmentStatus; label: string; cls: string }[] = [
      { status: PracticumEnrollmentStatus.InProgress, label: '进行中', cls: 'dot--primary' },
      { status: PracticumEnrollmentStatus.Submitted, label: '待评阅', cls: 'dot--gold' },
      { status: PracticumEnrollmentStatus.Reviewed, label: '已评阅', cls: 'dot--teal' },
      { status: PracticumEnrollmentStatus.Completed, label: '已完成', cls: 'dot--green' },
      { status: PracticumEnrollmentStatus.Enrolled, label: '已参与', cls: 'dot--gray' },
      { status: PracticumEnrollmentStatus.Cancelled, label: '已取消', cls: 'dot--mute' },
    ];
    return defs
      .map(d => {
        const count = items.filter(i => i.status === d.status).length;
        return { ...d, count, percent: Math.round((count / total) * 100) };
      })
      .filter(d => d.count > 0);
  });

  /** 各项目成绩（原型 list-row + progress 风格，未评分显示缺口） */
  readonly scoreStats = computed(() => {
    return [...this.items()]
      .sort((a, b) => new Date(a.enrolledAt || 0).getTime() - new Date(b.enrolledAt || 0).getTime())
      .map(i => ({
        id: i.id,
        title: i.projectTitle || '未命名项目',
        score: i.finalScore ?? null,
      }));
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.cache.load<PracticumEnrollmentDto[]>('student.practicums', 'my-enrollments', () => this.practicumService.getMyEnrollments()).subscribe({
      next: items => {
        this.items.set(items || []);
        this.loading.set(false);
        this.computeSummary();
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载我的实训失败');
      },
    });
  }

  private computeSummary(): void {
    const items = this.items();
    const s = { total: items.length, enrolled: 0, inProgress: 0, submitted: 0, reviewed: 0, completed: 0, cancelled: 0 };
    for (const it of items) {
      switch (it.status) {
        case PracticumEnrollmentStatus.Enrolled: s.enrolled++; break;
        case PracticumEnrollmentStatus.InProgress: s.inProgress++; break;
        case PracticumEnrollmentStatus.Submitted: s.submitted++; break;
        case PracticumEnrollmentStatus.Reviewed: s.reviewed++; break;
        case PracticumEnrollmentStatus.Completed: s.completed++; break;
        case PracticumEnrollmentStatus.Cancelled: s.cancelled++; break;
      }
    }
    this.summary.set(s);
  }

  openDetail(item: PracticumEnrollmentDto): void {
    this.router.navigate(['/student/practicums', item.projectId]);
  }

  openGuidance(item: PracticumEnrollmentDto): void {
    this.guidanceTitle = `${item.projectTitle || '实训项目'} · 指导记录`;
    this.guidanceVisible.set(true);
    this.guidanceLoading.set(true);
    this.guidanceItems.set([]);
    this.cache.load<PracticumGuidanceRecordDto[]>('student.practicums', `guidance:${item.id}`, () => this.practicumService.getGuidanceList(item.id)).subscribe({
      next: list => {
        this.guidanceItems.set(list || []);
        this.guidanceLoading.set(false);
      },
      error: () => {
        this.guidanceLoading.set(false);
        this.message.error('加载指导记录失败');
      },
    });
  }

  openTimeline(item: PracticumEnrollmentDto): void {
    this.timelineTitle = `${item.projectTitle || '实训项目'} · 过程追溯`;
    this.timelineVisible.set(true);
    this.timelineLoading.set(true);
    this.timelineItems.set([]);
    this.cache.load<PracticumTimelineItemDto[]>('student.practicums', `timeline:${item.id}`, () => this.practicumService.getTimeline(item.id)).subscribe({
      next: list => {
        this.timelineItems.set(list || []);
        this.timelineLoading.set(false);
      },
      error: () => {
        this.timelineLoading.set(false);
        this.message.error('加载过程记录失败');
      },
    });
  }

  /** 状态 tag：对齐原型 .tag（gold 异常 / green 完成 / primary 进行中 / gray 取消） */
  statusTag(status?: PracticumEnrollmentStatus): { text: string; cls: string } {
    switch (status) {
      case PracticumEnrollmentStatus.InProgress: return { text: '进行中', cls: 'tag tag--primary' };
      case PracticumEnrollmentStatus.Submitted: return { text: '待评阅', cls: 'tag tag--gold' };
      case PracticumEnrollmentStatus.Reviewed: return { text: '已评阅', cls: 'tag' };
      case PracticumEnrollmentStatus.Completed: return { text: '已完成', cls: 'tag tag--green' };
      case PracticumEnrollmentStatus.Cancelled: return { text: '已取消', cls: 'tag tag--gray' };
      case PracticumEnrollmentStatus.Enrolled: return { text: '已参与', cls: 'tag tag--gray' };
      default: return { text: '未知', cls: 'tag tag--gray' };
    }
  }

  /** 卡片封面色块（c0–c3 轮换，对齐原型 .cover） */
  coverClass(idx: number): number {
    return idx % 4;
  }

  /** 进度（原型 .progress 进度条） */
  progressWidth(item: PracticumEnrollmentDto): number {
    if (item.status === PracticumEnrollmentStatus.Completed) return 100;
    return Math.max(0, Math.min(100, item.progress ?? 0));
  }

  hasScore(item: PracticumEnrollmentDto): boolean {
    return item.finalScore != null;
  }

  hasMetadata(item: PracticumTimelineItemDto, key: string): boolean {
    return item.metadata?.[key] != null;
  }

  timelineTypeTag(type?: string): { text: string; cls: string } {
    switch (type) {
      case 'Submission': return { text: '提交成果', cls: 'tag tag--primary' };
      case 'Guidance': return { text: '教师指导', cls: 'tag tag--green' };
      case 'Assessment': return { text: '教师评分', cls: 'tag tag--gold' };
      case 'Enrollment': return { text: '参与实训', cls: 'tag tag--gray' };
      default: return { text: type || '', cls: 'tag tag--gray' };
    }
  }
}
