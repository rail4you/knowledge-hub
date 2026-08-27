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
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subject, takeUntil } from 'rxjs';
import { ChatService } from '../services/chat.service';
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
    NzEmptyModule,
    NzTabsModule
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
  readonly expandedAllRecordId = signal<string | null>(null);

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
  }

  ngOnDestroy() {
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

  /** Tab 切换到「就业指导历史记录」时懒加载全租户记录 */
  onTabChange(index: number): void {
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
          this.allRecords.set(records);
          this.allRecordsLoaded.set(true);
          this.allRecordsLoading.set(false);
        },
        error: () => {
          this.allRecordsLoading.set(false);
          this.messageService.error('加载就业指导历史记录失败');
        },
      });
  }

  toggleAllRecord(recordId: string): void {
    this.expandedAllRecordId.set(this.expandedAllRecordId() === recordId ? null : recordId);
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
          this.messageService.error('就业指导生成失败，请稍后重试');
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
