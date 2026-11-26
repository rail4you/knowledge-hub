import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { take } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzMessageService } from 'ng-zorro-antd/message';
import { StudentRecommendationService } from '../../proxy/learning/student-recommendation.service';
import type {
  CourseRecommendationOverviewDto,
  StudentRecommendationSummaryDto,
  StudentRecommendationDto,
} from '../../proxy/learning/dtos/models';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { MyCoursesService } from '../shared/my-courses.service';
import { CourseContextService } from '../course-context.service';

/**
 * 学习推荐工作台（教师端）。
 *
 * 推荐是**落库 + 有生命周期**的（待开始 → 已查看 → 进行中 → 已完成 / 已忽略），
 * 所以这里能看两件事：本课推荐分布（薄弱点/前置/未完成章节），以及**采纳率**（完成 / 全部）。
 * 「刷新整课推荐」会为每个学生按当前掌握度与章节进度重算（幂等：同一条只更新理由，不重复插）。
 */
@Component({
  selector: 'app-recommendation-workbench',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzIconModule,
    NzTableModule,
    NzTagModule,
    NzProgressModule,
    NzDrawerModule,
  ],
  templateUrl: './recommendation-workbench.component.html',
  styleUrls: ['./recommendation-workbench.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecommendationWorkbenchComponent implements OnInit {
  private readonly myCourses = inject(MyCoursesService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(StudentRecommendationService);
  private readonly message = inject(NzMessageService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly overview = signal<CourseRecommendationOverviewDto | null>(null);
  readonly loading = signal(false);
  readonly refreshing = signal(false);

  readonly detailOpen = signal(false);
  readonly detailStudent = signal<StudentRecommendationSummaryDto | null>(null);

  ngOnInit(): void {
    this.loadCourses();
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      const courseId = params.get('courseId') || this.courseContext.currentCourseId();
      if (courseId) this.onCourseSelected(courseId);
    });
  }

  loadCourses(): void {
    this.myCourses.getCourses(300).subscribe({
      next: list => this.courses.set(list as any),
      error: () => this.courses.set([]),
    });
  }

  onCourseSelected(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    if (!courseId) {
      this.overview.set(null);
      return;
    }
    this.courseContext.setCourse(courseId);
    this.load();
  }

  load(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    this.service.getCourseOverview(courseId).subscribe({
      next: data => {
        this.overview.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.overview.set(null);
        this.loading.set(false);
        this.message.error('推荐概览加载失败');
      },
    });
  }

  refreshCourse(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.refreshing.set(true);
    this.service.refreshCourse(courseId).subscribe({
      next: res => {
        this.refreshing.set(false);
        this.message.success(res.message ?? '已刷新');
        this.load();
      },
      error: err => {
        this.refreshing.set(false);
        this.message.error(err?.error?.error?.message || '刷新失败');
      },
    });
  }

  openStudent(row: StudentRecommendationSummaryDto): void {
    this.detailStudent.set(row);
    this.detailOpen.set(true);
  }

  percent(value?: number | null): number {
    return Math.round((value ?? 0) * 100);
  }

  statusColor(status?: number): string {
    switch (status) {
      case 1:
        return 'processing';
      case 2:
        return 'warning';
      case 3:
        return 'success';
      case 4:
        return 'default';
      default:
        return 'default';
    }
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
}
