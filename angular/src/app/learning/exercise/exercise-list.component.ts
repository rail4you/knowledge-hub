import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import { ExerciseDto } from '../../proxy/exams/dtos';
import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';
import { ExerciseFormComponent } from './exercise-form.component';

@Component({
  selector: 'app-exercise-list',
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
    NzBreadCrumbModule,
    NzDropDownModule
  ],
  template: `
    <nz-card [nzTitle]="cardTitle" [nzExtra]="extraTemplate">
      <nz-breadcrumb>
        <nz-breadcrumb-item>
          <a [routerLink]="['/learning/course-list']">课程列表</a>
        </nz-breadcrumb-item>
        <nz-breadcrumb-item>习题管理</nz-breadcrumb-item>
      </nz-breadcrumb>
      
      <div class="filters">
        <nz-select [(ngModel)]="selectedType" (ngModelChange)="filterByType()" nzPlaceHolder="选择题型" nzAllowClear style="width: 150px">
          <nz-option [nzValue]="null" nzLabel="全部题型"></nz-option>
          <nz-option [nzValue]="0" nzLabel="单选题"></nz-option>
          <nz-option [nzValue]="1" nzLabel="多选题"></nz-option>
          <nz-option [nzValue]="2" nzLabel="判断题"></nz-option>
          <nz-option [nzValue]="3" nzLabel="填空题"></nz-option>
          <nz-option [nzValue]="4" nzLabel="简答题"></nz-option>
        </nz-select>
        <button nz-button nzType="primary" (click)="openForm()">
          <span nz-icon nzType="plus"></span>
          创建习题
        </button>
      </div>
      
      <nz-table 
        [nzData]="exercises()" 
        [nzLoading]="loading()"
        [nzPageSize]="10"
        nzShowSizeChanger>
        <thead>
          <tr>
            <th>标题</th>
            <th>题型</th>
            <th>难度</th>
            <th>分值</th>
            <th>创建时间</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          @for (exercise of exercises(); track exercise.id) {
            <tr>
              <td>{{ exercise.title }}</td>
              <td>
                <nz-tag [nzColor]="getTypeColor(exercise.type)">
                  {{ getTypeName(exercise.type) }}
                </nz-tag>
              </td>
              <td>
                <nz-tag [nzColor]="getDifficultyColor(exercise.difficulty)">
                  {{ getDifficultyName(exercise.difficulty) }}
                </nz-tag>
              </td>
              <td>{{ exercise.score }}分</td>
              <td>{{ exercise.creationTime | date:'yyyy-MM-dd' }}</td>
              <td>
                <button nz-button nzType="link" nz-dropdown [nzDropdownMenu]="menu">
                  <span nz-icon nzType="more"></span>
                </button>
                <nz-dropdown-menu #menu="nzDropdownMenu">
                  <ul nz-menu>
                    <li nz-menu-item (click)="openForm(exercise)">编辑</li>
                    <li nz-menu-item (click)="preview(exercise)">预览</li>
                    <li nz-menu-item nzDanger (click)="delete(exercise)">删除</li>
                  </ul>
                </nz-dropdown-menu>
              </td>
            </tr>
          }
        </tbody>
      </nz-table>
    </nz-card>
    
    <ng-template #cardTitle>
      <span nz-icon nzType="profile"></span>
      习题管理 - {{ courseTitle() }}
    </ng-template>
    
    <ng-template #extraTemplate>
      <button nz-button nzType="default" [routerLink]="['/learning/course-detail', courseId()]">
        返回课程
      </button>
    </ng-template>
  `,
  styles: [`
    .filters {
      display: flex;
      gap: 16px;
      margin-bottom: 16px;
      justify-content: space-between;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExerciseListComponent implements OnInit {
  private readonly exerciseService = inject(ExerciseService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  
  exercises = signal<ExerciseDto[]>([]);
  loading = signal(true);
  selectedType: ExerciseType | null = null;
  courseId = signal<string>('');
  courseTitle = signal<string>('');
  
  ngOnInit() {
    const courseId = this.route.snapshot.paramMap.get('courseId');
    if (courseId) {
      this.courseId.set(courseId);
      this.loadCourseTitle();
      this.loadExercises();
    }
  }
  
  loadCourseTitle() {
    this.courseTitle.set('课程习题');
  }
  
  loadExercises() {
    this.loading.set(true);
    const courseId = this.courseId();
    
    if (this.selectedType !== null) {
      this.exerciseService.getList({ 
        courseId, 
        type: this.selectedType,
        maxResultCount: 100 
      } as any).subscribe({
        next: (res) => {
          this.exercises.set(res.items || []);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载习题失败');
        }
      });
    } else {
      this.exerciseService.getByCourse(courseId).subscribe({
        next: (exercises) => {
          this.exercises.set(exercises);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载习题失败');
        }
      });
    }
  }
  
  filterByType() {
    this.loadExercises();
  }
  
  openForm(exercise?: ExerciseDto) {
    // TODO: Implement form modal
    this.message.info('表单功能开发中...');
  }
  
  preview(exercise: ExerciseDto) {
    this.message.info('预览功能开发中...');
  }
  
  delete(exercise: ExerciseDto) {
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
