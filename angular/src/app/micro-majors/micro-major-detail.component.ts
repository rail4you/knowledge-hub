import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MicroMajorDetailDto, MicroMajorService } from './micro-major.service';
import { CourseService } from '../proxy/courses/course.service';
import type { CourseDto } from '../proxy/courses/dtos/models';

@Component({
  selector: 'app-micro-major-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzIconModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './micro-major-detail.component.html',
  styleUrls: ['./micro-major-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MicroMajorDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly microMajorService = inject(MicroMajorService);
  private readonly courseService = inject(CourseService);

  readonly loading = signal(false);
  readonly coursesLoading = signal(false);
  readonly detail = signal<MicroMajorDetailDto | null>(null);
  /** 课程完整信息（与课程列表同源，含专业/学期/学分等） */
  readonly courseDetails = signal<CourseDto[]>([]);

  /** 课程 Id -> 是否核心课 */
  private readonly coreCourseIds = computed(
    () =>
      new Set(
        (this.detail()?.courses || [])
          .filter(c => c.isCore && c.courseId)
          .map(c => c.courseId),
      ),
  );

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }

    this.loadDetail(id);
  }

  loadDetail(id: string): void {
    this.loading.set(true);
    this.microMajorService.getDetail(id).subscribe({
      next: detail => {
        this.detail.set(detail);
        this.loading.set(false);
        const courseIds = (detail?.courses || [])
          .map(c => c.courseId)
          .filter((courseId): courseId is string => !!courseId);
        this.loadCourseDetails(courseIds);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  /** 并行拉取每门课程的完整信息，单个失败不阻断其他课程 */
  loadCourseDetails(courseIds: string[]): void {
    if (courseIds.length === 0) {
      this.courseDetails.set([]);
      return;
    }
    this.coursesLoading.set(true);
    forkJoin(
      courseIds.map(id =>
        this.courseService.get(id).pipe(catchError(() => of(null as CourseDto | null))),
      ),
    ).subscribe({
      next: results => {
        const courses = (results || []).filter((c): c is CourseDto => !!c);
        const order = new Map(courseIds.map((courseId, idx) => [courseId, idx]));
        courses.sort((a, b) => (order.get(a.id!) ?? 0) - (order.get(b.id!) ?? 0));
        this.courseDetails.set(courses);
        this.coursesLoading.set(false);
      },
      complete: () => this.coursesLoading.set(false),
    });
  }

  isCoreCourse(courseId: string): boolean {
    return this.coreCourseIds().has(courseId);
  }

  /** 课程专业展示：优先多专业全量，其次主专业，无归属则为公共课 */
  courseMajorNames(course: CourseDto): string[] {
    const names = (course.majorNames || []).filter(n => !!n);
    if (names.length > 0) {
      return names;
    }
    if (course.majorName) {
      return [course.majorName];
    }
    return [];
  }
}
