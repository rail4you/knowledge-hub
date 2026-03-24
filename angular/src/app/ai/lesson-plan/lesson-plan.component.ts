import { Component, signal, inject, ChangeDetectionStrategy, OnDestroy } from '@angular/core';
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
import { Subject, takeUntil } from 'rxjs';
import { ChatService } from '../services/chat.service';

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
    NzSpinModule
  ],
  templateUrl: './lesson-plan.component.html',
  styleUrls: ['./lesson-plan.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LessonPlanComponent implements OnDestroy {
  private readonly chatService = inject(ChatService);
  private readonly destroy$ = new Subject<void>();
  
  input = signal<LessonPlanInput>({
    topic: '',
    subject: '',
    grade: '',
    duration: 45
  });
  
  result = signal<LessonPlanResult | null>(null);
  isLoading = signal(false);
  
  generate() {
    const input = this.input();
    if (!input.topic.trim()) return;
    
    this.isLoading.set(true);
    this.result.set(null);
    
    const prompt = this.buildPrompt();
    let fullResponse = '';
    
    this.chatService.chat({ message: prompt })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (chunk) => {
          if (chunk.content) {
            fullResponse += chunk.content;
            try {
              const parsed = JSON.parse(fullResponse);
              this.result.set(parsed);
            } catch {
              // Not yet complete JSON
            }
          }
        },
        error: (err) => {
          console.error('Error generating lesson plan:', err);
          this.isLoading.set(false);
        },
        complete: () => {
          this.isLoading.set(false);
        }
      });
  }
  
  private buildPrompt(): string {
    const input = this.input();
    return `请根据以下信息生成一份详细的教案：

课程主题: ${input.topic}
学科: ${input.subject || '通用'}
年级: ${input.grade || '全部'}
课时: ${input.duration}分钟

请以JSON格式输出教案，包含以下字段:
- title: 教案标题
- subject: 学科
- grade: 年级
- duration: 总时长
- objectives: 教学目标数组
- keyPoints: 教学重点数组
- difficulties: 教学难点数组
- sections: 教学环节数组,每个环节包含 name, duration, content, activities
- methods: 教学方法数组
- resources: 教学资源数组
- assessment: 评估方法数组
- homework: 课后作业数组

只输出JSON,不要其他内容。`;
  }
  
  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
