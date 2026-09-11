import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzTimelineModule } from 'ng-zorro-antd/timeline';
import { Subject, takeUntil } from 'rxjs';
import { EmploymentService } from '../../employment/employment.service';
import { ChatService } from '../../ai/services/chat.service';
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
    CommonModule, DatePipe,
    NzIconModule, NzSpinModule, NzEmptyModule, NzTableModule,
    NzModalModule, NzButtonModule, NzProgressModule, NzCollapseModule,
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
  private readonly message = inject(NzMessageService);
  private readonly cache = inject(ClientCacheService);
  private readonly destroy$ = new Subject<void>();

  readonly items = signal<GuidanceListItem[]>([]);
  readonly loading = signal(false);
  readonly exporting = signal(false);

  /** 当前预览的记录（null = 关闭预览） */
  readonly previewItem = signal<GuidanceListItem | null>(null);

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
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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
