import { Component, signal, inject, computed, ChangeDetectionStrategy, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
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
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subject, takeUntil } from 'rxjs';
import { ChatService } from '../services/chat.service';
import { EmploymentService, StudentResumeDto } from '../../employment/employment.service';

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

@Component({
  selector: 'app-career-guidance',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
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
    NzEmptyModule
  ],
  templateUrl: './career-guidance.component.html',
  styleUrls: ['./career-guidance.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CareerGuidanceComponent implements OnInit, OnDestroy {
  private readonly chatService = inject(ChatService);
  private readonly employmentService = inject(EmploymentService);
  private readonly messageService = inject(NzMessageService);
  private readonly destroy$ = new Subject<void>();

  resumes = signal<StudentResumeDto[]>([]);
  resourcesLoading = signal(false);
  selectedResourceId = signal<string | null>(null);

  selectedResource = computed(() => {
    const id = this.selectedResourceId();
    if (!id) return null;
    return this.resumes().find(r => r.id === id) ?? null;
  });

  careerGoal = signal('');
  result = signal<CareerGuidanceResult | null>(null);
  rawJson = signal('');
  isLoading = signal(false);
  isExporting = signal(false);
  isSaving = signal(false);
  /** 已成功保存的数据库记录 ID，保存后展示「已保存」状态。 */
  savedRecordId = signal<string | null>(null);

  canGenerate = computed(() => {
    return !!this.selectedResourceId() && !this.isLoading();
  });

  ngOnInit() {
    this.loadResources();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadResources() {
    this.resourcesLoading.set(true);
    // 职业规划下拉列出当前用户的就业简历（StudentResume），简历由"我的简历"页面统一维护。
    this.employmentService.getMyResumeList()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.resumes.set(data || []);
          this.resourcesLoading.set(false);
        },
        error: (err) => {
          console.error('Failed to load resumes:', err);
          this.resourcesLoading.set(false);
          this.messageService.error('加载简历列表失败');
        }
      });
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
    const resume = this.selectedResource();
    if (!resume) return;

    const resumeContent = this.buildResumeContent(resume);
    if (!resumeContent.trim()) {
      this.messageService.warning('简历内容为空，请先在「我的简历」完善简历信息');
      return;
    }

    this.isLoading.set(true);
    this.result.set(null);
    this.rawJson.set('');
    this.savedRecordId.set(null);

    let fullResponse = '';

    this.chatService.generateCareerGuidance({
      resumeContent,
      resumeTitle: resume.title,
      careerGoal: this.careerGoal() || undefined,
      attachmentUrl: resume.attachmentUrl || undefined,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (chunk) => {
          if (chunk.content) {
            fullResponse += chunk.content;
            this.rawJson.set(fullResponse);
            this.tryParseResult(fullResponse);
          }
        },
        error: (err) => {
          console.error('Error generating career guidance:', err);
          this.isLoading.set(false);
          this.messageService.error('职业规划生成失败，请稍后重试');
        },
        complete: () => {
          this.isLoading.set(false);
          if (fullResponse && !this.result()) {
            this.tryParseResult(fullResponse, true);
          }
        }
      });
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
      // 后端错误信号：例如资源不存在、尚未提取页面内容等
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
      a.download = `职业规划_${new Date().toISOString().slice(0, 10)}.docx`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      this.messageService.success('职业规划报告已下载');
    } catch (err) {
      console.error('Failed to export docx:', err);
      this.messageService.error('导出失败，请重试');
    } finally {
      this.isExporting.set(false);
    }
  }

  reset() {
    this.selectedResourceId.set(null);
    this.careerGoal.set('');
    this.result.set(null);
    this.rawJson.set('');
    this.savedRecordId.set(null);
  }

  /**
   * 把当前 AI 生成结果持久化到「我的就业指导」。
   * - Content 存 raw JSON，前端展示时可解析还原结构化数据。
   * - Title 取自结果 title 或 careerGoal 兜底。
   * - StudentId/SourceType/TeacherId 由后端自动填。
   */
  saveToMyGuidance() {
    if (this.isSaving() || this.savedRecordId()) return;
    const r = this.result();
    const rawJson = this.rawJson();
    if (!r || !rawJson) {
      this.messageService.warning('请先生成职业规划');
      return;
    }
    this.isSaving.set(true);
    const title = (r.title || this.careerGoal() || 'AI 职业规划').trim();
    this.employmentService
      .createMyAIGuidanceRecord({
        title,
        content: rawJson,
        careerGoal: this.careerGoal() || undefined,
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (saved) => {
          this.isSaving.set(false);
          this.savedRecordId.set(saved.id);
          this.messageService.success('已保存到我的就业指导');
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

  getPriorityColor(priority: string): string {
    switch (priority) {
      case '高': return 'red';
      case '中': return 'orange';
      case '低': return 'green';
      default: return 'default';
    }
  }
}
