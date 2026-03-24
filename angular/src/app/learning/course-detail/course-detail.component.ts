import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzListModule } from 'ng-zorro-antd/list';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { ChapterTreeComponent } from '../chapter/chapter-tree.component';

interface Chapter {
  id: string;
  title: string;
  sortOrder: number;
  knowledgeResources: KnowledgeResource[];
}

interface KnowledgeResource {
  id: string;
  name: string;
  description?: string;
  importanceLevel: number;
  difficulty: number;
}

interface CourseDetail {
  id: string;
  title: string;
  description?: string;
  coverImageUrl?: string;
  major?: string;
  semester?: string;
  credits?: number;
  difficulty: number;
  teacherName?: string;
  chapters: Chapter[];
  studentCount: number;
  isEnrolled: boolean;
  progress?: number;
}

@Component({
  selector: 'app-course-detail',
  standalone: true,
  imports: [
    CommonModule,
    NzCardModule,
    NzButtonModule,
    NzSpinModule,
    NzTagModule,
    NzIconModule,
    NzProgressModule,
    NzListModule,
    NzCollapseModule,
    NzDividerModule,
    ChapterTreeComponent
  ],
  templateUrl: './course-detail.component.html',
  styleUrls: ['./course-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CourseDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  
  course = signal<CourseDetail | null>(null);
  loading = signal(true);
  courseId = signal<string>('');
  
  ngOnInit() {
    const courseId = this.route.snapshot.paramMap.get('id');
    if (courseId) {
      this.courseId.set(courseId);
      this.loadCourse(courseId);
    }
  }
  
  loadCourse(id: string) {
    this.loading.set(true);
    setTimeout(() => {
      this.course.set({
        id,
        title: 'Python 编程基础',
        description: '从零开始学习 Python 编程，涵盖基础语法、数据结构、面向对象编程等核心内容。',
        major: '计算机科学',
        semester: '2024春季',
        credits: 3,
        difficulty: 1,
        teacherName: '张教授',
        studentCount: 156,
        isEnrolled: true,
        progress: 35,
        chapters: [
          {
            id: '1',
            title: '第一章：Python 简介',
            sortOrder: 1,
            knowledgeResources: [
              { id: '1-1', name: 'Python 历史与发展', importanceLevel: 2, difficulty: 1 },
              { id: '1-2', name: '环境搭建', importanceLevel: 3, difficulty: 1 }
            ]
          },
          {
            id: '2',
            title: '第二章：基础语法',
            sortOrder: 2,
            knowledgeResources: [
              { id: '2-1', name: '变量与数据类型', importanceLevel: 3, difficulty: 2 },
              { id: '2-2', name: '运算符', importanceLevel: 2, difficulty: 2 },
              { id: '2-3', name: '流程控制', importanceLevel: 3, difficulty: 2 }
            ]
          }
        ]
      });
      this.loading.set(false);
    }, 500);
  }
  
  getDifficultyColor(difficulty: number): string {
    const colors = ['green', 'lime', 'gold', 'orange', 'red'];
    return colors[difficulty - 1] || 'default';
  }
  
  getDifficultyLabel(difficulty: number): string {
    const labels = ['入门', '初级', '中级', '高级', '专家'];
    return labels[difficulty - 1] || '未知';
  }
  
  goBack() {
    this.router.navigate(['/learning/courses']);
  }
  
  startLearning() {
    const course = this.course();
    if (course) {
      this.router.navigate(['/learning/courses', course.id, 'learn']);
    }
  }
  
  viewKnowledgeGraph() {
    const course = this.course();
    if (course) {
      this.router.navigate(['/learning/knowledge-graph', course.id]);
    }
  }

  viewExercises() {
    const course = this.course();
    if (course) {
      this.router.navigate(['/learning/exercise', course.id]);
    }
  }
  
  enrollCourse() {
    console.log('Enrolling...');
  }
}
