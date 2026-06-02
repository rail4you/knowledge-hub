import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzMessageService } from 'ng-zorro-antd/message';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import type { PracticumProjectDetailDto, PracticumTaskDto, PracticumMaterialDto } from '../../proxy/practicums/dtos/models';

@Component({
  selector: 'app-student-practicum-detail',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, FormsModule, RouterModule,
    NzButtonModule, NzIconModule, NzSpinModule, NzTabsModule, NzInputModule, NzModalModule,
  ],
  templateUrl: './student-practicum-detail.component.html',
  styleUrls: ['./student-practicum-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentPracticumDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly practicumService = inject(PracticumService);
  private readonly message = inject(NzMessageService);

  readonly detail = signal<PracticumProjectDetailDto | null>(null);
  readonly loading = signal(true);
  readonly activeTab = signal<'tasks' | 'materials'>('tasks');
  readonly submitting = signal(false);
  readonly submitModalVisible = signal(false);
  readonly selectedTaskId = signal<string | null>(null);
  readonly submissionContent = signal('');
  readonly submissionUrl = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.loadDetail(id);
  }

  loadDetail(id: string): void {
    this.loading.set(true);
    this.practicumService.getDetail(id).subscribe({
      next: result => { this.detail.set(result); this.loading.set(false); },
      error: () => { this.loading.set(false); this.message.error('加载实训详情失败'); },
    });
  }

  enroll(): void {
    const id = this.detail()?.id;
    if (!id) return;
    this.practicumService.enroll(id).subscribe({
      next: () => { this.message.success('报名成功，等待教师审核'); this.loadDetail(id); },
      error: () => this.message.error('报名失败'),
    });
  }

  openSubmit(taskId?: string): void {
    this.selectedTaskId.set(taskId || null);
    this.submissionContent.set('');
    this.submissionUrl.set('');
    this.submitModalVisible.set(true);
  }

  submitWork(): void {
    const projectId = this.detail()?.id;
    const taskId = this.selectedTaskId();
    const content = this.submissionContent().trim();
    if (!projectId || !taskId || !content) {
      this.message.warning('请填写提交内容');
      return;
    }
    this.submitting.set(true);
    this.practicumService.createSubmission({
      projectId, taskId,
      content,
      linkUrl: this.submissionUrl().trim() || undefined,
    }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.submitModalVisible.set(false);
        this.message.success('提交成功');
      },
      error: () => { this.submitting.set(false); this.message.error('提交失败'); },
    });
  }

  downloadMaterial(material: PracticumMaterialDto): void {
    if (!material.resourceUrl) return;
    window.open(material.resourceUrl, '_blank');
  }
}
