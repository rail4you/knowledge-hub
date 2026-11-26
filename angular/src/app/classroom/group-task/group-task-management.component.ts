import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
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
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzMessageService } from 'ng-zorro-antd/message';
import { GroupTaskService } from '../../proxy/classroom/group-task.service';
import type {
  GroupTaskDto,
  GroupTaskDetailDto,
  CreateGroupTaskInput,
  StudentGroupDto,
  GroupTaskSubmissionDto,
} from '../../proxy/classroom/dtos/models';
import { GroupTaskStatus } from '../../proxy/classroom/enums/group-task-status.enum';
import { GroupMode } from '../../proxy/classroom/enums/group-mode.enum';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { MyCoursesService } from '../../learning/shared/my-courses.service';
import { CourseContextService } from '../../learning/course-context.service';
import {
  DEFAULT_GROUP_SIZE,
  GROUP_MODE_LABEL,
  GROUP_TASK_STATUS_LABEL,
  SHORT_URL_PREFIX,
  STATUS_TAG_COLOR,
  buildShortUrl,
} from '../classroom-labels';

/**
 * 小组任务管理：建任务 → 自动/手工分组 → 发布 → 各组提交 → 评分（组员共享）。
 * 一组一份提交由后端唯一索引保证，组内谁交都行。
 */
@Component({
  selector: 'app-group-task-management',
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
    NzDatePickerModule,
    NzInputNumberModule,
    NzDrawerModule,
  ],
  templateUrl: './group-task-management.component.html',
  styleUrls: ['./group-task-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GroupTaskManagementComponent implements OnInit {
  private readonly myCourses = inject(MyCoursesService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(GroupTaskService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly tasks = signal<GroupTaskDto[]>([]);
  readonly loading = signal(false);

  readonly isCreateOpen = signal(false);
  readonly saving = signal(false);
  readonly form = signal<CreateGroupTaskInput>(this.emptyForm());

  readonly detail = signal<GroupTaskDetailDto | null>(null);
  readonly detailLoading = signal(false);

  readonly gradingGroup = signal<StudentGroupDto | null>(null);
  readonly gradeScore = signal<number>(0);
  readonly gradeFeedback = signal('');

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
      this.tasks.set([]);
      return;
    }
    this.courseContext.setCourse(courseId);
    this.loadList();
  }

  loadList(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    this.loading.set(true);
    this.service.getList(courseId, 0, 100).subscribe({
      next: res => {
        this.tasks.set(res.items ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.tasks.set([]);
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
    this.form.set({ ...this.emptyForm(), courseId });
    this.isCreateOpen.set(true);
  }

  patchForm(patch: Partial<CreateGroupTaskInput>): void {
    this.form.set({ ...this.form(), ...patch });
  }

  save(): void {
    const model = this.form();
    if (!model.title?.trim()) {
      this.message.warning('请填写任务标题');
      return;
    }
    this.saving.set(true);
    this.service.create(model).subscribe({
      next: () => {
        this.saving.set(false);
        this.isCreateOpen.set(false);
        this.message.success('已创建任务');
        this.loadList();
      },
      error: err => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '创建失败');
      },
    });
  }

  openDetail(row: GroupTaskDto): void {
    this.detail.set(null);
    this.detailLoading.set(true);
    this.service.getDetail(row.id!).subscribe({
      next: data => {
        this.detail.set(data);
        this.detailLoading.set(false);
      },
      error: () => this.detailLoading.set(false),
    });
  }

  refreshDetail(): void {
    const current = this.detail();
    if (current) this.openDetail(current);
  }

  autoAssign(): void {
    const current = this.detail();
    if (!current) return;
    this.service.autoAssignGroups(current.id!, null).subscribe({
      next: data => {
        this.detail.set(data);
        this.message.success('已随机分组');
        this.loadList();
      },
      error: err => this.message.error(err?.error?.error?.message || '分组失败'),
    });
  }

  publish(): void {
    const current = this.detail();
    if (!current) return;
    this.service.publish(current.id!).subscribe({
      next: data => {
        this.detail.set(data);
        this.message.success('已发布');
        this.loadList();
      },
      error: err => this.message.error(err?.error?.error?.message || '发布失败'),
    });
  }

  closeTask(): void {
    const current = this.detail();
    if (!current) return;
    this.service.close(current.id!).subscribe({
      next: data => {
        this.detail.set(data);
        this.message.success('已结束');
        this.loadList();
      },
      error: err => this.message.error(err?.error?.error?.message || '操作失败'),
    });
  }

  removeTask(row: GroupTaskDto): void {
    this.modal.confirm({
      nzTitle: '删除小组任务',
      nzContent: `分组、提交与评分都会一起删除，确定删除「${row.title}」吗？`,
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

  openGrade(group: StudentGroupDto): void {
    const submission = group.submission;
    if (!submission) {
      this.message.warning('该组还没有提交');
      return;
    }
    this.gradingGroup.set(group);
    this.gradeScore.set(submission.score ?? 0);
    this.gradeFeedback.set(submission.feedback ?? '');
  }

  submitGrade(): void {
    const group = this.gradingGroup();
    const submission = group?.submission;
    if (!submission?.id) return;

    this.service.grade({ submissionId: submission.id, score: this.gradeScore(), feedback: this.gradeFeedback() }).subscribe({
      next: () => {
        this.gradingGroup.set(null);
        this.message.success('已评分');
        this.refreshDetail();
      },
      error: err => this.message.error(err?.error?.error?.message || '评分失败'),
    });
  }

  copyLink(task: GroupTaskDto | GroupTaskDetailDto): void {
    const url = buildShortUrl(SHORT_URL_PREFIX.groupTask, task.token ?? '');
    navigator.clipboard?.writeText(url).then(
      () => this.message.success('链接已复制'),
      () => this.message.warning('复制失败，请手动选择文本'),
    );
  }

  shortUrl(task: GroupTaskDto): string {
    return buildShortUrl(SHORT_URL_PREFIX.groupTask, task.token ?? '');
  }

  statusLabel(task: GroupTaskDto): string {
    // 已发布但已过截止时间 → 显示「已截止」，与短链页口径一致
    if ((task.status ?? 0) === GroupTaskStatus.Published && task.dueAt && new Date(task.dueAt) < new Date()) {
      return '已截止';
    }
    return GROUP_TASK_STATUS_LABEL[(task.status ?? 0) as GroupTaskStatus] ?? '—';
  }

  statusColor(task: GroupTaskDto): string {
    return STATUS_TAG_COLOR[this.statusLabel(task)] ?? 'default';
  }

  modeLabel(mode?: GroupMode | null): string {
    return GROUP_MODE_LABEL[(mode ?? 0) as GroupMode] ?? '—';
  }

  submissionOf(group: StudentGroupDto): GroupTaskSubmissionDto | undefined {
    return group.submission ?? undefined;
  }

  submittedCount(detail: GroupTaskDetailDto | null): number {
    return (detail?.groups ?? []).filter(g => !!g.submission).length;
  }

  unsubmittedGroups(detail: GroupTaskDetailDto | null): StudentGroupDto[] {
    return (detail?.groups ?? []).filter(g => !g.submission);
  }

  private emptyForm(): CreateGroupTaskInput {
    return {
      courseId: '',
      title: '',
      description: '',
      dueAt: null,
      maxScore: 100,
      groupMode: GroupMode.RandomAuto,
      groupCount: null,
      publishImmediately: true,
    };
  }

  get defaultGroupSize(): number {
    return DEFAULT_GROUP_SIZE;
  }
}
