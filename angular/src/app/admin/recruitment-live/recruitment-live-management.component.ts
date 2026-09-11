import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { RecruitmentLiveService } from '../../recruitment-live/recruitment-live.service';
import { ParticipantBriefDto, RecruitmentLiveDto, RecruitmentLiveStatus, UserBriefDto } from '../../recruitment-live/recruitment-live.models';

@Component({
  selector: 'app-recruitment-live-management',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    ReactiveFormsModule,
    NzTableModule,
    NzButtonModule,
    NzTagModule,
    NzIconModule,
    NzPopconfirmModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzSelectModule,
    NzDatePickerModule,
    NzCheckboxModule,
    NzEmptyModule,
    NzSpinModule,
  ],
  templateUrl: './recruitment-live-management.component.html',
  styleUrls: ['./recruitment-live-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecruitmentLiveManagementComponent implements OnInit {
  private liveService = inject(RecruitmentLiveService);
  private router = inject(Router);
  private message = inject(NzMessageService);
  private fb = inject(FormBuilder);

  loading = signal(false);
  lives = signal<RecruitmentLiveDto[]>([]);
  total = signal(0);
  pageIndex = signal(1);
  pageSize = signal(10);
  filter = signal('');

  // ── Create modal state ──
  createModalVisible = signal(false);
  students = signal<UserBriefDto[]>([]);
  selectedStudentIds = signal<Set<string>>(new Set());
  studentLoading = signal(false);
  studentSearch = signal('');

  form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    scheduledAt: [null as Date | null],
    scheduledEndAt: [null as Date | null],
  });

  ngOnInit() {
    this.loadLives();
  }

  loadLives() {
    this.loading.set(true);
    this.liveService.getTeacherLives({
      filter: this.filter() || undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    }).subscribe({
      next: (res) => {
        this.lives.set(res.items);
        this.total.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载直播列表失败');
      },
    });
  }

  openCreateModal() {
    this.form.reset({ title: '', description: '', scheduledAt: null, scheduledEndAt: null });
    this.selectedStudentIds.set(new Set());
    this.studentSearch.set('');
    this.loadStudents();
    this.createModalVisible.set(true);
  }

  closeCreateModal() {
    this.createModalVisible.set(false);
  }

  loadStudents() {
    this.studentLoading.set(true);
    this.liveService.getTenantStudents(this.studentSearch()).subscribe({
      next: (users) => {
        this.students.set(users);
        this.studentLoading.set(false);
      },
      error: () => {
        this.students.set([]);
        this.studentLoading.set(false);
      },
    });
  }

  onStudentSearch(value: string) {
    this.studentSearch.set(value);
    this.loadStudents();
  }

  toggleStudent(id: string) {
    this.selectedStudentIds.update(s => {
      const next = new Set(s);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  /** 是否可以创建：表单有效且至少选中0或多名学生（0表示创建空直播） */
  canSubmit(): boolean {
    return this.form.valid;
  }

  isSelected(id: string): boolean {
    return this.selectedStudentIds().has(id);
  }

  submit() {
    if (this.form.invalid) {
      Object.values(this.form.controls).forEach(c => {
        if (c.invalid) { c.markAsDirty(); c.updateValueAndValidity({ onlySelf: true }); }
      });
      return;
    }

    this.loading.set(true);
    const val = this.form.value;
    const ids = Array.from(this.selectedStudentIds());

    // 计划时间范围校验：结束时间不能早于开始时间
    if (val.scheduledAt && val.scheduledEndAt && val.scheduledEndAt < val.scheduledAt) {
      this.loading.set(false);
      this.message.error('计划结束时间不能早于计划开始时间');
      return;
    }

    this.liveService.createLive({
      title: val.title!,
      description: val.description || undefined,
      studentIds: ids.length > 0 ? ids : undefined,
      scheduledAt: val.scheduledAt ? val.scheduledAt.toISOString() : undefined,
      scheduledEndAt: val.scheduledEndAt ? val.scheduledEndAt.toISOString() : undefined,
    }).subscribe({
      next: (lives) => {
        this.loading.set(false);
        const codes = lives.map(l => l.roomCode).join(', ');
        const label = '直播创建成功';
        this.message.success(`${label}，房间码: ${codes}`);
        this.closeCreateModal();
        this.loadLives();
      },
      error: (err) => {
        this.loading.set(false);
        this.message.error(err?.error?.error?.message || '创建失败');
      },
    });
  }

  enterLive(live: RecruitmentLiveDto) {
    this.router.navigate(['/admin/recruitment-live', live.id]);
  }

  cancelLive(live: RecruitmentLiveDto) {
    this.liveService.cancelLive(live.id).subscribe({
      next: () => {
        this.message.success('直播已取消');
        this.loadLives();
      },
    });
  }

  deleteLive(live: RecruitmentLiveDto) {
    this.liveService.deleteLive(live.id).subscribe({
      next: () => {
        this.message.success('直播已删除');
        this.loadLives();
      },
    });
  }

  search() {
    this.pageIndex.set(1);
    this.loadLives();
  }

  pageChange(index: number) {
    this.pageIndex.set(index);
    this.loadLives();
  }

  statusColor(live: RecruitmentLiveDto): string {
    if (this.isExpired(live)) return 'error';
    switch (live.status) {
      case RecruitmentLiveStatus.Waiting: return 'gold';
      case RecruitmentLiveStatus.Active: return 'green';
      case RecruitmentLiveStatus.Ended: return 'default';
      case RecruitmentLiveStatus.Cancelled: return 'red';
      default: return 'default';
    }
  }

  statusText(live: RecruitmentLiveDto): string {
    if (this.isExpired(live)) return '已过期';
    switch (live.status) {
      case RecruitmentLiveStatus.Waiting: return '等待中';
      case RecruitmentLiveStatus.Active: return '进行中';
      case RecruitmentLiveStatus.Ended: return '已结束';
      case RecruitmentLiveStatus.Cancelled: return '已取消';
      default: return '未知';
    }
  }

  /** 是否展示“进入”按钮（等待中 / 进行中） */
  showEnterAction(live: RecruitmentLiveDto): boolean {
    return live.status === RecruitmentLiveStatus.Waiting || live.status === RecruitmentLiveStatus.Active;
  }

  canEnter(live: RecruitmentLiveDto): boolean {
    if (live.status === RecruitmentLiveStatus.Active) return true;
    if (live.status === RecruitmentLiveStatus.Waiting) return !this.isExpired(live);
    return false;
  }

  canCancel(live: RecruitmentLiveDto): boolean {
    return live.status === RecruitmentLiveStatus.Waiting || live.status === RecruitmentLiveStatus.Active;
  }

  canDelete(live: RecruitmentLiveDto): boolean {
    return true;
  }

  isExpired(live: RecruitmentLiveDto): boolean {
    // 仅“等待中且已超出计划结束时间”视为过期；已开始的直播（Active）不会过期
    return live.status === RecruitmentLiveStatus.Waiting
      && !!live.scheduledEndAt
      && new Date(live.scheduledEndAt) < new Date();
  }

  /**
   * 该直播对应的学生参与者列表（可能多个）。
   * 优先取参与者表中 role=student 的记录；旧记录无参与者时回退到 StudentName。
   */
  liveStudents(live: RecruitmentLiveDto): ParticipantBriefDto[] {
    return (live.participants || []).filter(p => p.role === 'student');
  }



  /** 格式化计划时间范围，如“2026-08-01 09:00 ~ 2026-08-01 12:00”或“~ 12:00”（同一天仅结束显示时分） */
  scheduleText(live: RecruitmentLiveDto): string {
    if (!live.scheduledAt) return '-';
    const start = new Date(live.scheduledAt);
    const s = `${this.fmt(start)}`;
    if (!live.scheduledEndAt) {
      return s;
    }
    const end = new Date(live.scheduledEndAt);
    // 跨天则结束也显示完整日期，同一天只显示时分
    const sameDay = start.getFullYear() === end.getFullYear()
      && start.getMonth() === end.getMonth()
      && start.getDate() === end.getDate();
    const e = sameDay
      ? `${this.pad(end.getHours())}:${this.pad(end.getMinutes())}`
      : this.fmt(end);
    return `${s} ~ ${e}`;
  }

  private fmt(d: Date): string {
    return `${d.getFullYear()}-${this.pad(d.getMonth() + 1)}-${this.pad(d.getDate())} ${this.pad(d.getHours())}:${this.pad(d.getMinutes())}`;
  }

  private pad(n: number): string {
    return n < 10 ? `0${n}` : `${n}`;
  }
}
