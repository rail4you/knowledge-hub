import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
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
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { EmploymentService, EmploymentGuidanceRecordDto, GetEmploymentGuidanceRecordsInput } from '../../employment/employment.service';
import { ChatService } from '../../ai/services/chat.service';

// ---- 与 my-guidance.component 共享的解析类型 ----
interface GuidanceAssessment {
  careerMatchScore: number;
  strengths: string[];
  areasForImprovement: string[];
  summary: string;
  educationBackground?: { school: string; degree: string; major: string; period: string }[];
  workExperience?: { company: string; position: string; period: string; description: string }[];
}
interface GuidanceRecommendedPath {
  title: string; description: string; matchScore: number;
  requiredSkills: string[]; salaryRange: string; growthPotential: string;
}
interface GuidanceSkillGap {
  skill: string; currentLevel: string; targetLevel: string; priority: string;
}
interface GuidanceActionItem {
  id: string; title: string; description: string; timeline: string; priority: string;
}
interface GuidanceResult {
  title: string;
  assessment: GuidanceAssessment;
  recommendedPaths: GuidanceRecommendedPath[];
  skillGaps: GuidanceSkillGap[];
  actionPlan: GuidanceActionItem[];
  nextSteps: string[];
}

interface ParsedRecord {
  id: string;
  title: string;
  careerGoal?: string;
  guidedAt: string;
  content: string;
  parsed: GuidanceResult | null;
  _studentId: string;
  _studentName: string;
}

interface StudentGroup {
  studentId: string;
  studentName: string;
  records: ParsedRecord[];
}

@Component({
  selector: 'app-admin-employment-guidance',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    NzButtonModule, NzCardModule, NzTagModule,
    NzDescriptionsModule, NzProgressModule, NzCollapseModule,
    NzTableModule, NzTimelineModule, NzIconModule,
    NzEmptyModule, NzSpinModule, NzInputModule, NzDividerModule,
  ],
  templateUrl: './admin-employment-guidance.component.html',
  styleUrls: ['./admin-employment-guidance.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminEmploymentGuidanceComponent implements OnInit, OnDestroy {
  private readonly employmentService = inject(EmploymentService);
  private readonly chatService = inject(ChatService);
  private readonly message = inject(NzMessageService);
  private readonly destroy$ = new Subject<void>();

  readonly loading = signal(false);
  readonly allRecords = signal<ParsedRecord[]>([]);
  readonly searchText = signal('');

  readonly expandedStudentId = signal<string | null>(null);
  readonly expandedRecordId = signal<string | null>(null);

  /** 按学生分组，支持搜索过滤 */
  readonly groups = computed<StudentGroup[]>(() => {
    const records = this.allRecords();
    const search = this.searchText().trim().toLowerCase();
    const map = new Map<string, StudentGroup>();
    for (const r of records) {
      const key = r._studentId || 'unknown';
      const sname = r._studentName || '未知学生';
      if (!map.has(key)) {
        map.set(key, { studentId: key, studentName: sname, records: [] });
      }
      const group = map.get(key)!;
      const match = !search
        || group.studentName.toLowerCase().includes(search)
        || r.title.toLowerCase().includes(search)
        || (r.careerGoal && r.careerGoal.toLowerCase().includes(search));
      if (match) {
        group.records.push(r);
      }
    }
    return Array.from(map.values())
      .filter(g => g.records.length > 0)
      .sort((a, b) => a.studentName.localeCompare(b.studentName));
  });

  ngOnInit(): void {
    this.loadAll();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAll(): void {
    this.loading.set(true);
    const input: GetEmploymentGuidanceRecordsInput = {
      skipCount: 0,
      maxResultCount: 500,
    };
    this.employmentService.getGuidanceRecordList(input)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => {
          const records: ParsedRecord[] = (result.items || []).map(r => ({
            id: r.id!,
            title: r.title || '未命名规划',
            careerGoal: r.careerGoal,
            guidedAt: r.guidedAt!,
            content: r.content || '',
            parsed: this.tryParseAiResult(r.content),
            _studentId: r.studentId,
            _studentName: r.studentName || '未知学生',
          }));
          this.allRecords.set(records);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载就业指导列表失败');
        },
      });
  }

  toggleStudent(studentId: string): void {
    this.expandedStudentId.set(this.expandedStudentId() === studentId ? null : studentId);
    this.expandedRecordId.set(null);
  }

  toggleRecord(recordId: string): void {
    this.expandedRecordId.set(this.expandedRecordId() === recordId ? null : recordId);
  }

  downloadDocx(record: ParsedRecord): void {
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
        this.message.success('职业规划报告已下载');
      })
      .catch(() => this.message.error('导出失败，请重试'));
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
