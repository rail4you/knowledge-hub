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
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subject, takeUntil } from 'rxjs';
import { ChatService, ResourceForChat } from '../services/chat.service';

interface CareerGuidanceResult {
  title: string;
  assessment: {
    careerMatchScore: number;
    strengths: string[];
    areasForImprovement: string[];
    summary: string;
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
  private readonly messageService = inject(NzMessageService);
  private readonly destroy$ = new Subject<void>();

  resources = signal<ResourceForChat[]>([]);
  resourcesLoading = signal(false);
  selectedResourceId = signal<string | null>(null);

  selectedResource = computed(() => {
    const id = this.selectedResourceId();
    if (!id) return null;
    return this.resources().find(r => r.id === id) ?? null;
  });

  careerGoal = signal('');
  result = signal<CareerGuidanceResult | null>(null);
  rawJson = signal('');
  isLoading = signal(false);
  isExporting = signal(false);

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
    this.chatService.getResources()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.resources.set(data);
          this.resourcesLoading.set(false);
        },
        error: (err) => {
          console.error('Failed to load resources:', err);
          this.resourcesLoading.set(false);
          this.messageService.error('加载资源列表失败');
        }
      });
  }

  generate() {
    const resourceId = this.selectedResourceId();
    if (!resourceId) return;

    this.isLoading.set(true);
    this.result.set(null);
    this.rawJson.set('');

    let fullResponse = '';

    this.chatService.generateCareerGuidance({
      resourceId,
      careerGoal: this.careerGoal() || undefined
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
