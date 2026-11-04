import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { take } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import {
  AssignmentApiService,
  AssignmentFileItem,
  StudentAssignmentFeedback,
  formatFileSize,
} from '../../learning/assignment-management/assignment.service';

@Component({
  selector: 'app-student-assignment-feedback',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzIconModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './student-assignment-feedback.component.html',
  styleUrls: ['./student-assignment-feedback.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAssignmentFeedbackComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly assignmentApi = inject(AssignmentApiService);
  private readonly message = inject(NzMessageService);

  readonly assignmentId = signal<string>('');
  readonly feedback = signal<StudentAssignmentFeedback | null>(null);
  readonly loading = signal(false);

  ngOnInit(): void {
    this.route.paramMap.pipe(take(1)).subscribe((params) => {
      const id = params.get('id');
      if (!id) {
        this.message.error('缺少作业参数');
        return;
      }
      this.assignmentId.set(id);
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.assignmentApi.getMyFeedback(this.assignmentId()).subscribe({
      next: (feedback) => {
        this.feedback.set(feedback);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载成绩与反馈失败');
      },
    });
  }

  back(): void {
    this.router.navigate(['/student/assignments']);
  }

  openFile(file: AssignmentFileItem): void {
    this.assignmentApi.getFile(file.id).subscribe({
      next: (base64) => {
        const clean = (base64 || '').replace(/^data:[^;]+;base64,/, '');
        const binary = atob(clean);
        const buffer = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) buffer[i] = binary.charCodeAt(i);
        const blob = new Blob([buffer], { type: file.contentType || 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank');
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: (err) => this.message.error(this.errorText(err, '文件读取失败')),
    });
  }

  formatFileSize(bytes: number): string {
    return formatFileSize(bytes);
  }

  formatTime(value?: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  private errorText(err: any, fallback: string): string {
    return err?.error?.error?.message || err?.error?.message || err?.message || fallback;
  }
}
