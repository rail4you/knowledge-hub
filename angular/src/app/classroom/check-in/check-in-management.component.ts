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
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CheckInService } from '../../proxy/classroom/check-in.service';
import type {
  CheckInSessionDto,
  CreateCheckInSessionInput,
  CheckInRosterDto,
} from '../../proxy/classroom/dtos/models';
import { CheckInSessionStatus } from '../../proxy/classroom/enums/check-in-session-status.enum';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { MyCoursesService } from '../../learning/shared/my-courses.service';
import { CourseContextService } from '../../learning/course-context.service';
import {
  CHECK_IN_STATUS_LABEL,
  DEFAULT_CHECK_IN_MINUTES,
  SHORT_URL_PREFIX,
  STATUS_TAG_COLOR,
  buildShortUrl,
} from '../classroom-labels';

/**
 * 签到管理：教师发起场次 → 投屏/复制短链 → 学生扫码签到 → 实时签到率 + 未签到名单。
 *
 * 关于二维码：项目没有引入二维码库，所以这里给「大号短链 + 一键复制 + 投屏模式」；
 * 需要二维码时可加 `qrcode` 依赖后在此渲染（短链已经是最终形态，不涉及后端改动）。
 */
@Component({
  selector: 'app-check-in-management',
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
    NzInputNumberModule,
    NzSwitchModule,
    NzDrawerModule,
  ],
  templateUrl: './check-in-management.component.html',
  styleUrls: ['./check-in-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CheckInManagementComponent implements OnInit {
  private readonly myCourses = inject(MyCoursesService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(CheckInService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly sessions = signal<CheckInSessionDto[]>([]);
  readonly loading = signal(false);

  readonly isCreateOpen = signal(false);
  readonly saving = signal(false);
  readonly form = signal<CreateCheckInSessionInput>(this.emptyForm());

  /** 投屏/展示码面板 */
  readonly displaySession = signal<CheckInSessionDto | null>(null);
  /** 名单抽屉 */
  readonly roster = signal<CheckInRosterDto | null>(null);
  readonly rosterSession = signal<CheckInSessionDto | null>(null);
  readonly rosterLoading = signal(false);

  readonly totalCheckedIn = computed(() => this.sessions().reduce((sum, x) => sum + (x.checkedInCount ?? 0), 0));

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
      this.sessions.set([]);
      return;
    }
    this.courseContext.setCourse(courseId);
    this.loadSessions();
  }

  loadSessions(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    this.loading.set(true);
    this.service.getList(courseId, 0, 100).subscribe({
      next: res => {
        this.sessions.set(res.items ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.sessions.set([]);
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

  patchForm(patch: Partial<CreateCheckInSessionInput>): void {
    this.form.set({ ...this.form(), ...patch });
  }

  /** 模板里不能写 Number(...)（Angular 模板没有全局 Number），统一走这里。 */
  toNumber(raw: unknown): number | null {
    const value = typeof raw === 'string' ? Number(raw) : (raw as number);
    return Number.isFinite(value) ? value : null;
  }

  save(): void {
    const model = this.form();
    if (!model.title?.trim()) {
      this.message.warning('请填写签到标题');
      return;
    }
    this.saving.set(true);
    this.service.create(model).subscribe({
      next: session => {
        this.saving.set(false);
        this.isCreateOpen.set(false);
        this.message.success('已发起签到');
        this.loadSessions();
        this.displaySession.set(session);
      },
      error: err => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '发起失败');
      },
    });
  }

  closeSession(session: CheckInSessionDto): void {
    this.modal.confirm({
      nzTitle: '结束签到',
      nzContent: `结束后学生不能再签到：「${session.title}」`,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.service.close(session.id!).subscribe({
            next: () => {
              this.message.success('已结束');
              this.loadSessions();
              resolve();
            },
            error: err => {
              this.message.error(err?.error?.error?.message || '操作失败');
              reject(err);
            },
          });
        }),
    });
  }

  removeSession(session: CheckInSessionDto): void {
    this.modal.confirm({
      nzTitle: '删除签到',
      nzContent: `删除后签到记录一并清除，确定删除「${session.title}」吗？`,
      nzOkDanger: true,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.service.delete(session.id!).subscribe({
            next: () => {
              this.message.success('已删除');
              this.loadSessions();
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

  openRoster(session: CheckInSessionDto): void {
    this.rosterSession.set(session);
    this.roster.set(null);
    this.rosterLoading.set(true);
    this.service.getRoster(session.id!).subscribe({
      next: data => {
        this.roster.set(data);
        this.rosterLoading.set(false);
      },
      error: () => {
        this.rosterLoading.set(false);
      },
    });
  }

  shortUrl(session: CheckInSessionDto): string {
    return buildShortUrl(SHORT_URL_PREFIX.checkIn, session.token ?? '');
  }

  copyLink(session: CheckInSessionDto): void {
    const url = this.shortUrl(session);
    navigator.clipboard?.writeText(url).then(
      () => this.message.success('链接已复制'),
      () => this.message.warning('复制失败，请手动选择文本'),
    );
  }

  statusLabel(session: CheckInSessionDto): string {
    return CHECK_IN_STATUS_LABEL[(session.status ?? 0) as CheckInSessionStatus] ?? '—';
  }

  statusColor(session: CheckInSessionDto): string {
    return STATUS_TAG_COLOR[this.statusLabel(session)] ?? 'default';
  }

  rate(session: CheckInSessionDto): number {
    const expected = session.expectedCount ?? 0;
    if (!expected) return 0;
    return Math.round(((session.checkedInCount ?? 0) / expected) * 100);
  }

  /** 导出未签到名单（CSV，带 BOM 避免 Excel 乱码）。 */
  exportAbsent(): void {
    const data = this.roster();
    const session = this.rosterSession();
    if (!data || !session) return;

    const rows = [['学号/用户名', '姓名']];
    for (const s of data.absent ?? []) {
      rows.push([s.studentUserName ?? '', s.studentName ?? '']);
    }
    const csv = '\uFEFF' + rows.map(r => r.map(v => `"${(v ?? '').replace(/"/g, '""')}"`).join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `未签到名单-${session.title}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  private emptyForm(): CreateCheckInSessionInput {
    return {
      courseId: '',
      title: '',
      description: '',
      durationMinutes: DEFAULT_CHECK_IN_MINUTES,
      requireLocation: false,
      latitude: null,
      longitude: null,
      radiusMeters: null,
    };
  }

  get isCheckInOpen(): (s: CheckInSessionDto) => boolean {
    return (s: CheckInSessionDto) => (s.status ?? 0) === CheckInSessionStatus.Active;
  }
}
