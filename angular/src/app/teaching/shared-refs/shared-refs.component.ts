import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { CourseSharedRefService } from '../../proxy/resource-libraries/course-shared-ref.service';
import { TeachingResourceLibraryService } from '../../proxy/resource-libraries/teaching-resource-library.service';
import { CourseService } from '../../proxy/courses/course.service';
import type {
  CourseSharedRefDto,
  SharedRefStatisticsDto,
  TeachingLibraryDto,
  TeachingLibraryContentDto,
} from '../../proxy/resource-libraries/dtos/models';

type Direction = 'all' | 'incoming' | 'outgoing';

/**
 * 我的跨校引用（V2.0 P4）。
 *
 * <p>两个视角同屏：**我引用的**（本校课程用了别校资源）与**被引用的**（我的资源被别校用了）。
 * 页面不展示对方学生明细——来源校只能看到"哪所学校、哪门课程、用了几条"。</p>
 */
@Component({
  selector: 'app-shared-refs',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzEmptyModule,
    NzModalModule,
    NzSelectModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './shared-refs.component.html',
  styleUrls: ['./shared-refs.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SharedRefsComponent implements OnInit {
  private readonly refService = inject(CourseSharedRefService);
  private readonly libraryService = inject(TeachingResourceLibraryService);
  private readonly courseService = inject(CourseService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly loading = signal(false);
  readonly refs = signal<CourseSharedRefDto[]>([]);
  readonly total = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  readonly direction = signal<Direction>('all');
  readonly stats = signal<SharedRefStatisticsDto | null>(null);

  readonly statsCards = computed(() => {
    const s = this.stats();
    return [
      { label: '被引学校', value: s?.referencedSchoolCount ?? 0 },
      { label: '被引课程', value: s?.referencedCourseCount ?? 0 },
      { label: '生效引用', value: s?.activeRefCount ?? 0 },
      { label: '已撤销', value: s?.revokedRefCount ?? 0 },
      { label: '学习人数', value: s?.learnerCount ?? 0 },
    ];
  });

  // 新建引用
  readonly createOpen = signal(false);
  readonly creating = signal(false);
  readonly libraries = signal<TeachingLibraryDto[]>([]);
  readonly contents = signal<TeachingLibraryContentDto[]>([]);
  readonly courses = signal<Array<{ id: string; title: string }>>([]);
  readonly selectedLibraryId = signal<string | null>(null);
  readonly selectedContentId = signal<string | null>(null);
  readonly selectedCourseId = signal<string | null>(null);

  ngOnInit(): void {
    this.reload();
    this.loadStats();
  }

  reload(): void {
    this.loading.set(true);
    this.refService
      .getList({
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
        direction: this.direction(),
      } as any)
      .subscribe({
        next: res => {
          this.refs.set(res.items ?? []);
          this.total.set(res.totalCount ?? 0);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载引用列表失败');
        },
      });
  }

  loadStats(): void {
    this.refService.getStatistics().subscribe({
      next: s => this.stats.set(s),
      error: () => this.stats.set(null),
    });
  }

  setDirection(dir: Direction): void {
    this.direction.set(dir);
    this.pageIndex.set(1);
    this.reload();
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
    this.reload();
  }

  // ── 新建引用 ──

  openCreate(): void {
    this.selectedLibraryId.set(null);
    this.selectedContentId.set(null);
    this.selectedCourseId.set(null);
    this.contents.set([]);
    this.loadLibraries();
    this.loadCourses();
    this.createOpen.set(true);
  }

  private loadLibraries(): void {
    this.libraryService.getMyLibraries({} as any).subscribe({
      next: list => this.libraries.set(list ?? []),
      error: () => this.libraries.set([]),
    });
  }

  private loadCourses(): void {
    this.courseService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe({
      next: res =>
        this.courses.set(
          (res.items ?? []).map(c => ({ id: c.id!, title: c.title ?? '(未命名课程)' })),
        ),
      error: () => this.courses.set([]),
    });
  }

  onLibraryChange(libraryId: string | null): void {
    this.selectedLibraryId.set(libraryId);
    this.selectedContentId.set(null);
    this.contents.set([]);
    if (!libraryId) return;

    this.libraryService.getContents(libraryId, { skipCount: 0, maxResultCount: 200 } as any).subscribe({
      next: res =>
        this.contents.set(
          // 只列已发布到库的内容（未发布/待审的不能引用）
          (res.items ?? []).filter(c => (c.publicationStatus ?? 2) === 2),
        ),
      error: () => this.contents.set([]),
    });
  }

  submitCreate(): void {
    if (!this.selectedLibraryId() || !this.selectedContentId() || !this.selectedCourseId()) {
      this.message.warning('请选择资源库、资源与目标课程');
      return;
    }

    this.creating.set(true);
    this.refService
      .create({
        libraryId: this.selectedLibraryId()!,
        contentId: this.selectedContentId()!,
        courseId: this.selectedCourseId()!,
      } as any)
      .subscribe({
        next: () => {
          this.creating.set(false);
          this.createOpen.set(false);
          this.message.success('已引用到课程：本课程学生现在可以学习了');
          this.reload();
          this.loadStats();
        },
        error: err => {
          this.creating.set(false);
          this.message.error(err?.error?.error?.message ?? '引用失败');
        },
      });
  }

  revoke(ref: CourseSharedRefDto): void {
    this.modal.confirm({
      nzTitle: '撤销引用',
      nzContent: `撤销后「${ref.contentName}」在课程「${ref.courseTitle}」中立即不可访问，学生端会显示"资源暂不可用"。`,
      nzOkDanger: true,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.refService.revoke(ref.id!, '教师撤销').subscribe({
            next: () => {
              this.message.success('已撤销');
              this.reload();
              this.loadStats();
              resolve();
            },
            error: err => {
              this.message.error(err?.error?.error?.message ?? '撤销失败');
              reject(err);
            },
          });
        }),
    });
  }

  isActive(ref: CourseSharedRefDto): boolean {
    return (ref.status ?? 0) === 0;
  }

  trackById = (_: number, item: { id?: string }) => item.id;
}
