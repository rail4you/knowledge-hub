import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTimelineModule } from 'ng-zorro-antd/timeline';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { Subject, takeUntil } from 'rxjs';
import { EmploymentService, EmploymentGuidanceRecordDto, StudentResumeDto } from './employment.service';
import { ChatService } from '../ai/services/chat.service';

interface GuidanceAssessment {
  careerMatchScore: number;
  strengths: string[];
  areasForImprovement: string[];
  summary: string;
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

interface SavedGuidanceItem {
  id: string;
  title: string;
  guidedAt: string;
  careerGoal?: string;
  content: string;
  /** 解析后的结构化结果，用于预览 */
  parsed?: GuidanceResult | null;
}

@Component({
  selector: 'app-my-guidance',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NzButtonModule,
    NzCardModule,
    NzTagModule,
    NzDescriptionsModule,
    NzProgressModule,
    NzCollapseModule,
    NzTableModule,
    NzTimelineModule,
    NzIconModule,
    NzEmptyModule,
    NzSpinModule,
    NzSelectModule,
    NzInputModule,
    NzPopconfirmModule,
    NzDividerModule,
  ],
  templateUrl: './my-guidance.component.html',
  styleUrls: ['./my-guidance.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyGuidanceComponent implements OnInit, OnDestroy {
  private readonly employmentService = inject(EmploymentService);
  private readonly chatService = inject(ChatService);
  private readonly message = inject(NzMessageService);
  private readonly destroy$ = new Subject<void>();

  // ============= 生成区域 =============
  /** 学生简历列表 */
  readonly resumes = signal<StudentResumeDto[]>([]);
  readonly resumesLoading = signal(false);
  readonly selectedResumeId = signal<string | null>(null);
  readonly careerGoal = signal('');
  readonly generating = signal(false);
  readonly exporting = signal(false);
  readonly saving = signal(false);

  readonly selectedResume = computed(() => {
    const id = this.selectedResumeId();
    if (!id) return null;
    return this.resumes().find(r => r.id === id) ?? null;
  });

  /** 当前生成/预览中的结果 */
  readonly currentResult = signal<GuidanceResult | null>(null);
  readonly currentRawJson = signal('');
  readonly currentTitle = computed(() =>
    this.currentResult()?.title || this.careerGoal() || 'AI 职业规划'
  );

  readonly canGenerate = computed(() =>
    !!this.selectedResumeId() && !this.generating()
  );

  // ============= 已保存列表 =============
  readonly savedItems = signal<SavedGuidanceItem[]>([]);
  readonly savedLoading = signal(false);

  /** 当前展开查看的已保存记录 ID */
  readonly expandedId = signal<string | null>(null);

  // ============= 视图状态 =============
  /** 生成区域是否折叠（当在查看已保存记录时） */
  readonly generatorCollapsed = computed(() => false); // 始终展开

  ngOnInit(): void {
    this.loadResumes();
    this.loadSavedItems();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ========== 加载简历 ==========
  private loadResumes(): void {
    this.resumesLoading.set(true);
    this.employmentService.getMyResumeList()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: data => {
          this.resumes.set(data || []);
          this.resumesLoading.set(false);
        },
        error: () => {
          this.resumesLoading.set(false);
          this.message.error('加载简历列表失败');
        },
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

  // ========== 加载已保存的指导 ==========
  loadSavedItems(): void {
    this.savedLoading.set(true);
    this.employmentService.getMyGuidanceRecordList({ skipCount: 0, maxResultCount: 100 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => {
          const items: SavedGuidanceItem[] = (result.items || []).map(r => ({
            id: r.id!,
            title: r.title || '未命名规划',
            guidedAt: r.guidedAt!,
            careerGoal: r.careerGoal,
            content: r.content || '',
            parsed: this.tryParseAiResult(r.content),
          }));
          this.savedItems.set(items);
          this.savedLoading.set(false);
        },
        error: () => {
          this.savedLoading.set(false);
        },
      });
  }

  // ========== 生成 ==========
  generate(): void {
    const resume = this.selectedResume();
    if (!resume) return;

    const resumeContent = this.buildResumeContent(resume);
    if (!resumeContent.trim()) {
      this.message.warning('简历内容为空，请先完善简历信息');
      return;
    }

    this.generating.set(true);
    this.currentResult.set(null);
    this.currentRawJson.set('');

    let fullResponse = '';

    this.chatService.generateCareerGuidance({
      resumeContent,
      resumeTitle: resume.title,
      careerGoal: this.careerGoal() || undefined,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: chunk => {
          if (chunk.content) {
            fullResponse += chunk.content;
            this.currentRawJson.set(fullResponse);
            this.tryParseResult(fullResponse);
          }
        },
        error: err => {
          console.error('Career guidance generation error:', err);
          this.generating.set(false);
          this.message.error('职业规划生成失败，请稍后重试');
        },
        complete: () => {
          this.generating.set(false);
          if (fullResponse && !this.currentResult()) {
            this.tryParseResult(fullResponse, true);
          }
          // 自动保存
          if (fullResponse && this.currentResult()) {
            this.autoSave();
          }
        },
      });
  }

  private tryParseResult(json: string, final = false): void {
    try {
      let clean = json.trim();
      if (clean.startsWith('```')) {
        const nl = clean.indexOf('\n');
        if (nl >= 0) clean = clean.substring(nl + 1);
        if (clean.endsWith('```')) clean = clean.substring(0, clean.length - 3).trimEnd();
      }
      const parsed = JSON.parse(clean);
      if (parsed?.assessment) {
        this.currentResult.set(parsed);
      }
    } catch {
      if (final) this.message.warning('AI 返回数据格式不完整');
    }
  }

  // ========== 自动保存 ==========
  private autoSave(): void {
    const json = this.currentRawJson();
    const result = this.currentResult();
    if (!json || !result) return;

    this.saving.set(true);
    const title = this.currentTitle();

    this.employmentService.createMyAIGuidanceRecord({
      title,
      content: json,
      careerGoal: this.careerGoal() || undefined,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.loadSavedItems();
        },
        error: err => {
          this.saving.set(false);
          const detail = err?.error?.error?.message || err?.error?.message || err?.message || '';
          this.message.error('保存失败：' + (detail || '未知错误'));
        },
      });
  }

  // ========== 下载 DOCX ==========
  downloadDocx(): void {
    const json = this.currentRawJson();
    if (!json) return;

    this.exporting.set(true);
    this.chatService.exportCareerGuidanceDocx(json)
      .then(blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `职业规划_${new Date().toISOString().slice(0, 10)}.docx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        this.message.success('职业规划报告已下载');
        this.exporting.set(false);
      })
      .catch(() => {
        this.exporting.set(false);
        this.message.error('导出失败，请重试');
      });
  }

  // ========== 下载已保存记录的 DOCX ==========
  downloadSavedDocx(item: SavedGuidanceItem): void {
    if (!item.content) return;
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
      })
      .catch(() => {
        this.exporting.set(false);
        this.message.error('导出失败，请重试');
      });
  }

  // ========== 删除 ==========
  deleteItem(id: string): void {
    this.employmentService.deleteMyGuidanceRecord(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.message.success('已删除');
          if (this.expandedId() === id) this.expandedId.set(null);
          this.savedItems.update(items => items.filter(i => i.id !== id));
        },
        error: () => this.message.error('删除失败'),
      });
  }

  // ========== 重置生成区域 ==========
  reset(): void {
    this.selectedResumeId.set(null);
    this.careerGoal.set('');
    this.currentResult.set(null);
    this.currentRawJson.set('');
  }

  // ========== 查看已保存记录（展开） ==========
  toggleExpand(id: string): void {
    this.expandedId.set(this.expandedId() === id ? null : id);
    // 展开时关闭生成结果
    if (this.expandedId() === id) {
      this.reset();
    }
  }

  // ========== 工具方法 ==========
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
