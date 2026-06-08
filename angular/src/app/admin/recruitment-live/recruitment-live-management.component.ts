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
import { NzMessageService } from 'ng-zorro-antd/message';
import { RecruitmentLiveService } from '../../recruitment-live/recruitment-live.service';
import { RecruitmentLiveDto, RecruitmentLiveStatus, UserBriefDto } from '../../recruitment-live/recruitment-live.models';

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
  ],
  templateUrl: './recruitment-live-management.component.html',
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
  studentLoading = signal(false);
  studentSearch = signal('');

  form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    studentId: [null as string | null],
    scheduledAt: [null as Date | null],
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
    this.form.reset({
      title: '',
      description: '',
      studentId: null,
      scheduledAt: null,
    });
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

  submit() {
    if (this.form.invalid) {
      Object.values(this.form.controls).forEach(c => {
        if (c.invalid) { c.markAsDirty(); c.updateValueAndValidity({ onlySelf: true }); }
      });
      return;
    }

    this.loading.set(true);
    const val = this.form.value;
    this.liveService.createLive({
      title: val.title!,
      description: val.description || undefined,
      studentId: val.studentId || undefined,
      scheduledAt: val.scheduledAt ? val.scheduledAt.toISOString() : undefined,
    }).subscribe({
      next: (live) => {
        this.loading.set(false);
        this.message.success(`直播创建成功，房间码: ${live.roomCode}`);
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

  statusColor(status: RecruitmentLiveStatus): string {
    switch (status) {
      case RecruitmentLiveStatus.Waiting: return 'gold';
      case RecruitmentLiveStatus.Active: return 'green';
      case RecruitmentLiveStatus.Ended: return 'default';
      case RecruitmentLiveStatus.Cancelled: return 'red';
      default: return 'default';
    }
  }

  statusText(status: RecruitmentLiveStatus): string {
    switch (status) {
      case RecruitmentLiveStatus.Waiting: return '等待中';
      case RecruitmentLiveStatus.Active: return '进行中';
      case RecruitmentLiveStatus.Ended: return '已结束';
      case RecruitmentLiveStatus.Cancelled: return '已取消';
      default: return '未知';
    }
  }

  canEnter(live: RecruitmentLiveDto): boolean {
    return live.status === RecruitmentLiveStatus.Waiting || live.status === RecruitmentLiveStatus.Active;
  }

  canCancel(live: RecruitmentLiveDto): boolean {
    return live.status === RecruitmentLiveStatus.Waiting;
  }

  canDelete(live: RecruitmentLiveDto): boolean {
    return live.status === RecruitmentLiveStatus.Ended || live.status === RecruitmentLiveStatus.Cancelled;
  }
}
