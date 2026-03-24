import { Component, Input, Output, EventEmitter, signal, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import { CreateUpdateExerciseDto, ExerciseDto } from '../../proxy/exams/dtos/exercise.dto';
import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';

interface OptionItem {
  key: string;
  content: string;
  isCorrect: boolean;
}

@Component({
  selector: 'app-exercise-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzFormModule,
    NzInputModule,
    NzSelectModule,
    NzButtonModule,
    NzInputNumberModule,
    NzRadioModule,
    NzCheckboxModule,
    NzSwitchModule
  ],
  template: `
    <nz-card [nzTitle]="isEdit ? '编辑习题' : '创建习题'">
      <nz-form-item>
        <nz-form-label [nzSpan]="4" nzRequired>课程ID</nz-form-label>
        <nz-form-control [nzSpan]="18">
          <input nz-input [(ngModel)]="formData.courseId" placeholder="课程ID" />
        </nz-form-control>
      </nz-form-item>
      
      <nz-form-item>
        <nz-form-label [nzSpan]="4" nzRequired>章节ID</nz-form-label>
        <nz-form-control [nzSpan]="18">
          <input nz-input [(ngModel)]="formData.chapterId" placeholder="章节ID（可选）" />
        </nz-form-control>
      </nz-form-item>
      
      <nz-form-item>
        <nz-form-label [nzSpan]="4" nzRequired>标题</nz-form-label>
        <nz-form-control [nzSpan]="18">
          <input nz-input [(ngModel)]="formData.title" placeholder="习题标题" />
        </nz-form-control>
      </nz-form-item>
      
      <nz-form-item>
        <nz-form-label [nzSpan]="4" nzRequired>题型</nz-form-label>
        <nz-form-control [nzSpan]="18">
          <nz-select [(ngModel)]="formData.type" (ngModelChange)="onTypeChange()" style="width: 200px">
            <nz-option [nzValue]="0" nzLabel="单选题"></nz-option>
            <nz-option [nzValue]="1" nzLabel="多选题"></nz-option>
            <nz-option [nzValue]="2" nzLabel="判断题"></nz-option>
            <nz-option [nzValue]="3" nzLabel="填空题"></nz-option>
            <nz-option [nzValue]="4" nzLabel="简答题"></nz-option>
          </nz-select>
        </nz-form-control>
      </nz-form-item>
      
      <nz-form-item>
        <nz-form-label [nzSpan]="4" nzRequired>题目内容</nz-form-label>
        <nz-form-control [nzSpan]="18">
          <textarea nz-input [(ngModel)]="formData.questionContent" placeholder="请输入题目内容" [nzAutosize]="{ minRows: 3, maxRows: 6 }"></textarea>
        </nz-form-control>
      </nz-form-item>
      
      @if (formData.type === 0 || formData.type === 1) {
        <nz-form-item>
          <nz-form-label [nzSpan]="4" nzRequired>选项</nz-form-label>
          <nz-form-control [nzSpan]="18">
            <div class="options-list">
              @for (opt of options; track opt.key; let i = $index) {
                <div class="option-item">
                  <nz-radio [(ngModel)]="opt.isCorrect" [nzValue]="opt.key" (ngModelChange)="onCorrectChange(opt.key, $event)">
                    <input nz-input [(ngModel)]="opt.content" placeholder="选项内容" style="width: 300px" />
                  </nz-radio>
                  @if (formData.type === 1) {
                    <label nz-checkbox [(ngModel)]="opt.isCorrect" (ngModelChange)="onCorrectChange(opt.key, $event)">正确答案</label>
                  }
                  @if (options.length > 2) {
                    <button nz-button nzType="link" nzDanger (click)="removeOption(i)">删除</button>
                  }
                </div>
              }
              @if (options.length < 6) {
                <button nz-button nzType="link" (click)="addOption()">+ 添加选项</button>
              }
            </div>
          </nz-form-control>
        </nz-form-item>
      }
      
      @if (formData.type === 2) {
        <nz-form-item>
          <nz-form-label [nzSpan]="4" nzRequired>答案</nz-form-label>
          <nz-form-control [nzSpan]="18">
            <nz-radio-group [(ngModel)]="trueFalseAnswer">
              <label nz-radio [nzValue]="true">正确</label>
              <label nz-radio [nzValue]="false">错误</label>
            </nz-radio-group>
          </nz-form-control>
        </nz-form-item>
      }
      
      @if (formData.type === 3) {
        <nz-form-item>
          <nz-form-label [nzSpan]="4" nzRequired>答案</nz-form-label>
          <nz-form-control [nzSpan]="18">
            <input nz-input [(ngModel)]="formData.answer" placeholder="填空题答案" />
          </nz-form-control>
        </nz-form-item>
      }
      
      @if (formData.type === 4 || formData.type === 5) {
        <nz-form-item>
          <nz-form-label [nzSpan]="4" nzRequired>答案</nz-form-label>
          <nz-form-control [nzSpan]="18">
            <textarea nz-input [(ngModel)]="formData.answer" placeholder="简答/论述题答案要点" [nzAutosize]="{ minRows: 4, maxRows: 8 }"></textarea>
          </nz-form-control>
        </nz-form-item>
      }
      
      <nz-form-item>
        <nz-form-label [nzSpan]="4">答案解析</nz-form-label>
        <nz-form-control [nzSpan]="18">
          <textarea nz-input [(ngModel)]="formData.answerExplanation" placeholder="答案解析（可选）" [nzAutosize]="{ minRows: 2, maxRows: 4 }"></textarea>
        </nz-form-control>
      </nz-form-item>
      
      <nz-form-item>
        <nz-form-label [nzSpan]="4">难度</nz-form-label>
        <nz-form-control [nzSpan]="18">
          <nz-select [(ngModel)]="formData.difficulty" style="width: 150px">
            <nz-option [nzValue]="1" nzLabel="入门"></nz-option>
            <nz-option [nzValue]="2" nzLabel="简单"></nz-option>
            <nz-option [nzValue]="3" nzLabel="中等"></nz-option>
            <nz-option [nzValue]="4" nzLabel="困难"></nz-option>
            <nz-option [nzValue]="5" nzLabel="专家"></nz-option>
          </nz-select>
        </nz-form-control>
      </nz-form-item>
      
      <nz-form-item>
        <nz-form-label [nzSpan]="4">分值</nz-form-label>
        <nz-form-control [nzSpan]="18">
          <nz-input-number [(ngModel)]="formData.score" [nzMin]="1" [nzMax]="100" [nzStep]="1"></nz-input-number>
        </nz-form-control>
      </nz-form-item>
      
      <nz-form-item>
        <nz-form-control [nzSpan]="18" [nzOffset]="4">
          <button nz-button nzType="default" (click)="cancel()">取消</button>
          <button nz-button nzType="primary" (click)="submit()" style="margin-left: 8px">保存</button>
        </nz-form-control>
      </nz-form-item>
    </nz-card>
  `,
  styles: [`
    .options-list {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .option-item {
      display: flex;
      align-items: center;
      gap: 12px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExerciseFormComponent {
  @Input() exercise?: ExerciseDto;
  @Input() courseId?: string;
  @Output() saved = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();
  
  private readonly exerciseService = inject(ExerciseService);
  private readonly message = inject(NzMessageService);
  
  isEdit = false;
  formData: CreateUpdateExerciseDto = this.createEmptyForm();
  options: OptionItem[] = [
    { key: 'A', content: '', isCorrect: false },
    { key: 'B', content: '', isCorrect: false },
    { key: 'C', content: '', isCorrect: false },
    { key: 'D', content: '', isCorrect: false }
  ];
  trueFalseAnswer = true;
  
  constructor() {
    if (this.exercise) {
      this.isEdit = true;
      this.formData = {
        courseId: this.exercise.courseId,
        chapterId: this.exercise.chapterId,
        title: this.exercise.title,
        questionContent: this.exercise.questionContent,
        type: this.exercise.type,
        options: this.exercise.options,
        answer: this.exercise.answer,
        answerExplanation: this.exercise.answerExplanation,
        difficulty: this.exercise.difficulty,
        score: this.exercise.score
      };
      
      if (this.exercise.options) {
        try {
          const opts = JSON.parse(this.exercise.options);
          this.options = opts;
        } catch {}
      }
    } else if (this.courseId) {
      this.formData.courseId = this.courseId;
    }
  }
  
  createEmptyForm(): CreateUpdateExerciseDto {
    return {
      courseId: '',
      title: '',
      questionContent: '',
      type: ExerciseType.SingleChoice,
      answer: '',
      difficulty: 1,
      score: 5
    };
  }
  
  onTypeChange() {
    if (this.formData.type === ExerciseType.TrueFalse) {
      this.trueFalseAnswer = this.formData.answer === 'true';
    }
  }
  
  onCorrectChange(key: string, isCorrect: boolean) {
    if (this.formData.type === ExerciseType.SingleChoice && isCorrect) {
      this.options.forEach(o => o.isCorrect = o.key === key);
    }
  }
  
  addOption() {
    const keys = 'EFGHI'.split('');
    const newKey = keys[this.options.length - 4];
    if (newKey) {
      this.options.push({ key: newKey, content: '', isCorrect: false });
    }
  }
  
  removeOption(index: number) {
    this.options.splice(index, 1);
  }
  
  submit() {
    if (!this.formData.title || !this.formData.questionContent) {
      this.message.warning('请填写标题和题目内容');
      return;
    }
    
    if (this.formData.type === 0 || this.formData.type === 1) {
      const correctOptions = this.options.filter(o => o.isCorrect);
      if (correctOptions.length === 0) {
        this.message.warning('请选择正确答案');
        return;
      }
      this.formData.options = JSON.stringify(this.options);
      this.formData.answer = correctOptions.map(o => o.key).join('');
    }
    
    if (this.formData.type === ExerciseType.TrueFalse) {
      this.formData.answer = this.trueFalseAnswer ? 'true' : 'false';
    }
    
    const request = this.isEdit 
      ? this.exerciseService.update(this.exercise!.id, this.formData)
      : this.exerciseService.create(this.formData);
    
    request.subscribe({
      next: () => {
        this.message.success(this.isEdit ? '习题更新成功' : '习题创建成功');
        this.saved.emit();
      },
      error: () => {
        this.message.error('保存失败');
      }
    });
  }
  
  cancel() {
    this.cancelled.emit();
  }
}
