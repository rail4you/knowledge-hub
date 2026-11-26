import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { StudentRecommendationService } from '../../proxy/learning/student-recommendation.service';
import type { StudentRecommendationDto } from '../../proxy/learning/dtos/models';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { LearningService } from '../../proxy/learning/learning.service';

/**
 * 学生端「学习推荐」。
 *
 * 每条推荐都是可解释的（"为什么推我"），并带一个动作：
 * - 薄弱知识点 / 前置知识 → 去专项练习（带知识点）
 * - 未完成章节 → 去章节学习
 * 学生可以标记「完成」或「忽略」，教师端据此统计采纳率。
 */
@Component({
  selector: 'app-student-recommendations',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzIconModule,
    NzTagModule,
  ],
  templateUrl: './student-recommendations.component.html',
  styleUrls: ['./student-recommendations.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentRecommendationsComponent implements OnInit {
  private readonly learningService = inject(LearningService);
  private readonly service = inject(StudentRecommendationService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly items = signal<StudentRecommendationDto[]>([]);
  readonly loading = signal(false);
  readonly refreshing = signal(false);

  ngOnInit(): void {
    this.loadCourses();
  }

  loadCourses(): void {
    this.learningService.getMyCourses().subscribe({
      next: list => {
        const mapped = (list ?? []).map(x => ({ id: x.courseId, title: x.courseTitle }) as CourseDto);
        this.courses.set(mapped);
        const courseId = mapped.length > 0 ? mapped[0].id ?? null : null;
        if (courseId) this.onCourseSelected(courseId);
      },
      error: () => this.courses.set([]),
    });
  }

  onCourseSelected(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    if (!courseId) {
      this.items.set([]);
      return;
    }
    this.load();
  }

  load(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    this.service.getMy(courseId, false).subscribe({
      next: rows => {
        this.items.set(rows ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.items.set([]);
        this.loading.set(false);
      },
    });
  }

  /** 没有推荐时用一次刷新兜底（学生首次进入体验） */
  refresh(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.refreshing.set(true);
    this.service.refreshMine(courseId).subscribe({
      next: res => {
        this.refreshing.set(false);
        this.message.success(res.message ?? '已更新推荐');
        this.load();
      },
      error: err => {
        this.refreshing.set(false);
        this.message.error(err?.error?.error?.message || '更新失败');
      },
    });
  }

  mark(item: StudentRecommendationDto, status: number): void {
    if (!item.id) return;
    this.service.mark(item.id, status).subscribe({
      next: () => {
        this.message.success(status === 3 ? '已标记完成' : '已忽略');
        this.load();
      },
      error: err => this.message.error(err?.error?.error?.message || '操作失败'),
    });
  }

  /** 按推荐类型跳转：未完成章节去学习页，其余去专项练习 */
  go(item: StudentRecommendationDto): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    if (item.actionKind === 'chapter') {
      const query = item.chapterId ? `?chapterId=${item.chapterId}` : '';
      this.router.navigateByUrl(`/student/courses/${courseId}/learn${query}`);
      return;
    }

    const params = new URLSearchParams({ courseId });
    if (item.knowledgeResourceId) params.set('knowledgeResourceId', item.knowledgeResourceId);
    if (item.knowledgeResourceName) params.set('kpName', item.knowledgeResourceName);
    this.router.navigateByUrl(`/student/practice?${params.toString()}`);
  }

  typeColor(type?: number): string {
    switch (type) {
      case 0:
        return 'error';
      case 1:
        return 'warning';
      case 2:
        return 'processing';
      default:
        return 'default';
    }
  }

  priorityLabel(priority?: number): string {
    const p = priority ?? 5;
    if (p >= 9) return '优先处理';
    if (p >= 7) return '建议尽快';
    return '有空再看';
  }
}
