import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import type { CourseDetailDto, ChapterDto } from '../../proxy/courses/dtos/models';
import { courseStatusColor, courseStatusLabel } from '../../shared/course-status';

@Component({
  selector: 'app-course-detail',
  standalone: true,
  imports: [
    CommonModule,
    NzCardModule,
    NzButtonModule,
    NzSpinModule,
    NzTagModule,
    NzIconModule,
    NzProgressModule,
    NzTooltipModule,
    NzEmptyModule,
  ],
  templateUrl: './course-detail.component.html',
  styleUrls: ['./course-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CourseDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly courseService = inject(CourseService);
  private readonly chapterService = inject(ChapterService);

  course = signal<CourseDetailDto | null>(null);
  loading = signal(true);
  /** 课程加载失败时的提示（401/403 由全局拦截器弹重新登录，这里只处理 404/网络等）。 */
  loadError = signal<string | null>(null);
  courseId = signal<string>('');
  isStudentView = signal(false);
  /** 从微专业课程列表进入时的微专业 id；存在时返回按钮先回微专业，再由微专业回列表 */
  backToMicroMajor = signal<string | null>(null);
  chapters = signal<ChapterDto[]>([]);
  chaptersLoading = signal(true);
  expandedNodes = signal<Set<string>>(new Set());

  ngOnInit() {
    this.isStudentView.set(this.router.url.startsWith('/student'));
    this.backToMicroMajor.set(this.route.snapshot.queryParamMap.get('fromMicroMajor'));
    const courseId = this.route.snapshot.paramMap.get('id');
    if (courseId) {
      this.courseId.set(courseId);
      this.loadCourse(courseId);
      this.loadChapters(courseId);
    }
  }

  loadChapters(courseId: string) {
    this.chaptersLoading.set(true);
    this.chapterService.getChapterTree(courseId).subscribe({
      next: (data) => {
        this.chapters.set(data);
        this.chaptersLoading.set(false);
      },
      error: () => {
        this.chaptersLoading.set(false);
      }
    });
  }

  isExpanded(nodeId: string): boolean {
    return this.expandedNodes().has(nodeId);
  }

  toggleNode(nodeId: string) {
    this.expandedNodes.update(set => {
      const newSet = new Set(set);
      if (newSet.has(nodeId)) {
        newSet.delete(nodeId);
      } else {
        newSet.add(nodeId);
      }
      return newSet;
    });
  }

  loadCourse(id: string) {
    this.loading.set(true);
    this.loadError.set(null);
    this.courseService.getDetail(id).subscribe({
      next: (result) => {
        this.course.set(result);
        this.loading.set(false);
        if (!result) {
          this.loadError.set('课程不存在或已被删除');
        }
      },
      error: (err: any) => {
        this.loading.set(false);
        const status = err?.status;
        // 401/403 交给全局处理（登录过期弹窗 / 权限页），本地不再重复提示；
        // 这里只处理 404（课程不存在）与网络等异常。
        if (status === 401 || status === 403) return;
        this.loadError.set(
          status === 404
            ? '课程不存在或已被删除'
            : status === 0
              ? '无法连接服务器，服务可能正在重启，请稍后重试'
              : '课程加载失败，请稍后重试',
        );
      }
    });
  }

  /** 失败态重试入口（模板按钮调用）。 */
  retryLoad() {
    const id = this.courseId();
    if (id) {
      this.loadCourse(id);
      this.loadChapters(id);
    }
  }
  
  getDifficultyColor(difficulty: number): string {
    const colors = ['green', 'lime', 'gold', 'orange', 'red'];
    return colors[difficulty - 1] || 'default';
  }
  
  getDifficultyLabel(difficulty: number): string {
    const labels = ['入门', '初级', '中级', '高级', '专家'];
    return labels[difficulty - 1] || '未知';
  }

  /** 课程发布状态文案（草稿 / 已发布 …） */
  courseStatusLabel(status?: number | null): string {
    return courseStatusLabel(status);
  }

  /** 课程发布状态标签配色 */
  courseStatusColor(status?: number | null): string {
    return courseStatusColor(status);
  }

  /** 兼属专业名（跳过主专业，模板内不支持复杂表达式故放 TS） */
  getExtraMajorNames(): string[] {
    const names = (this.course() as any)?.majorNames as string[] | undefined;
    return names?.length ? names.slice(1) : [];
  }
  
  goBack() {
    const mmId = this.backToMicroMajor();
    if (mmId) {
      this.router.navigate(['/micro-majors', mmId]);
      return;
    }
    this.router.navigate([this.isStudentView() ? '/student/courses' : '/']);
  }
  
  startLearning() {
    const course = this.course();
    if (course) {
      this.router.navigate([
        this.isStudentView() ? '/student/exercise-learning' : '/learning/exercise-learning',
        course.id,
      ]);
    }
  }
  
  viewKnowledgeGraph() {
    const course = this.course();
    if (course) {
      const basePath = this.isStudentView() ? '/student/knowledge-graph' : '/learning/knowledge-graph';
      this.router.navigate([basePath, course.id]);
    }
  }

  viewExercises() {
    if (this.isStudentView()) {
      return;
    }

    const course = this.course();
    if (course) {
      this.router.navigate(['/learning/exercise', course.id]);
    }
  }

  startExerciseLearning() {
    const course = this.course();
    if (course) {
      this.router.navigate([
        this.isStudentView() ? '/student/exercise-learning' : '/learning/exercise-learning',
        course.id,
      ]);
    }
  }
  
  enrollCourse() {
    console.log('Enrolling...');
  }
}
