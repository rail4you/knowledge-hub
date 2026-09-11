import { Component, signal, inject, computed, ChangeDetectionStrategy, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTimelineModule } from 'ng-zorro-antd/timeline';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subject, Subscription, takeUntil } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { ChatService } from '../services/chat.service';
import { AiGenerationTaskDto, AiTaskService, AiTaskStatus, AiTaskType } from '../services/ai-task.service';
import { AiTaskNotificationService } from '../services/ai-task-notification.service';
import { EmploymentService, StudentResumeDto, EmploymentGuidanceRecordDto, CareerGuidanceStudentDto } from '../../employment/employment.service';

interface CareerGuidanceResult {
  title: string;
  assessment: {
    careerMatchScore: number;
    strengths: string[];
    areasForImprovement: string[];
    summary: string;
    educationBackground: {
      school: string;
      degree: string;
      major: string;
      period: string;
    }[];
    workExperience: {
      company: string;
      position: string;
      period: string;
      description: string;
    }[];
  };
  recommendedPaths: {
    title: string;
    description: string;
    matchScore: number;
    requiredSkills: string[];
    salaryRange: string;
    growthPotential: string;
  }[];
  skillGaps: {
    skill: string;
    currentLevel: string;
    targetLevel: string;
    priority: string;
  }[];
  actionPlan: {
    id: string;
    title: string;
    description: string;
    timeline: string;
    priority: string;
  }[];
  nextSteps: string[];
}

interface ParsedRecord {
  id: string;
  title: string;
  careerGoal?: string;
  studentName?: string;
  guidedAt: string;
  content: string;
  parsed: CareerGuidanceResult | null;
  /** 来自 AI 任务（尚未保存为指导记录），仅可预览 / 下载 */
  fromTask?: boolean;
  /** 任务 inputJson（用于反查所属学生/职业目标） */
  taskInputJson?: string;
}

@Component({
  selector: 'app-career-guidance',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzInputModule,
    NzButtonModule,
    NzCardModule,
    NzSelectModule,
    NzDividerModule,
    NzCollapseModule,
    NzDescriptionsModule,
    NzTagModule,
    NzProgressModule,
    NzSpinModule,
    NzTableModule,
    NzTimelineModule,
    NzIconModule,
    NzEmptyModule,
    NzTabsModule,
    NzTooltipModule,
    NzEmptyModule,
  ],
  templateUrl: './career-guidance.component.html',
  styleUrls: ['./career-guidance.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CareerGuidanceComponent implements OnInit, OnDestroy {
  private readonly chatService = inject(ChatService);
  private readonly employmentService = inject(EmploymentService);
  private readonly messageService = inject(NzMessageService);
  private readonly aiTaskService = inject(AiTaskService);
  private readonly aiTaskNotifications = inject(AiTaskNotificationService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy$ = new Subject<void>();
  private taskPollSub: Subscription | null = null;
  private lastPreviewTaskId: string | null = null;
  currentTaskId = signal<string | null>(null);

  // ============= 学生列表 =============
  readonly students = signal<CareerGuidanceStudentDto[]>([]);
  readonly studentsLoading = signal(false);
  readonly searchText = signal('');
  readonly selectedStudentId = signal<string | null>(null);

  readonly filteredStudents = computed(() => {
    const kw = this.searchText().trim().toLowerCase();
    const all = this.students();
    if (!kw) return all;
    return all.filter(s => s.studentName.toLowerCase().includes(kw));
  });

  readonly selectedStudent = computed(() => {
    const id = this.selectedStudentId();
    if (!id) return null;
    return this.students().find(s => s.studentId === id) ?? null;
  });

  // ============= 简历选择 =============
  resumes = signal<StudentResumeDto[]>([]);
  selectedResumeId = signal<string | null>(null);
  selectedResume = computed(() => {
    const id = this.selectedResumeId();
    if (!id) return null;
    return this.resumes().find(r => r.id === id) ?? null;
  });

  // ============= 全部学生记录（Tab 2） =============
  readonly allRecords = signal<ParsedRecord[]>([]);
  readonly allRecordsLoading = signal(false);
  readonly allRecordsLoaded = signal(false);
  readonly previewItem = signal<ParsedRecord | null>(null);

  // ============= 生成区 =============
  careerGoal = signal('');
  result = signal<CareerGuidanceResult | null>(null);
  rawJson = signal('');
  isLoading = signal(false);
  isExporting = signal(false);
  isSaving = signal(false);
  /** 已成功保存的数据库记录 ID，保存后展示「已保存」状态。 */
  savedRecordId = signal<string | null>(null);

  canGenerate = computed(() => {
    return !!this.selectedStudentId() && !!this.selectedResumeId() && !this.isLoading();
  });

  ngOnInit() {
    this.loadStudents();

    // ?taskId= 深度链接（通知 / 任务中心跳转）：响应式订阅，页内跳转同样生效
    this.route.queryParamMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const taskId = params.get('taskId');
        if (taskId && taskId !== this.lastPreviewTaskId) {
          this.lastPreviewTaskId = taskId;
          this.loadTaskPreview(taskId);
        }
      });

    // 任务完成实时同步：后台生成成功后，历史记录自动更新
    this.aiTaskNotifications.completed$
      .pipe(takeUntil(this.destroy$))
      .subscribe(tasks => {
        const mine = tasks.filter(t => t.taskType === AiTaskType.CareerGuidance);
        if (mine.length === 0) return;
        if (this.allRecordsLoaded()) {
          for (const t of mine) {
            const json = t.resultJson;
            if (json) {
              this.mergeTaskResult(t, json);
            } else {
              this.aiTaskService
                .get(t.id)
                .pipe(takeUntil(this.destroy$))
                .subscribe({ next: (full) => {
                  if (full.resultJson) this.mergeTaskResult(full, full.resultJson);
                }});
            }
          }
        } else {
          // 历史尚未加载过：失效标记，下次切页时自动带出任务结果
          this.allRecordsLoaded.set(false);
        }
      });
  }

  ngOnDestroy() {
    this.cancelTaskPolling();
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadStudents() {
    this.studentsLoading.set(true);
    this.employmentService.getCareerGuidanceStudents()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.students.set(data || []);
          this.studentsLoading.set(false);
          this.fillTaskContexts();
        },
        error: () => {
          this.studentsLoading.set(false);
          this.messageService.error('加载学生列表失败');
        }
      });
  }

  selectStudent(studentId: string) {
    this.selectedStudentId.set(studentId);
    this.selectedResumeId.set(null);
    this.resumes.set(this.selectedStudent()?.resumes || []);
    this.resetResult();
  }

  /** 简历附件预览：走后端预览端点（新标签页内联展示，Word 会先转 PDF）。 */
  previewResumeAttachment(event: Event): void {
    event.stopPropagation();
    const url = this.selectedResume()?.attachmentUrl;
    if (!url) {
      this.messageService.warning('该简历暂无附件');
      return;
    }
    window.open(this.employmentService.getResumePreviewUrl(url), '_blank', 'noopener');
  }

  /** 当前 Tab：0 = 生成就业指导，1 = 就业指导历史记录 */
  readonly activeTabIndex = signal(0);

  /** Tab 切换到「就业指导历史记录」时懒加载全租户记录 */
  onTabChange(index: number): void {
    this.activeTabIndex.set(index);
    if (index === 1 && !this.allRecordsLoaded()) {
      this.loadAllRecords();
    }
  }

  loadAllRecords(): void {
    this.allRecordsLoading.set(true);
    this.employmentService.getGuidanceRecordList({
      skipCount: 0,
      maxResultCount: 100,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => {
          const records: ParsedRecord[] = (result.items || []).map(r => ({
            id: r.id!,
            title: r.title || '未命名就业指导',
            careerGoal: r.careerGoal,
            studentName: r.studentName,
            guidedAt: r.guidedAt!,
            content: r.content || '',
            parsed: this.tryParseAiResult(r.content),
          }));
          // 保留已在展示中的任务条目（?taskId= 深链），避免切页时预览闪失
          const keepTasks = this.allRecords().filter(r => r.fromTask && !records.some(x => x.id === r.id));
          this.allRecords.set([...records, ...keepTasks]);
          this.allRecordsLoaded.set(true);
          this.allRecordsLoading.set(false);
          // AI 任务结果也并入历史：未点保存的生成内容同样可见（可预览/下载）
          this.syncTaskResults();
        },
        error: () => {
          this.allRecordsLoading.set(false);
          this.messageService.error('加载就业指导历史记录失败');
        },
      });
  }

  /**
   * 把我名下已完成的职业规划 AI 任务并入历史（去重：已保存为指导记录的不重复显示）。
   * 列表接口不返回 ResultJson 大字段，缺失时取详情后再合并。
   */
  private syncTaskResults(): void {
    this.aiTaskService
      .getList({ taskType: AiTaskType.CareerGuidance, status: AiTaskStatus.Completed, onlyMine: true, maxResultCount: 20 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          const savedContents = new Set(this.allRecords().filter(r => !r.fromTask).map(r => r.content));
          const missing = (res.items || [])
            .filter((t) => !this.allRecords().some((x) => x.id === t.id))
            .slice(0, 10);
          for (const t of missing) {
            if (t.resultJson) {
              this.mergeTaskResult(t, t.resultJson, savedContents);
            } else {
              this.aiTaskService
                .get(t.id)
                .pipe(takeUntil(this.destroy$))
                .subscribe({
                  next: (full) => {
                    if (full.resultJson) this.mergeTaskResult(full, full.resultJson, savedContents);
                  },
                });
            }
          }
        },
      });
  }

  /** 单条任务结果并入历史（已入库的跳过），返回是否新增。 */
  private mergeTaskResult(task: AiGenerationTaskDto, resultJson: string, savedContents?: Set<string>): boolean {
    if (this.allRecords().some((x) => x.id === task.id)) return false;
    const contents = savedContents ?? new Set(this.allRecords().filter(r => !r.fromTask).map(r => r.content));
    if (contents.has(resultJson)) return false;
    const record = this.buildTaskRecord(task, resultJson);
    if (!record) return false;
    this.allRecords.update((list) =>
      [...list, record].sort((a, b) => +new Date(b.guidedAt) - +new Date(a.guidedAt)));
    return true;
  }

  /** 后端任务 → 历史条目（与其他 AI 功能页一致），内容不可解析返回 null。 */
  private buildTaskRecord(task: AiGenerationTaskDto, resultJson: string): ParsedRecord | null {
    const parsed = this.tryParseAiResult(resultJson);
    if (!parsed) return null;
    const ctx = this.resolveTaskContext(task);
    return {
      id: task.id,
      title: task.title || '未命名就业指导',
      careerGoal: ctx.careerGoal,
      studentName: ctx.studentName,
      guidedAt: task.completedAt || task.creationTime,
      content: resultJson,
      parsed,
      fromTask: true,
      taskInputJson: task.inputJson,
    };
  }

  /**
   * 从任务 inputJson 还原上下文：职业目标直接可取；
   * 所属学生按简历附件/标题在已加载的学生简历里匹配（前端本地匹配，无需后端改动）。
   */
  private resolveTaskContext(task: AiGenerationTaskDto): { studentName: string; careerGoal?: string } {
    try {
      const input = JSON.parse(task.inputJson || '{}') as {
        resumeTitle?: string; attachmentUrl?: string; careerGoal?: string;
      };
      const careerGoal = (input.careerGoal || '').trim() || undefined;
      const title = (input.resumeTitle || '').trim();
      const url = (input.attachmentUrl || '').trim();
      let studentName = '';
      if (title || url) {
        const hit = this.students().find((s) =>
          (s.resumes || []).some((r) =>
            (!!url && (r.attachmentUrl || '') === url) ||
            (!!title && (r.title || '') === title)));
        studentName = hit?.studentName ?? '';
      }
      return { studentName, careerGoal };
    } catch {
      return { studentName: '' };
    }
  }

  /** 学生列表到达后，回填任务条目缺失的学生名/职业目标（列表先到、任务后到的竞态）。 */
  private fillTaskContexts(): void {
    if (this.students().length === 0) return;
    this.allRecords.update((list) => list.map((r) => {
      if (!r.fromTask || r.studentName || !r.taskInputJson) return r;
      const ctx = this.resolveTaskContext({ inputJson: r.taskInputJson } as AiGenerationTaskDto);
      if (!ctx.studentName && !ctx.careerGoal) return r;
      return { ...r, studentName: ctx.studentName || r.studentName, careerGoal: ctx.careerGoal ?? r.careerGoal };
    }));
  }

  openRecordDetail(record: ParsedRecord): void {
    this.previewItem.set(record);
  }

  closeRecordDetail(): void {
    this.previewItem.set(null);
  }

  backToList(): void {
    this.previewItem.set(null);
  }

  /**
   * 归一化职业匹配度分数（0-100 整数）。
   * AI 返回的可能是数字、字符串、或者 0-1 的小数，统一处理避免 NaN。
   */
  normalizeScore(value: unknown): number {
    if (value == null) return 0;
    let n = typeof value === 'number' ? value : parseFloat(String(value));
    if (!Number.isFinite(n)) return 0;
    // 如果 AI 给的是 0-1 小数（如 0.72），乘以 100
    if (n > 0 && n <= 1) n = n * 100;
    return Math.max(0, Math.min(100, Math.round(n)));
  }

  /** 将 StudentResumeDto 构建为 AI 提示用的文本 */
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

  generate() {
    const student = this.selectedStudent();
    const resume = this.selectedResume();
    if (!student || !resume) return;

    const resumeContent = this.buildResumeContent(resume);
    if (!resumeContent.trim()) {
      this.messageService.warning('该简历内容为空，无法生成');
      return;
    }

    this.isLoading.set(true);
    this.resetResult();

    const careerGoal = this.careerGoal();
    const payload = {
      resumeContent,
      resumeTitle: resume.title,
      careerGoal: careerGoal || undefined,
      attachmentUrl: resume.attachmentUrl || undefined,
    };

    this.cancelTaskPolling();
    this.aiTaskService
      .create({
        taskType: AiTaskType.CareerGuidance,
        title: resume.title ? `职业规划：${resume.title}` : '职业规划',
        inputJson: JSON.stringify(payload),
      })
      .subscribe({
        next: (task) => {
          this.currentTaskId.set(task.id);
          this.messageService.success('任务已提交后台生成，可切换页面，完成后会通知你');
          this.followTask(task.id);
        },
        error: (err) => {
          this.isLoading.set(false);
          this.messageService.error(err?.error?.error?.message || '提交任务失败，请重试');
        },
      });
  }

  // ---------- background task helpers ----------
  private followTask(taskId: string, onCompleted?: () => void) {
    this.cancelTaskPolling();
    this.taskPollSub = this.aiTaskNotifications
      .pollTask(taskId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (t) => {
          if (t.status === AiTaskStatus.Completed) {
            this.cancelTaskPolling();
            this.isLoading.set(false);
            // rawJson 必须同步保存，否则后台完成的结果无法下载 DOCX
            this.rawJson.set(t.resultJson || '');
            this.tryParseResult(t.resultJson || '', true);
            onCompleted?.();
          } else if (t.status === AiTaskStatus.Failed || t.status === AiTaskStatus.Cancelled) {
            this.cancelTaskPolling();
            this.isLoading.set(false);
            this.messageService.error(t.errorMessage || '就业指导生成失败，请稍后重试');
          }
        },
        error: () => {
          this.cancelTaskPolling();
          this.isLoading.set(false);
          this.messageService.error('就业指导生成失败，请稍后重试');
        },
      });
  }

  private cancelTaskPolling() {
    this.taskPollSub?.unsubscribe();
    this.taskPollSub = null;
  }

  private loadTaskPreview(taskId: string) {
    this.aiTaskService
      .get(taskId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (task) => this.openTaskResult(task),
        error: () => this.messageService.error('加载任务结果失败'),
      });
  }

  private openTaskResult(task: AiGenerationTaskDto) {
    // 与教案/案例分析一致：?taskId= 结果进「历史记录」Tab 的结果 UI 展示
    if (task.status === AiTaskStatus.Pending || task.status === AiTaskStatus.Running) {
      this.isLoading.set(true);
      this.currentTaskId.set(task.id);
      this.followTask(task.id, () => {
        const json = this.rawJson();
        if (json) this.previewTaskResult({ ...task, status: AiTaskStatus.Completed, resultJson: json });
      });
      return;
    }
    if (task.status === AiTaskStatus.Completed) {
      this.rawJson.set(task.resultJson || '');
      this.tryParseResult(task.resultJson || '', true);
      if (task.resultJson) this.previewTaskResult(task);
    }
  }

  /** 任务结果在历史 Tab 预览（合并进列表并选中展示）。 */
  private previewTaskResult(task: AiGenerationTaskDto): void {
    if (!task.resultJson) return;
    this.mergeTaskResult(task, task.resultJson);
    const record = this.allRecords().find((r) => r.id === task.id);
    if (record) {
      this.previewItem.set(record);
      this.activeTabIndex.set(1);
    }
  }

  private tryParseResult(json: string, final = false) {
    try {
      let cleanJson = json.trim();
      if (cleanJson.startsWith('```')) {
        const firstNewline = cleanJson.indexOf('\n');
        if (firstNewline >= 0) cleanJson = cleanJson.substring(firstNewline + 1);
        if (cleanJson.endsWith('```')) {
          cleanJson = cleanJson.substring(0, cleanJson.length - 3).trimEnd();
        }
      }
      const parsed = JSON.parse(cleanJson);
      if (parsed && typeof parsed === 'object' && parsed.error) {
        this.messageService.error(parsed.error);
        return;
      }
      this.result.set(parsed);
    } catch {
      if (final) {
        this.messageService.warning('AI 返回的数据格式不完整，请重新生成');
      }
    }
  }

  async downloadDocx() {
    const json = this.rawJson();
    if (!json) return;

    this.isExporting.set(true);
    try {
      const blob = await this.chatService.exportCareerGuidanceDocx(json);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `就业指导_${new Date().toISOString().slice(0, 10)}.docx`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      this.messageService.success('就业指导报告已下载');
    } catch (err) {
      console.error('Failed to export docx:', err);
      this.messageService.error('导出失败，请重试');
    } finally {
      this.isExporting.set(false);
    }
  }

  reset() {
    this.resetResult();
  }

  resetResult() {
    this.selectedResumeId.set(null);
    this.careerGoal.set('');
    this.result.set(null);
    this.rawJson.set('');
    this.savedRecordId.set(null);
  }

  /**
   * 把当前 AI 生成结果保存为所选学生的就业指导记录（管理端）。
   * - Content 存 raw JSON，前端展示时可解析还原结构化数据。
   * - StudentId 由所选学生决定，SourceType=AI，TeacherId=null。
   */
  saveToStudentGuidance() {
    if (this.isSaving() || this.savedRecordId()) return;
    const student = this.selectedStudent();
    const r = this.result();
    const rawJson = this.rawJson();
    if (!student || !r || !rawJson) {
      this.messageService.warning('请先生成就业指导');
      return;
    }
    this.isSaving.set(true);
    const title = (r.title || this.careerGoal() || '就业指导').trim();
    this.employmentService
      .createStudentCareerGuidanceRecord({
        studentId: student.studentId,
        title,
        content: rawJson,
        careerGoal: this.careerGoal() || undefined,
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (saved) => {
          this.isSaving.set(false);
          this.savedRecordId.set(saved.id);
          this.messageService.success('已保存到该学生的就业指导');
          // 历史缓存失效：下次切到历史 Tab 自动重载（含刚保存的这条）；
          // 若已在历史 Tab，直接刷新
          if (this.activeTabIndex() === 1) {
            this.loadAllRecords();
          } else {
            this.allRecordsLoaded.set(false);
          }
        },
        error: (err) => {
          this.isSaving.set(false);
          const detail =
            err?.error?.error?.message ||
            err?.error?.message ||
            err?.message ||
            '';
          this.messageService.error('保存失败：' + (detail || '未知错误'));
        },
      });
  }

  downloadHistoryDocx(record: ParsedRecord): void {
    if (!record.content) return;
    this.chatService.exportCareerGuidanceDocx(record.content)
      .then(blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${record.title}_${new Date().toISOString().slice(0, 10)}.docx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        this.messageService.success('就业指导报告已下载');
      })
      .catch(() => this.messageService.error('导出失败，请重试'));
  }

  getPriorityColor(priority: string): string {
    switch (priority) {
      case '高': return 'red';
      case '中': return 'orange';
      case '低': return 'green';
      default: return 'default';
    }
  }

  private tryParseAiResult(content: string | undefined): CareerGuidanceResult | null {
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
      if (parsed?.assessment) return parsed as CareerGuidanceResult;
      return null;
    } catch {
      return null;
    }
  }
}
