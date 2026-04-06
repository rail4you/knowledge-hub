import { Component, signal, inject, computed, ChangeDetectionStrategy, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subject, takeUntil } from 'rxjs';
import { ChatService, ResourceForChat } from '../services/chat.service';

interface LessonPlanInput {
  topic: string;
  subject: string;
  grade: string;
  duration: number;
}

interface TeachingSection {
  name: string;
  duration: number;
  content: string;
  activities: string[];
}

interface LessonPlanResult {
  title: string;
  subject: string;
  grade: string;
  duration: number;
  objectives: string[];
  keyPoints: string[];
  difficulties: string[];
  sections: TeachingSection[];
  methods: string[];
  resources: string[];
  assessment: string[];
  homework: string[];
}

@Component({
  selector: 'app-lesson-plan',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzInputModule,
    NzButtonModule,
    NzCardModule,
    NzFormModule,
    NzSelectModule,
    NzInputNumberModule,
    NzDividerModule,
    NzCollapseModule,
    NzDescriptionsModule,
    NzTagModule,
    NzSpinModule,
    NzIconModule
  ],
  templateUrl: './lesson-plan.component.html',
  styleUrls: ['./lesson-plan.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LessonPlanComponent implements OnInit, OnDestroy {
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

  input = signal<LessonPlanInput>({
    topic: '',
    subject: '',
    grade: '',
    duration: 45
  });

  result = signal<LessonPlanResult | null>(null);
  rawJson = signal('');
  isLoading = signal(false);
  isExporting = signal(false);

  canGenerate = computed(() => {
    const i = this.input();
    return !!this.selectedResourceId() && i.topic.trim().length > 0 && !this.isLoading();
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

  updateTopic(value: string) {
    this.input.update(v => ({ ...v, topic: value }));
  }

  updateSubject(value: string) {
    this.input.update(v => ({ ...v, subject: value }));
  }

  updateGrade(value: string) {
    this.input.update(v => ({ ...v, grade: value }));
  }

  updateDuration(value: number) {
    this.input.update(v => ({ ...v, duration: value }));
  }

  generate() {
    const input = this.input();
    const resourceId = this.selectedResourceId();
    if (!resourceId || !input.topic.trim()) return;

    this.isLoading.set(true);
    this.result.set(null);
    this.rawJson.set('');

    let fullResponse = '';

    this.chatService.generateLessonPlan({
      resourceId,
      topic: input.topic,
      subject: input.subject || undefined,
      grade: input.grade || undefined,
      duration: input.duration
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
          console.error('Error generating lesson plan:', err);
          this.isLoading.set(false);
          this.messageService.error('教案生成失败，请稍后重试');
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
      const blob = await this.chatService.exportLessonPlanDocx(json);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `教案_${new Date().toISOString().slice(0, 10)}.docx`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      this.messageService.success('教案已下载');
    } catch (err) {
      console.error('Failed to export docx:', err);
      this.messageService.error('导出失败，请重试');
    } finally {
      this.isExporting.set(false);
    }
  }

  reset() {
    this.selectedResourceId.set(null);
    this.input.set({ topic: '', subject: '', grade: '', duration: 45 });
    this.result.set(null);
    this.rawJson.set('');
  }
}
