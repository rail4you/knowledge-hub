import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
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
import { EmploymentGuidanceSourceType, EmploymentService, EmploymentGuidanceRecordDto } from './employment.service';

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

interface GuidanceItemView {
  raw: EmploymentGuidanceRecordDto;
  isAi: boolean;
  ai?: GuidanceResult;
}

@Component({
  selector: 'app-my-guidance',
  standalone: true,
  imports: [
    CommonModule,
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
  ],
  templateUrl: './my-guidance.component.html',
  styleUrls: ['./my-guidance.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyGuidanceComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);

  readonly items = signal<EmploymentGuidanceRecordDto[]>([]);
  readonly sourceTypes = EmploymentGuidanceSourceType;
  readonly viewItems = computed<GuidanceItemView[]>(() =>
    this.items().map(raw => {
      const ai = this.tryParseAiResult(raw.content);
      return {
        raw,
        isAi: !!ai,
        ai: ai ?? undefined,
      };
    })
  );

  ngOnInit(): void {
    this.employmentService.getMyGuidanceRecordList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.items.set(result.items || []));
  }

  getSourceLabel(type: EmploymentGuidanceSourceType): string {
    return type === EmploymentGuidanceSourceType.AI ? 'AI' : '人工';
  }

  getPriorityColor(priority: string | undefined): string {
    switch (priority) {
      case '高': return 'red';
      case '中': return 'orange';
      case '低': return 'green';
      default: return 'default';
    }
  }

  /**
   * 解析 AI 生成的 Content（持久化为 raw JSON）。
   * 解析成功返回结构化结果；解析失败返回 null，按纯文本展示。
   */
  private tryParseAiResult(content: string | undefined): GuidanceResult | null {
    if (!content) return null;
    const trimmed = content.trim();
    if (!trimmed.startsWith('{') && !trimmed.startsWith('```')) return null;
    let json = trimmed;
    if (json.startsWith('```')) {
      const firstNewline = json.indexOf('\n');
      if (firstNewline >= 0) json = json.substring(firstNewline + 1);
      if (json.endsWith('```')) json = json.substring(0, json.length - 3).trimEnd();
    }
    try {
      const parsed = JSON.parse(json);
      // 简单校验：必须包含 assessment 才认作结构化 AI 结果
      if (parsed && typeof parsed === 'object' && parsed.assessment) {
        return parsed as GuidanceResult;
      }
      return null;
    } catch {
      return null;
    }
  }
}
