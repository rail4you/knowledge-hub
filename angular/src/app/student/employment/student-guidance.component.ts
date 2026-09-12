import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzTimelineModule } from 'ng-zorro-antd/timeline';
import { Subject, interval, takeUntil } from 'rxjs';
import { EmploymentService, type StudentResumeDto } from '../../employment/employment.service';
import { ChatService } from '../../ai/services/chat.service';
import { AiTaskService, AiTaskStatus, AiTaskType, type AiGenerationTaskDto } from '../../ai/services/ai-task.service';
import { AiTaskNotificationService } from '../../ai/services/ai-task-notification.service';
import { ClientCacheService } from '../../shared/cache/client-cache.service';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';

interface GuidanceAssessment {
  careerMatchScore: number;
  strengths: string[];
  areasForImprovement: string[];
  summary: string;
  educationBackground?: {
    school: string;
    degree: string;
    major: string;
    period: string;
  }[];
  workExperience?: {
    company: string;
    position: string;
    period: string;
    description: string;
  }[];
}

interface GuidanceRecommendedPath {
  title: string;
  description: string;
  matchScore: number;
  requiredSkills: string[];
  salaryRange: string;
  growthPotential: string;
}

interface GuidanceSkillGap {
  skill: string;
  currentLevel: string;
  targetLevel: string;
  priority: string;
}

interface GuidanceActionItem {
  id: string;
  title: string;
  description: string;
  timeline: string;
  priority: string;
}

interface GuidanceResult {
  title: string;
  assessment: GuidanceAssessment;
  recommendedPaths: GuidanceRecommendedPath[];
  skillGaps: GuidanceSkillGap[];
  actionPlan: GuidanceActionItem[];
  nextSteps: string[];
}

interface GuidanceListItem {
  id: string;
  title: string;
  guidedAt: string;
  careerGoal?: string;
  content: string;
  /** 解析后的结构化结果，用于预览 */
  parsed?: GuidanceResult | null;
}

@Component({
  selector: 'app-student-guidance',
  standalone: true,
  imports: [
    CommonModule, DatePipe, FormsModule,
    NzIconModule, NzSpinModule, NzEmptyModule, NzTableModule,
    NzModalModule, NzButtonModule, NzInputModule, NzSelectModule,
    NzProgressModule, NzCollapseModule,
    NzDescriptionsModule, NzTimelineModule,
    StudentHeroComponent,
  ],
  templateUrl: './student-guidance.component.html',
  styleUrls: ['./student-guidance.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentGuidanceComponent implements OnInit, OnDestroy {
  private readonly employmentService = inject(EmploymentService);
  private readonly chatService = inject(ChatService);
  private readonly aiTaskService = inject(AiTaskService);
  private readonly aiTaskNotifications = inject(AiTaskNotificationService);
  private readonly message = inject(NzMessageService);
  private readonly cache = inject(ClientCacheService);
  private readonly destroy$ = new Subject<void>();

  readonly items = signal<GuidanceListItem[]>([]);
  readonly loading = signal(false);
  readonly exporting = signal(false);

  /** 当前预览的记录（null = 关闭预览） */
  readonly previewItem = signal<GuidanceListItem | null>(null);

  // ========== AI 自助生成（多任务并行） ==========
  /** 我的简历（生成指导的数据来源） */
  readonly resumes = signal<StudentResumeDto[]>([]);
  readonly resumesLoading = signal(false);
  readonly selectedResumeId = signal<string | null>(null);
  /** 职业目标（可选，写进 AI 提示词） */
  readonly careerGoalInput = signal('');
  /** 正在提交任务（创建接口在途） */
  readonly submitting = signal(false);
  /** 进行中的后台任务（可多个并行，带进度） */
  readonly pendingTasks = signal<AiGenerationTaskDto[]>([]);
  /** 正在保存/拉取中的任务 id（防重复保存） */
  private readonly savingTaskIds = new Set<string>();
  /** 已提示过失败的任务 id（失败只提示一次） */
  private readonly notifiedFailedIds = new Set<string>();

  /** 模板用：Running 状态枚举（进度条 active 态） */
  readonly runningStatus = AiTaskStatus.Running;

  readonly selectedResume = computed(() => {
    const id = this.selectedResumeId();
    if (!id) return null;
    return this.resumes().find(r => r.id === id) ?? null;
  });

  readonly canGenerate = computed(() =>
    !!this.selectedResumeId() && !this.submitting());

  /** Hero 区数据总览 */
  readonly heroStats = computed(() => {
    const items = this.items();
    const now = new Date();
    const monthCount = items.filter(x => {
      const d = new Date(x.guidedAt);
      return !isNaN(d.getTime()) && d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth();
    }).length;
    return [
      { label: '指导记录', value: items.length, suffix: '条', icon: 'compass', color: '#2b6cd4' },
      { label: '本月新增', value: monthCount, suffix: '条', icon: 'calendar', color: '#2b6cd4' },
    ];
  });

  ngOnInit(): void {
    this.reload();
    this.loadResumes();
    // 我的职业规划任务对账：进行中的进进度列表，已完成未保存的自动保存
    // （离开页面期间完成的任务，回来时在这里补保存；全局通知 toast 照常推送）
    this.refreshMyTasks();
    // 进度轮询：只在有进行中任务时刷新，离开页面自动取消，重进页面重新对账
    interval(3000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        if (this.pendingTasks().length > 0) this.refreshMyTasks();
      });
    // 本页打开期间完成的任务实时保存
    this.aiTaskNotifications.completed$
      .pipe(takeUntil(this.destroy$))
      .subscribe(tasks => {
        const mine = tasks.filter(t => t.taskType === AiTaskType.CareerGuidance);
        if (mine.length === 0) return;
        this.refreshMyTasks();
        for (const t of mine) this.ensureSaved(t);
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /** 加载我的简历（默认选中默认简历） */
  loadResumes(): void {
    this.resumesLoading.set(true);
    this.employmentService.getMyResumeList()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: list => {
          this.resumes.set(list || []);
          this.resumesLoading.set(false);
          if (!this.selectedResumeId() && (list || []).length > 0) {
            const def = (list || []).find(r => r.isDefault) || (list || [])[0];
            this.selectedResumeId.set(def.id);
          }
        },
        error: () => this.resumesLoading.set(false),
      });
  }

  reload(): void {
    this.loading.set(true);
    this.cache.load<any>('student.employment', 'guidance-list', () => this.employmentService.getMyGuidanceRecordList({ skipCount: 0, maxResultCount: 100 }))
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => {
          const items: GuidanceListItem[] = (result.items || []).map(r => ({
            id: r.id!,
            title: r.title || '未命名就业指导',
            guidedAt: r.guidedAt!,
            careerGoal: r.careerGoal,
            content: r.content || '',
            parsed: this.tryParseAiResult(r.content),
          }));
          this.items.set(items);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载就业指导失败');
        },
      });
  }

  // ========== AI 自助生成（多任务并行） ==========
  /** 发起 AI 就业指导：按所选简历提交后台任务，可连续提交多个，完成后自动保存 */
  generate(): void {
    const resume = this.selectedResume();
    if (!resume || this.submitting()) return;

    const resumeContent = this.buildResumeContent(resume);
    if (!resumeContent.trim()) {
      this.message.warning('该简历内容为空，请先完善简历');
      return;
    }

    const careerGoal = this.careerGoalInput().trim();
    const payload = {
      resumeContent,
      resumeTitle: resume.title,
      careerGoal: careerGoal || undefined,
      attachmentUrl: resume.attachmentUrl || undefined,
    };

    this.submitting.set(true);
    this.aiTaskService
      .create({
        taskType: AiTaskType.CareerGuidance,
        title: resume.title ? `我的职业规划：${resume.title}` : '我的职业规划',
        inputJson: JSON.stringify(payload),
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: task => {
          this.submitting.set(false);
          this.pendingTasks.update(list => [task, ...list.filter(t => t.id !== task.id)]);
          const n = this.pendingTasks().length;
          this.message.success(`已提交后台生成（进行中 ${n} 个），完成后会自动保存，可离开本页`);
        },
        error: err => {
          this.submitting.set(false);
          this.message.error(this.friendlyTaskError(err?.error?.error?.message, '提交失败，请稍后重试'));
        },
      });
  }

  /** 取消进行中的任务 */
  cancelTask(id: string): void {
    this.aiTaskService.cancel(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.pendingTasks.update(list => list.filter(t => t.id !== id));
          this.message.success('已取消该生成任务');
        },
        error: () => this.message.error('取消失败，请重试'),
      });
  }

  /** 拉取我的职业规划任务：进行中的进进度列表，已完成未保存的自动保存 */
  private refreshMyTasks(): void {
    this.aiTaskService
      .getList({ taskType: AiTaskType.CareerGuidance, onlyMine: true, maxResultCount: 20 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          const items = res.items || [];
          const running = items.filter(t =>
            t.status === AiTaskStatus.Pending || t.status === AiTaskStatus.Running);
          this.pendingTasks.set(running);
          for (const t of items) {
            if (t.status === AiTaskStatus.Completed) {
              this.ensureSaved(t);
            } else if (t.status === AiTaskStatus.Failed || t.status === AiTaskStatus.Cancelled) {
              if (!this.notifiedFailedIds.has(t.id)) {
                this.notifiedFailedIds.add(t.id);
                this.message.error(`「${t.title}」生成失败：${this.friendlyTaskError(t.errorMessage)}`);
              }
            }
          }
        },
      });
  }

  /**
   * 已完成任务自动保存为我的指导记录（幂等：保存中/已保存的不重复）。
   * 列表接口不带 resultJson 大字段，缺失时取详情后再保存。
   */
  private ensureSaved(task: AiGenerationTaskDto): void {
    if (this.savingTaskIds.has(task.id)) return;
    if (task.resultJson) {
      this.saveGeneratedResult(task, task.resultJson);
      return;
    }
    this.savingTaskIds.add(task.id);
    this.aiTaskService
      .get(task.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: full => {
          this.savingTaskIds.delete(task.id);
          if (full.resultJson) this.ensureSaved({ ...task, resultJson: full.resultJson });
        },
        error: () => this.savingTaskIds.delete(task.id),
      });
  }

  /** 解析并自动保存为我的指导记录，然后刷新列表 */
  private saveGeneratedResult(task: AiGenerationTaskDto, resultJson: string): void {
    // 已保存过（内容相同）则跳过
    if (this.items().some(i => i.content === resultJson)) return;
    const parsed = this.tryParseAiResult(resultJson);
    if (!parsed) return;
    const careerGoal = this.extractCareerGoal(task);
    this.savingTaskIds.add(task.id);
    const title = (parsed.title || careerGoal || '我的就业指导').trim();
    this.employmentService
      .createMyAIGuidanceRecord({
        title,
        content: resultJson,
        careerGoal: careerGoal || undefined,
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.savingTaskIds.delete(task.id);
          this.message.success(`「${title}」已生成并保存`);
          // 列表缓存失效后刷新
          this.cache.invalidate('student.employment', 'guidance-list');
          this.reload();
        },
        error: err => {
          this.savingTaskIds.delete(task.id);
          this.message.error('保存失败：' + (err?.error?.error?.message || '未知错误'));
        },
      });
  }

  /** 从任务 inputJson 还原职业目标 */
  private extractCareerGoal(task: AiGenerationTaskDto): string {
    try {
      const input = JSON.parse(task.inputJson || '{}') as { careerGoal?: string };
      return (input.careerGoal || '').trim();
    } catch {
      return '';
    }
  }

  /**
   * 任务失败原因转中文友好提示：后端业务异常本身是中文的可直接展示；
   * 第三方 SDK/HTTP 原文（含英文、技术性长文本）统一转成“服务不可用”类中文，不透出英文。
   */
  private friendlyTaskError(msg?: string | null, fallback = 'AI 服务暂时不可用，请稍后重试'): string {
    const text = (msg || '').trim();
    if (!text) return fallback;
    const hasChinese = /[\u4e00-\u9fa5]/.test(text);
    if (hasChinese && text.length <= 200) return text;
    return fallback;
  }

  /** 将简历 DTO 构建为 AI 提示用的文本（与管理端一致） */
  private buildResumeContent(r: StudentResumeDto): string {
    const parts: string[] = [];
    if (r.fullName) parts.push(`姓名：${r.fullName}`);
    if (r.schoolName) parts.push(`学校：${r.schoolName}`);
    if (r.major) parts.push(`专业：${r.major}`);
    if (r.grade) parts.push(`年级：${r.grade}`);
    if (r.summary) parts.push(`个人总结：${r.summary}`);
    if (r.skills) parts.push(`技能：${r.skills}`);
    if (r.educationExperience) parts.push(`教育经历：${r.educationExperience}`);
    if (r.internshipExperience) parts.push(`实习经历：${r.internshipExperience}`);
    if (r.projectExperience) parts.push(`项目经历：${r.projectExperience}`);
    if (r.certificateText) parts.push(`证书：${r.certificateText}`);
    return parts.join('\n');
  }

  // ========== 预览 ==========
  openPreview(item: GuidanceListItem): void {
    this.previewItem.set(item);
  }

  closePreview(): void {
    this.previewItem.set(null);
  }

  // ========== 下载 DOCX ==========
  downloadDocx(item: GuidanceListItem): void {
    if (!item.content) {
      this.message.warning('该记录暂无内容可下载');
      return;
    }
    this.exporting.set(true);
    this.chatService.exportCareerGuidanceDocx(item.content)
      .then(blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${item.title}_${new Date().toISOString().slice(0, 10)}.docx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        this.exporting.set(false);
        this.message.success('报告已开始下载');
      })
      .catch(() => {
        this.exporting.set(false);
        this.message.error('导出失败，请重试');
      });
  }

  getPriorityColor(p: string): string {
    switch (p) {
      case '高': return 'red';
      case '中': return 'orange';
      case '低': return 'green';
      default: return 'default';
    }
  }

  private tryParseAiResult(content: string | undefined): GuidanceResult | null {
    if (!content) return null;
    const trimmed = content.trim();
    if (!trimmed.startsWith('{') && !trimmed.startsWith('```')) return null;
    let json = trimmed;
    if (json.startsWith('```')) {
      const nl = json.indexOf('\n');
      if (nl >= 0) json = json.substring(nl + 1);
      if (json.endsWith('```')) json = json.substring(0, json.length - 3).trimEnd();
    }
    try {
      const parsed = JSON.parse(json);
      if (parsed?.assessment) return parsed as GuidanceResult;
      return null;
    } catch {
      return null;
    }
  }
}
