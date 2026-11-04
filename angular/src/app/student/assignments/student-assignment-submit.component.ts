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
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import {
  AssignmentApiService,
  AssignmentFileItem,
  StudentAssignmentDetail,
  formatFileSize,
} from '../../learning/assignment-management/assignment.service';

@Component({
  selector: 'app-student-assignment-submit',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzCheckboxModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './student-assignment-submit.component.html',
  styleUrls: ['./student-assignment-submit.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAssignmentSubmitComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly assignmentApi = inject(AssignmentApiService);
  private readonly message = inject(NzMessageService);

  readonly assignmentId = signal<string>('');
  readonly detail = signal<StudentAssignmentDetail | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly uploading = signal(false);

  readonly note = signal('');
  readonly honorPledge = signal(false);
  /** 本次新上传的文件（尚未绑定到提交记录） */
  readonly pendingFiles = signal<AssignmentFileItem[]>([]);

  ngOnInit(): void {
    this.route.paramMap.pipe(take(1)).subscribe((params) => {
      const id = params.get('id');
      if (!id) {
        this.message.error('缺少作业参数');
        return;
      }
      this.assignmentId.set(id);
      this.loadDetail();
    });
  }

  loadDetail(): void {
    this.loading.set(true);
    this.assignmentApi.getMyAssignment(this.assignmentId()).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.note.set(detail.myNote || '');
        this.honorPledge.set(detail.honorPledge);
        this.pendingFiles.set([]);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载作业失败');
      },
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files || []);
    input.value = '';
    if (!files.length) return;

    const detail = this.detail();
    if (!detail) return;

    this.uploading.set(true);
    let pending = 0;
    let failed = 0;

    files.forEach((file) => {
      this.assignmentApi.uploadFile(detail.assignmentId, file, 'submission').subscribe({
        next: (result) => {
          this.pendingFiles.set([...this.pendingFiles(), result]);
          pending++;
          if (pending + failed === files.length) {
            this.uploading.set(false);
            this.message.success(`已上传 ${pending} 个文件`);
          }
        },
        error: (err) => {
          failed++;
          this.message.error(this.errorText(err, `${file.name} 上传失败`));
          if (pending + failed === files.length) {
            this.uploading.set(false);
          }
        },
      });
    });
  }

  removeFile(fileId: string): void {
    this.pendingFiles.set(this.pendingFiles().filter((f) => f.id !== fileId));
  }

  /** 已有提交件（历史版本）也一并提交，保证「以最新版为准」 */
  collectFileIds(): string[] {
    const detail = this.detail();
    const existing = (detail?.myFiles || []).filter((f) => f.version === detail?.version);
    const ids = new Set<string>(existing.map((f) => f.id));
    this.pendingFiles().forEach((f) => ids.add(f.id));
    return Array.from(ids);
  }

  save(isDraft: boolean): void {
    const detail = this.detail();
    if (!detail) return;

    if (!isDraft && !this.honorPledge()) {
      this.message.warning('请勾选学术诚信承诺后再提交');
      return;
    }

    const fileIds = this.collectFileIds();
    if (!isDraft && fileIds.length === 0) {
      this.message.warning('请至少上传一个文件');
      return;
    }

    this.saving.set(true);
    this.assignmentApi
      .submit({
        assignmentId: detail.assignmentId,
        note: this.note() || null,
        honorPledge: this.honorPledge(),
        isDraft: isDraft,
        fileIds,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.message.success(isDraft ? '草稿已保存' : '作业已提交');
          this.loadDetail();
        },
        error: (err) => {
          this.saving.set(false);
          this.message.error(this.errorText(err, isDraft ? '保存草稿失败' : '提交失败'));
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
