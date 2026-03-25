import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzPageHeaderModule } from 'ng-zorro-antd/page-header';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import { CreateUpdateExerciseDto, ExerciseDto } from '../../proxy/exams/dtos';
import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';

@Component({
  selector: 'app-exercise-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzCardModule,
    NzButtonModule,
    NzTableModule,
    NzSelectModule,
    NzTagModule,
    NzIconModule,
    NzModalModule,
    NzInputModule,
    NzPageHeaderModule,
    NzDropDownModule,
    NzBreadCrumbModule
  ],
  template: `
    <nz-card nzTitle="习题管理">
      <nz-breadcrumb>
        <nz-breadcrumb-item>
          <a nz-icon nzType="home" [routerLink]="['/']"></a>
        </nz-breadcrumb-item>
        <nz-breadcrumb-item>管理端</nz-breadcrumb-item>
        <nz-breadcrumb-item>习题管理</nz-breadcrumb-item>
      </nz-breadcrumb>
      
      <div class="filters">
        <nz-select [(ngModel)]="selectedType" (ngModelChange)="loadExercises()" nzPlaceHolder="选择题型" nzAllowClear style="width: 150px">
          <nz-option [nzValue]="null" nzLabel="全部题型"></nz-option>
          <nz-option [nzValue]="0" nzLabel="单选题"></nz-option>
          <nz-option [nzValue]="1" nzLabel="多选题"></nz-option>
          <nz-option [nzValue]="2" nzLabel="判断题"></nz-option>
          <nz-option [nzValue]="3" nzLabel="填空题"></nz-option>
          <nz-option [nzValue]="4" nzLabel="简答题"></nz-option>
        </nz-select>
        <input nz-input [(ngModel)]="searchText" (ngModelChange)="loadExercises()" placeholder="搜索标题" style="width: 200px" />
        <button nz-button nzType="primary" (click)="showForm = true">
          <span nz-icon nzType="plus"></span>
          创建习题
        </button>
      </div>
      
      <nz-table 
        [nzData]="exercises()" 
        [nzLoading]="loading()"
        [nzPageSize]="20"
        nzShowSizeChanger
        nzShowPagination>
        <thead>
          <tr>
            <th>ID</th>
            <th>标题</th>
            <th>题型</th>
            <th>难度</th>
            <th>分值</th>
            <th>课程ID</th>
            <th>创建时间</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          @for (exercise of exercises(); track exercise.id) {
            <tr>
              <td>{{ exercise.id?.substring(0, 8) }}...</td>
              <td>{{ exercise.title }}</td>
              <td>
                <nz-tag [nzColor]="getTypeColor(exercise.type!)">
                  {{ getTypeName(exercise.type!) }}
                </nz-tag>
              </td>
              <td>
                <nz-tag [nzColor]="getDifficultyColor(exercise.difficulty!)">
                  {{ getDifficultyName(exercise.difficulty!) }}
                </nz-tag>
              </td>
              <td>{{ exercise.score }}分</td>
              <td>{{ exercise.courseId?.substring(0, 8) }}...</td>
              <td>{{ exercise.creationTime | date:'yyyy-MM-dd' }}</td>
              <td>
                <button nz-button nzType="link" (click)="editExercise(exercise)">编辑</button>
                <button nz-button nzType="link" nzDanger (click)="deleteExercise(exercise)">删除</button>
              </td>
            </tr>
          }
        </tbody>
      </nz-table>
    </nz-card>
    
    @if (showForm) {
      <nz-modal 
        [(nzVisible)]="showForm" 
        [nzTitle]="isEditing ? '编辑习题' : '创建习题'" 
        [nzWidth]="700"
        (nzOnCancel)="closeForm()"
        (nzOnOk)="saveExercise()">
        <ng-container *nzModalContent>
          <nz-form-item>
            <nz-form-label [nzSpan]="4" nzRequired>课程ID</nz-form-label>
            <nz-form-control [nzSpan]="18">
              <input nz-input [(ngModel)]="formData.courseId" placeholder="课程ID" />
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
              <nz-select [(ngModel)]="formData.type" style="width: 200px">
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
          
          <nz-form-item>
            <nz-form-label [nzSpan]="4" nzRequired>答案</nz-form-label>
            <nz-form-control [nzSpan]="18">
              <input nz-input [(ngModel)]="formData.answer" placeholder="答案" />
            </nz-form-control>
          </nz-form-item>
          
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
        </ng-container>
      </nz-modal>
    }
  `,
  styles: [`
    .filters {
      display: flex;
      gap: 16px;
      margin-bottom: 16px;
      align-items: center;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExerciseManagementComponent implements OnInit {
  private readonly exerciseService = inject(ExerciseService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  
  exercises = signal<ExerciseDto[]>([]);
  loading = signal(true);
  selectedType: ExerciseType | null = null;
  searchText = '';
  showForm = false;
  isEditing = false;
  editingId = '';
  
  formData: CreateUpdateExerciseDto = this.createEmptyForm();
  
  ngOnInit() {
    this.loadExercises();
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
  
  loadExercises() {
    this.loading.set(true);
    this.exerciseService.getList({ 
      maxResultCount: 100,
      type: this.selectedType ?? undefined
    } as any).subscribe({
      next: (res) => {
        let items = res.items || [];
        if (this.searchText) {
          items = items.filter(e => e.title?.toLowerCase().includes(this.searchText.toLowerCase()));
        }
        this.exercises.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载习题失败');
      }
    });
  }
  
  editExercise(exercise: ExerciseDto) {
    this.isEditing = true;
    this.editingId = exercise.id!;
    this.formData = {
      courseId: exercise.courseId,
      title: exercise.title,
      questionContent: exercise.questionContent,
      type: exercise.type,
      answer: exercise.answer,
      answerExplanation: exercise.answerExplanation,
      difficulty: exercise.difficulty,
      score: exercise.score
    };
    this.showForm = true;
  }
  
  closeForm() {
    this.showForm = false;
    this.isEditing = false;
    this.editingId = '';
    this.formData = this.createEmptyForm();
  }
  
  saveExercise() {
    if (!this.formData.title || !this.formData.questionContent || !this.formData.answer) {
      this.message.warning('请填写必填项');
      return;
    }
    
    const request = this.isEditing
      ? this.exerciseService.update(this.editingId, this.formData)
      : this.exerciseService.create(this.formData);
    
    request.subscribe({
      next: () => {
        this.message.success(this.isEditing ? '习题更新成功' : '习题创建成功');
        this.closeForm();
        this.loadExercises();
      },
      error: () => {
        this.message.error('保存失败');
      }
    });
  }
  
  deleteExercise(exercise: ExerciseDto) {
    this.message.info('删除功能开发中...');
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
