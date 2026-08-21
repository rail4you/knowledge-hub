import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
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
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subject, takeUntil } from 'rxjs';
import { EmploymentService } from './employment.service';
import { ChatService } from '../ai/services/chat.service';

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
    NzPopconfirmModule,
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

  // ============= 已保存的就业指导记录 =============
  readonly savedItems = signal<SavedGuidanceItem[]>([]);
  readonly savedLoading = signal(false);

  /** 当前展开查看的已保存记录 ID */
  readonly expandedId = signal<string | null>(null);

  readonly exporting = signal(false);

  ngOnInit(): void {
    this.loadSavedItems();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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
            title: r.title || '未命名就业指导',
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

  // ========== 查看已保存记录（展开） ==========
  toggleExpand(id: string): void {
    this.expandedId.set(this.expandedId() === id ? null : id);
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