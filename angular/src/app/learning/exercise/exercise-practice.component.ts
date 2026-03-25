import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzResultModule } from 'ng-zorro-antd/result';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import { ExerciseDto } from '../../proxy/exams/dtos';
import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';

interface AnswerItem {
  exerciseId: string;
  answer: string;
}

interface OptionItem {
  key: string;
  content: string;
}

@Component({
  selector: 'app-exercise-practice',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzCardModule,
    NzButtonModule,
    NzRadioModule,
    NzCheckboxModule,
    NzInputModule,
    NzTagModule,
    NzProgressModule,
    NzAlertModule,
    NzIconModule,
    NzResultModule
  ],
  template: `
    @if (loading()) {
      <nz-card nzTitle="加载中...">
        <p>正在加载习题...</p>
      </nz-card>
    } @else if (submitted()) {
      <nz-card nzTitle="答题结果">
        <nz-result 
          [nzStatus]="passed() ? 'success' : 'error'"
          [nzTitle]="passed() ? '恭喜通过！' : '未通过'"
          [nzSubTitle]="'得分：' + score() + '分'">
          <div nz-result-extra>
            <button nz-button nzType="default" (click)="retry()">重新答题</button>
            <button nz-button nzType="primary" [routerLink]="['/learning/course-detail', courseId()]">返回课程</button>
          </div>
        </nz-result>
        
        <div class="result-details">
          <h3>答题详情</h3>
          @for (item of exerciseResults(); track item.exercise.id) {
            <nz-card [nzTitle]="item.exercise.title" [nzSize]="'small'" class="result-card">
              <p><strong>题型：</strong>{{ getTypeName(item.exercise.type) }}</p>
              <p><strong>你的答案：</strong>{{ item.userAnswer || '未作答' }}</p>
              <p><strong>正确答案：</strong>{{ item.exercise.answer }}</p>
              <p><strong>结果：</strong>
                <nz-tag [nzColor]="item.isCorrect ? 'green' : 'red'">
                  {{ item.isCorrect ? '正确' : '错误' }}
                </nz-tag>
              </p>
              @if (item.exercise.answerExplanation) {
                <nz-alert nzType="info" [nzMessage]="item.exercise.answerExplanation" nzShowIcon></nz-alert>
              }
            </nz-card>
          }
        </div>
      </nz-card>
    } @else {
      <nz-card [nzTitle]="'练习 - ' + courseTitle()">
        <div class="progress-bar">
          <nz-progress [nzPercent]="progressPercent()" nzStatus="active"></nz-progress>
          <span class="progress-text">第 {{ currentIndex() + 1 }} / {{ exercises().length }} 题</span>
        </div>
        
        @if (currentExercise()) {
          <div class="exercise-card">
            <div class="exercise-header">
              <nz-tag [nzColor]="getTypeColor(currentExercise()!.type)">
                {{ getTypeName(currentExercise()!.type) }}
              </nz-tag>
              <nz-tag [nzColor]="getDifficultyColor(currentExercise()!.difficulty)">
                {{ getDifficultyName(currentExercise()!.difficulty) }}
              </nz-tag>
              <span class="score">{{ currentExercise()!.score }}分</span>
            </div>
            
            <h3 class="exercise-title">{{ currentExercise()!.title }}</h3>
            <p class="exercise-content">{{ currentExercise()!.questionContent }}</p>
            
            @if (currentExercise()!.type === 0) {
              <div class="options">
                <nz-radio-group [(ngModel)]="currentAnswer">
                  @for (opt of parseOptions(currentExercise()!); track opt.key) {
                    <label nz-radio [nzValue]="opt.key" class="option-item">
                      <span class="option-key">{{ opt.key }}.</span>
                      <span class="option-content">{{ opt.content }}</span>
                    </label>
                  }
                </nz-radio-group>
              </div>
            }
            
            @if (currentExercise()!.type === 1) {
              <div class="options">
                @for (opt of parseOptions(currentExercise()!); track opt.key) {
                  <label nz-checkbox [(ngModel)]="multiSelectedOptions[opt.key]" class="option-item">
                    <span class="option-key">{{ opt.key }}.</span>
                    <span class="option-content">{{ opt.content }}</span>
                  </label>
                }
              </div>
            }
            
            @if (currentExercise()!.type === 2) {
              <div class="options">
                <nz-radio-group [(ngModel)]="currentAnswer">
                  <label nz-radio nzValue="true" class="option-item">正确</label>
                  <label nz-radio nzValue="false" class="option-item">错误</label>
                </nz-radio-group>
              </div>
            }
            
            @if (currentExercise()!.type === 3 || currentExercise()!.type === 4 || currentExercise()!.type === 5) {
              <div class="options">
                <textarea nz-input [(ngModel)]="currentAnswer" placeholder="请输入答案" [nzAutosize]="{ minRows: 3, maxRows: 6 }" style="width: 100%"></textarea>
              </div>
            }
            
            @if (showExplanation()) {
              <nz-alert nzType="info" [nzMessage]="currentExercise()!.answerExplanation || ''" nzShowIcon class="explanation"></nz-alert>
            }
          </div>
        }
        
        <div class="actions">
          <button nz-button nzType="default" (click)="prev()" [disabled]="currentIndex() === 0">
            <span nz-icon nzType="left"></span>
            上一题
          </button>
          @if (currentIndex() < exercises().length - 1) {
            <button nz-button nzType="default" (click)="next()">
              下一题
              <span nz-icon nzType="right"></span>
            </button>
          } @else {
            <button nz-button nzType="primary" (click)="submit()">
              提交答案
              <span nz-icon nzType="check"></span>
            </button>
          }
        </div>
      </nz-card>
    }
  `,
  styles: [`
    .progress-bar {
      display: flex;
      align-items: center;
      gap: 16px;
      margin-bottom: 24px;
    }
    .progress-text {
      white-space: nowrap;
    }
    .exercise-card {
      margin-bottom: 24px;
    }
    .exercise-header {
      display: flex;
      gap: 8px;
      margin-bottom: 16px;
    }
    .score {
      margin-left: auto;
      color: #1890ff;
      font-weight: bold;
    }
    .exercise-title {
      font-size: 18px;
      margin-bottom: 12px;
    }
    .exercise-content {
      font-size: 16px;
      line-height: 1.8;
      margin-bottom: 24px;
      white-space: pre-wrap;
    }
    .options {
      margin-bottom: 24px;
    }
    .option-item {
      display: flex;
      align-items: flex-start;
      margin-bottom: 12px;
      font-size: 15px;
    }
    .option-key {
      font-weight: bold;
      margin-right: 8px;
      min-width: 20px;
    }
    .option-content {
      flex: 1;
    }
    .explanation {
      margin-top: 16px;
    }
    .actions {
      display: flex;
      justify-content: space-between;
      gap: 16px;
    }
    .result-details {
      margin-top: 24px;
    }
    .result-details h3 {
      margin-bottom: 16px;
    }
    .result-card {
      margin-bottom: 16px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExercisePracticeComponent implements OnInit {
  private readonly exerciseService = inject(ExerciseService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  
  exercises = signal<ExerciseDto[]>([]);
  loading = signal(true);
  submitted = signal(false);
  currentIndex = signal(0);
  currentAnswer = '';
  multiSelectedOptions: Record<string, boolean> = {};
  showExplanation = signal(false);
  score = signal(0);
  passed = signal(false);
  courseId = signal('');
  courseTitle = signal('课程练习');
  
  exerciseResults = signal<{ exercise: ExerciseDto; userAnswer: string; isCorrect: boolean }[]>([]);
  
  currentExercise = () => this.exercises()[this.currentIndex()];
  
  progressPercent = () => Math.round((this.currentIndex() + 1) / this.exercises().length * 100);
  
  ngOnInit() {
    const courseId = this.route.snapshot.paramMap.get('courseId');
    if (courseId) {
      this.courseId.set(courseId);
      this.loadExercises(courseId);
    } else {
      this.message.error('课程ID不存在');
      this.loading.set(false);
    }
  }
  
  loadExercises(courseId: string) {
    this.loading.set(true);
    this.exerciseService.getByCourse(courseId).subscribe({
      next: (exercises) => {
        this.exercises.set(exercises);
        this.loading.set(false);
        if (exercises.length === 0) {
          this.message.warning('该课程暂无习题');
        }
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载习题失败');
      }
    });
  }
  
  parseOptions(exercise: ExerciseDto): OptionItem[] {
    if (!exercise.options) return [];
    try {
      return JSON.parse(exercise.options);
    } catch {
      return [];
    }
  }
  
  next() {
    this.saveCurrentAnswer();
    if (this.currentIndex() < this.exercises().length - 1) {
      this.currentIndex.set(this.currentIndex() + 1);
      this.loadCurrentAnswer();
    }
  }
  
  prev() {
    this.saveCurrentAnswer();
    if (this.currentIndex() > 0) {
      this.currentIndex.set(this.currentIndex() - 1);
      this.loadCurrentAnswer();
    }
  }
  
  saveCurrentAnswer() {
    const exercise = this.currentExercise();
    if (!exercise) return;
    
    let answer = this.currentAnswer;
    if (exercise.type === ExerciseType.MultiChoice) {
      answer = Object.entries(this.multiSelectedOptions)
        .filter(([_, v]) => v)
        .map(([k, _]) => k)
        .join('');
    }
  }
  
  loadCurrentAnswer() {
    const exercise = this.currentExercise();
    if (!exercise) return;
    
    this.showExplanation.set(false);
    this.currentAnswer = '';
    this.multiSelectedOptions = {};
    
    if (exercise.type === ExerciseType.MultiChoice) {
      // Load previously selected options if any
    }
  }
  
  submit() {
    this.saveCurrentAnswer();
    
    let totalScore = 0;
    const results: { exercise: ExerciseDto; userAnswer: string; isCorrect: boolean }[] = [];
    
    for (const exercise of this.exercises()) {
      let userAnswer = '';
      if (exercise.type === ExerciseType.MultiChoice) {
        userAnswer = Object.entries(this.multiSelectedOptions)
          .filter(([_, v]) => v)
          .map(([k, _]) => k)
          .join('');
      } else {
        // Get answer from stored answers
        userAnswer = '';
      }
      
      const isCorrect = this.checkAnswer(exercise, userAnswer);
      if (isCorrect) {
        totalScore += exercise.score;
      }
      
      results.push({ exercise, userAnswer, isCorrect });
    }
    
    this.exerciseResults.set(results);
    this.score.set(totalScore);
    this.passed.set(totalScore >= 60);
    this.submitted.set(true);
  }
  
  checkAnswer(exercise: ExerciseDto, userAnswer: string): boolean {
    if (exercise.type === ExerciseType.SingleChoice || exercise.type === ExerciseType.MultiChoice) {
      return exercise.answer.toUpperCase() === userAnswer.toUpperCase();
    }
    if (exercise.type === ExerciseType.TrueFalse) {
      return exercise.answer === userAnswer;
    }
    // FillBlank, ShortAnswer, Essay - partial matching could be implemented
    return exercise.answer.includes(userAnswer) || userAnswer.includes(exercise.answer);
  }
  
  retry() {
    this.submitted.set(false);
    this.currentIndex.set(0);
    this.currentAnswer = '';
    this.multiSelectedOptions = {};
    this.showExplanation.set(false);
    this.score.set(0);
    this.passed.set(false);
    this.exerciseResults.set([]);
  }
  
  getTypeName(type: ExerciseType): string {
    const names = ['单选题', '多选题', '判断题', '填空题', '简答题', '论述题', '案例分析'];
    return names[type] || '未知';
  }
  
  getTypeColor(type: ExerciseType): string {
    const colors = ['blue', 'purple', 'cyan', 'orange', 'gold', 'red', 'green'];
    return colors[type] || 'default';
  }
  
  getDifficultyName(difficulty: number): string {
    const names = ['入门', '简单', '中等', '困难', '专家'];
    return names[difficulty - 1] || '未知';
  }
  
  getDifficultyColor(difficulty: number): string {
    const colors = ['green', 'lime', 'gold', 'orange', 'red'];
    return colors[difficulty - 1] || 'default';
  }
}
