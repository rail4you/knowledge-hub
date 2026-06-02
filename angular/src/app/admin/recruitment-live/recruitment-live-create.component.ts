import { Component, ChangeDetectionStrategy, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { RecruitmentLiveService } from '../../recruitment-live/recruitment-live.service';
import { UserBriefDto } from '../../recruitment-live/recruitment-live.models';
import { debounceTime, distinctUntilChanged, switchMap, of } from 'rxjs';

@Component({
  selector: 'app-recruitment-live-create',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    ReactiveFormsModule,
    NzFormModule,
    NzInputModule,
    NzButtonModule,
    NzSelectModule,
    NzDatePickerModule,
    NzIconModule,
  ],
  templateUrl: './recruitment-live-create.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecruitmentLiveCreateComponent {
  private fb = inject(FormBuilder);
  private liveService = inject(RecruitmentLiveService);
  private router = inject(Router);
  private message = inject(NzMessageService);

  loading = signal(false);
  students = signal<UserBriefDto[]>([]);
  studentLoading = signal(false);
  studentSearch = signal('');

  form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    studentId: [null as string | null],
    scheduledAt: [null as Date | null],
  });

  constructor() {
    this.loadStudents();
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
        this.router.navigate(['/admin/recruitment-live']);
      },
      error: (err) => {
        this.loading.set(false);
        this.message.error(err?.error?.error?.message || '创建失败');
      },
    });
  }

  goBack() {
    this.router.navigate(['/admin/recruitment-live']);
  }
}
