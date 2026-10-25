import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { EmploymentService } from '../../proxy/employment/employment.service';
import type {
  InterviewScheduleDto,
  JobPostingDto,
  StudentResumeDto,
} from '../../proxy/employment/dtos/models';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentEmploymentFilesService } from './student-employment-files.service';
import {
  applicationStage,
  applicationStatusLabel,
  applicationStatusTone,
  jobTypeLabel,
  splitTagList,
} from './employment-labels';

/**
 * 学生端 · 岗位详情与投递（对齐原型 `#job`）。
 *
 * 结构：岗位概要 → 岗位职责 / 能力要求 / 补学建议 → 授权投递表单 → 投递状态与面试安排。
 *
 * 数据来源（全部真实接口）：
 *   · 岗位详情 GET  /api/app/employment/{id}/job
 *   · 我的简历   GET  /api/app/employment/my-resume-list
 *   · 投递       POST /api/app/employment/application
 *   · 面试安排   GET  /api/app/employment/interview-list?jobPostingId=
 *
 * 后端规则：同一岗位重复投递会被拒绝（UserFriendlyException「您已投递过该岗位。」），
 * 这里如实展示后端返回的原因，不做假成功。
 */
@Component({
  selector: 'app-student-job-detail',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
  ],
  templateUrl: './student-job-detail.component.html',
  styleUrls: ['./student-job-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentJobDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly employmentService = inject(EmploymentService);
  private readonly filesService = inject(StudentEmploymentFilesService);
  private readonly message = inject(NzMessageService);

  readonly job = signal<JobPostingDto | null>(null);
  readonly resumes = signal<StudentResumeDto[]>([]);
  readonly interview = signal<InterviewScheduleDto | null>(null);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  /** 岗位不存在 / 已下线（404、403）时的空态 */
  readonly notFound = signal(false);
  readonly submitting = signal(false);

  /** 投递表单 */
  readonly selectedResumeId = signal('');
  readonly coverLetter = signal('');
  /** 授权勾选：必须明确同意向该企业提供本次简历才能提交 */
  readonly consented = signal(false);
  /** 后端返回的失败原因（如「您已投递过该岗位。」） */
  readonly submitError = signal('');

  /** 岗位类型文案 */
  readonly typeLabel = computed(() => jobTypeLabel(this.job()?.jobType));

  /** 能力要求（技能标签） */
  readonly skillTags = computed(() => splitTagList(this.job()?.skillTags));

  /** 福利待遇 */
  readonly benefitTags = computed(() => splitTagList(this.job()?.benefits));

  /** 投递状态文案 / 色调 */
  readonly statusLabel = computed(() => applicationStatusLabel(this.job()?.applicationStatus));
  readonly statusTone = computed(() => applicationStatusTone(this.job()?.applicationStatus));

  /** 求职进度阶段：0 未投递 / 1 已投递 / 2 面试 / 3 面试完成 / 4 录用 / -1 未通过 */
  readonly stage = computed(() => applicationStage(this.job()?.applicationStatus));

  /** 是否有可投递的简历 */
  readonly hasResumes = computed(() => this.resumes().length > 0);

  /** 是否可以提交：已勾选授权 + 选了简历 + 未在提交中 */
  readonly canSubmit = computed(() => this.consented() && !!this.selectedResumeId() && !this.submitting());

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.message.warning('岗位不存在');
      this.router.navigate(['/student/employment/jobs']);
      return;
    }

    this.loading.set(true);
    this.loadFailed.set(false);
    this.notFound.set(false);

    this.employmentService.getJob(id, { skipHandleError: true }).subscribe({
      next: job => {
        this.job.set(job);
        this.loading.set(false);
        if (job.hasApplied) this.loadInterview(job.id);
      },
      error: err => {
        this.loading.set(false);
        // 404 / 403：岗位已删除或已下线（业务空态），其余视为服务失败
        if (err?.status === 404 || err?.status === 403) {
          this.notFound.set(true);
        } else {
          this.loadFailed.set(true);
        }
      },
    });

    this.employmentService.getMyResumeList().subscribe({
      next: list => {
        const resumes = list || [];
        this.resumes.set(resumes);
        // 默认简历优先：与「默认版本只影响后续选择」的规则一致
        const preferred = resumes.find(r => r.isDefault) || resumes[0];
        this.selectedResumeId.set(preferred?.id || '');
      },
      error: () => this.resumes.set([]),
    });
  }

  /** 加载该岗位关联的面试安排 */
  private loadInterview(jobId: string): void {
    this.employmentService
      .getInterviewList({ jobPostingId: jobId, skipCount: 0, maxResultCount: 20 })
      .subscribe({
        next: result => {
          const list = result?.items || [];
          if (!list.length) return;
          list.sort((a, b) => new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime());
          this.interview.set(list[0]);
        },
        error: () => undefined,
      });
  }

  /** 重新加载（错误态重试） */
  reload(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.loading.set(true);
    this.loadFailed.set(false);
    this.notFound.set(false);
    this.employmentService.getJob(id, { skipHandleError: true }).subscribe({
      next: job => {
        this.job.set(job);
        this.loading.set(false);
        if (job.hasApplied) this.loadInterview(job.id);
      },
      error: err => {
        this.loading.set(false);
        // 404 / 403：岗位已删除或已下线（业务空态），其余视为服务失败
        if (err?.status === 404 || err?.status === 403) {
          this.notFound.set(true);
        } else {
          this.loadFailed.set(true);
        }
      },
    });
  }

  /** 提交投递 */
  submit(): void {
    const job = this.job();
    if (!job?.id || !this.canSubmit()) return;

    this.submitting.set(true);
    this.submitError.set('');

    this.employmentService
      .createApplication({
        jobPostingId: job.id,
        resumeId: this.selectedResumeId(),
        coverLetter: this.coverLetter().trim() || undefined,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.message.success('投递成功，可在「我的投递」查看进度');
          this.reload();
        },
        error: err => {
          this.submitting.set(false);
          // 后端对重复投递会返回「您已投递过该岗位。」，如实展示，不做假成功
          const reason = this.serverMessage(err, '投递失败，请稍后重试');
          this.submitError.set(reason);
          this.message.error(reason);
        },
      });
  }

  /** 抽取后端返回的业务错误信息 */
  private serverMessage(err: any, fallback: string): string {
    const raw: string =
      err?.error?.error?.message || err?.error?.message || err?.message || '';
    const text = (raw || '').trim();
    if (!text) return fallback;
    // 只展示可读的中文业务提示，过滤技术性长文本
    if (text.length <= 120 && /[\u4e00-\u9fa5]/.test(text)) return text;
    return fallback;
  }

  /** 截止日期提示 */
  deadlineHint(item: JobPostingDto): string {
    if (!item.deadline) return '长期有效';
    const end = new Date(item.deadline).getTime();
    if (Number.isNaN(end)) return '长期有效';
    const days = Math.ceil((end - Date.now()) / 86400000);
    if (days < 0) return '已截止';
    if (days === 0) return '今天截止';
    return `剩 ${days} 天`;
  }

  /** 是否已过截止日期 */
  isExpired(item: JobPostingDto): boolean {
    if (!item.deadline) return false;
    const end = new Date(item.deadline).getTime();
    if (Number.isNaN(end)) return false;
    return end < Date.now();
  }

  /** 工作地点：地点 · 详细地址 */
  workplace(item: JobPostingDto): string {
    const loc = (item.location || '').trim() || '待定';
    const addr = (item.address || '').trim();
    if (!addr || addr === loc) return loc;
    return `${loc} · ${addr}`;
  }

  /** 岗位宣传视频地址（已发布岗位匿名可播） */
  videoSrc(): string | null {
    const path = this.job()?.videoUrl;
    if (!path) return null;
    return `${this.filesService.jobVideoUrl(path)}#t=0.1`;
  }

  /** 去我的简历 */
  goResumes(): void {
    this.router.navigate(['/student/employment/my-resumes']);
  }

  /** 去我的投递 */
  goApplications(): void {
    this.router.navigate(['/student/employment/my-applications']);
  }

  /** 回岗位列表 */
  goJobs(): void {
    this.router.navigate(['/student/employment/jobs']);
  }
}
