import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import {
  CreatePracticumSubmissionDto,
  PracticumGuidanceRecordDto,
  PracticumProjectDetailDto,
  PracticumService,
  PracticumSubmissionDto,
  PracticumSubmissionStatus,
} from './practicum.service';

type SubmissionForm = {
  content: string;
  attachmentUrls: string;
  linkUrl: string;
  screenshotUrls: string;
};

@Component({
  selector: 'app-practicum-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzDescriptionsModule,
    NzDividerModule,
    NzInputModule,
    NzProgressModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './practicum-detail.component.html',
  styleUrls: ['./practicum-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticumDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly practicumService = inject(PracticumService);
  private readonly message = inject(NzMessageService);

  readonly project = signal<PracticumProjectDetailDto | null>(null);
  readonly submissions = signal<PracticumSubmissionDto[]>([]);
  readonly guidances = signal<PracticumGuidanceRecordDto[]>([]);
  readonly submissionStatuses = PracticumSubmissionStatus;

  private enrollmentId: string | null = null;
  forms: Record<string, SubmissionForm> = {};

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }

    this.loadProject(id);
  }

  loadProject(id: string): void {
    this.practicumService.getDetail(id).subscribe({
      next: project => {
        this.project.set(project);
        for (const task of project.tasks) {
          this.forms[task.id] = this.forms[task.id] || {
            content: '',
            attachmentUrls: '',
            linkUrl: '',
            screenshotUrls: '',
          };
        }

        if (project.isCurrentUserEnrolled) {
          this.loadMyData(project.id);
        }
      },
    });
  }

  loadMyData(projectId: string): void {
    this.practicumService.getMyEnrollments().subscribe({
      next: enrollments => {
        const enrollment = enrollments.find(x => x.projectId === projectId);
        this.enrollmentId = enrollment?.id || null;
        if (!this.enrollmentId) {
          return;
        }

        this.practicumService.getSubmissionList({
          projectId,
          enrollmentId: this.enrollmentId,
          skipCount: 0,
          maxResultCount: 200,
        }).subscribe(result => this.submissions.set(result.items || []));

        this.practicumService.getGuidanceList(this.enrollmentId).subscribe(items => this.guidances.set(items || []));
      },
    });
  }

  enroll(): void {
    const project = this.project();
    if (!project) {
      return;
    }

    this.practicumService.enroll(project.id).subscribe({
      next: () => {
        this.message.success('已加入实训项目');
        this.loadProject(project.id);
      },
      error: () => this.message.error('加入实训失败'),
    });
  }

  submit(taskId: string): void {
    const project = this.project();
    const form = this.forms[taskId];
    if (!project || !form) {
      return;
    }

    const input: CreatePracticumSubmissionDto = {
      projectId: project.id,
      taskId,
      content: form.content,
      attachmentUrls: form.attachmentUrls,
      linkUrl: form.linkUrl,
      screenshotUrls: form.screenshotUrls,
    };

    this.practicumService.createSubmission(input).subscribe({
      next: () => {
        this.message.success('任务成果已提交');
        this.forms[taskId] = { content: '', attachmentUrls: '', linkUrl: '', screenshotUrls: '' };
        this.loadMyData(project.id);
        this.loadProject(project.id);
      },
      error: () => this.message.error('提交失败'),
    });
  }

  getTaskSubmissions(taskId: string): PracticumSubmissionDto[] {
    return this.submissions().filter(x => x.taskId === taskId);
  }

  getSubmissionStatusLabel(status: PracticumSubmissionStatus): string {
    const labels: Record<number, string> = {
      [PracticumSubmissionStatus.Submitted]: '已提交',
      [PracticumSubmissionStatus.Returned]: '已退回',
      [PracticumSubmissionStatus.Reviewed]: '已评阅',
    };
    return labels[status] || '未知';
  }
}
