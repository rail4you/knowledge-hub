import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { take } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzMessageService } from 'ng-zorro-antd/message';
import { DiscussionService } from '../../proxy/classroom/discussion.service';
import type {
  DiscussionDto,
  DiscussionReplyDto,
  CreateDiscussionInput,
  CreateDiscussionReplyInput,
  GetDiscussionListInput,
} from '../../proxy/classroom/dtos/models';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { MyCoursesService } from '../../learning/shared/my-courses.service';
import { CourseContextService } from '../../learning/course-context.service';
import { LearningService } from '../../proxy/learning/learning.service';

/**
 * 课程讨论答疑。
 *
 * 同一个组件被两处路由复用：
 * - 教师端 `/classroom/discussions`（route data.studentMode = false）：课程下拉 = 我教的课，可置顶/标记解决；
 * - 学生端 `/student/classroom`（studentMode = true）：课程下拉 = 我选的课，发帖后会出现在列表里。
 *
 * 权限由后端把关（发帖 `Classroom.Participate`，管理动作 `Classroom.Discussion`），前端只控制按钮显隐。
 */
@Component({
  selector: 'app-discussion-board',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzIconModule,
    NzTableModule,
    NzTagModule,
    NzTooltipModule,
    NzModalModule,
    NzFormModule,
    NzDrawerModule,
    NzCheckboxModule,
    NzPaginationModule,
  ],
  templateUrl: './discussion-board.component.html',
  styleUrls: ['./discussion-board.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DiscussionBoardComponent implements OnInit {
  private readonly myCourses = inject(MyCoursesService);
  private readonly learningService = inject(LearningService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(DiscussionService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly studentMode = signal(false);
  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly items = signal<DiscussionDto[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);

  readonly onlyUnresolved = signal(false);
  readonly onlyMine = signal(false);
  readonly keyword = signal('');
  readonly pageIndex = signal(1);
  readonly pageSize = 20;

  readonly isCreateOpen = signal(false);
  readonly form = signal<CreateDiscussionInput>({ courseId: '', title: '', content: '' });

  readonly detail = signal<DiscussionDto | null>(null);
  readonly detailLoading = signal(false);
  readonly replyContent = signal('');
  readonly replyTo = signal<DiscussionReplyDto | null>(null);

  readonly unresolvedCount = computed(() => this.items().filter(x => !x.isResolved).length);

  ngOnInit(): void {
    this.studentMode.set(this.route.snapshot.data['studentMode'] === true);
    this.loadCourses();
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      const courseId = params.get('courseId') || this.courseContext.currentCourseId();
      if (courseId) this.onCourseSelected(courseId);
    });
  }

  loadCourses(): void {
    if (this.studentMode()) {
      this.learningService.getMyCourses().subscribe({
        next: list =>
          this.courses.set(
            (list ?? []).map(x => ({ id: x.courseId, title: x.courseTitle }) as CourseDto),
          ),
        error: () => this.courses.set([]),
      });
      return;
    }

    this.myCourses.getCourses(300).subscribe({
      next: list => this.courses.set(list as any),
      error: () => this.courses.set([]),
    });
  }

  onCourseSelected(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    this.pageIndex.set(1);
    if (!courseId) {
      this.items.set([]);
      return;
    }
    if (!this.studentMode()) this.courseContext.setCourse(courseId);
    this.loadList();
  }

  loadList(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    const input: GetDiscussionListInput = {
      courseId,
      onlyUnresolved: this.onlyUnresolved(),
      onlyMine: this.onlyMine(),
      filter: this.keyword()?.trim() || null,
      skipCount: (this.pageIndex() - 1) * this.pageSize,
      maxResultCount: this.pageSize,
    };

    this.service.getList(input).subscribe({
      next: res => {
        this.items.set(res.items ?? []);
        this.total.set(res.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => {
        this.items.set([]);
        this.loading.set(false);
      },
    });
  }

  openCreate(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    this.form.set({ courseId, title: '', content: '' });
    this.isCreateOpen.set(true);
  }

  patchForm(patch: Partial<CreateDiscussionInput>): void {
    this.form.set({ ...this.form(), ...patch });
  }

  submitCreate(): void {
    const model = this.form();
    if (!model.title?.trim() || !model.content?.trim()) {
      this.message.warning('请填写标题与问题描述');
      return;
    }
    this.service.create(model).subscribe({
      next: () => {
        this.isCreateOpen.set(false);
        this.message.success('已发布问题');
        this.loadList();
      },
      error: err => this.message.error(err?.error?.error?.message || '发布失败'),
    });
  }

  openDetail(row: DiscussionDto): void {
    this.detail.set(null);
    this.detailLoading.set(true);
    this.replyContent.set('');
    this.replyTo.set(null);
    this.service.get(row.id!).subscribe({
      next: data => {
        this.detail.set(data);
        this.detailLoading.set(false);
        // 本地同步浏览数/回复数，避免整页刷新
        this.items.set(this.items().map(x => (x.id === data.id ? { ...x, viewCount: data.viewCount } : x)));
      },
      error: () => this.detailLoading.set(false),
    });
  }

  sendReply(): void {
    const text = this.replyContent()?.trim();
    const detail = this.detail();
    if (!text || !detail) {
      this.message.warning('请输入回复内容');
      return;
    }

    const input: CreateDiscussionReplyInput = {
      discussionId: detail.id!,
      parentReplyId: this.replyTo()?.id ?? null,
      content: text,
    };

    this.service.reply(input).subscribe({
      next: () => {
        this.replyContent.set('');
        this.replyTo.set(null);
        this.openDetail(detail);
        this.loadList();
      },
      error: err => this.message.error(err?.error?.error?.message || '回复失败'),
    });
  }

  replyToSomeone(reply: DiscussionReplyDto): void {
    this.replyTo.set(reply);
  }

  cancelReplyTo(): void {
    this.replyTo.set(null);
  }

  acceptReply(reply: DiscussionReplyDto): void {
    const detail = this.detail();
    if (!detail) return;
    this.service.acceptReply(detail.id!, reply.id!).subscribe({
      next: () => {
        this.message.success('已采纳为最佳答案');
        this.openDetail(detail);
        this.loadList();
      },
      error: err => this.message.error(err?.error?.error?.message || '操作失败'),
    });
  }

  toggleResolved(row: DiscussionDto): void {
    this.service.setResolved(row.id!, !row.isResolved).subscribe({
      next: () => {
        this.message.success(row.isResolved ? '已标记为未解决' : '已标记为已解决');
        this.loadList();
        if (this.detail()?.id === row.id) this.openDetail({ ...row, isResolved: !row.isResolved });
      },
      error: err => this.message.error(err?.error?.error?.message || '操作失败'),
    });
  }

  togglePinned(row: DiscussionDto): void {
    this.service.setPinned(row.id!, !row.isPinned).subscribe({
      next: () => {
        this.message.success(row.isPinned ? '已取消置顶' : '已置顶');
        this.loadList();
      },
      error: err => this.message.error(err?.error?.error?.message || '操作失败'),
    });
  }

  remove(row: DiscussionDto): void {
    this.modal.confirm({
      nzTitle: '删除讨论',
      nzContent: `确定删除「${row.title}」及其全部回复吗？`,
      nzOkDanger: true,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.service.delete(row.id!).subscribe({
            next: () => {
              this.message.success('已删除');
              this.loadList();
              resolve();
            },
            error: err => {
              this.message.error(err?.error?.error?.message || '删除失败');
              reject(err);
            },
          });
        }),
    });
  }

  onPageChange(page: number): void {
    this.pageIndex.set(page);
    this.loadList();
  }

  canManage(row: DiscussionDto): boolean {
    return !this.studentMode() || row.isMine === true;
  }

  /** 顶层回复（parentReplyId 为空）；子回复挂在父回复下面。 */
  topReplies(detail: DiscussionDto): DiscussionReplyDto[] {
    return (detail.replies ?? []).filter(x => !x.parentReplyId);
  }

  childReplies(detail: DiscussionDto, parentId?: string): DiscussionReplyDto[] {
    return (detail.replies ?? []).filter(x => x.parentReplyId && x.parentReplyId === parentId);
  }
}
