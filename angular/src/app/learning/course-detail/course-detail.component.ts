import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import type { CourseDetailDto, ChapterDto } from '../../proxy/courses/dtos/models';

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
    NzTooltipModule,
    NzEmptyModule,
  ],
  templateUrl: './course-detail.component.html',
  styleUrls: ['./course-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CourseDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly courseService = inject(CourseService);
  private readonly chapterService = inject(ChapterService);

  course = signal<CourseDetailDto | null>(null);
  loading = signal(true);
  courseId = signal<string>('');
  isStudentView = signal(false);
  chapters = signal<ChapterDto[]>([]);
  chaptersLoading = signal(true);
  expandedNodes = signal<Set<string>>(new Set());

  ngOnInit() {
    this.isStudentView.set(this.router.url.startsWith('/student'));
    const courseId = this.route.snapshot.paramMap.get('id');
    if (courseId) {
      this.courseId.set(courseId);
      this.loadCourse(courseId);
      this.loadChapters(courseId);
    }
  }

  loadChapters(courseId: string) {
    this.chaptersLoading.set(true);
    this.chapterService.getChapterTree(courseId).subscribe({
      next: (data) => {
        this.chapters.set(data);
        this.chaptersLoading.set(false);
      },
      error: () => {
        this.chaptersLoading.set(false);
      }
    });
  }

  isExpanded(nodeId: string): boolean {
    return this.expandedNodes().has(nodeId);
  }

  toggleNode(nodeId: string) {
    this.expandedNodes.update(set => {
      const newSet = new Set(set);
      if (newSet.has(nodeId)) {
        newSet.delete(nodeId);
      } else {
        newSet.add(nodeId);
      }
      return newSet;
    });
  }

  loadCourse(id: string) {
    this.loading.set(true);
    this.courseService.getDetail(id).subscribe({
      next: (result) => {
        this.course.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
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
    this.router.navigate([this.isStudentView() ? '/student/courses' : '/learning/my-courses']);
  }
  
  startLearning() {
    const course = this.course();
    if (course) {
      this.router.navigate([
        this.isStudentView() ? '/student/exercise-learning' : '/learning/exercise-learning',
        course.id,
      ]);
    }
  }
  
  viewKnowledgeGraph() {
    const course = this.course();
    if (course) {
      const basePath = this.isStudentView() ? '/student/knowledge-graph' : '/learning/knowledge-graph';
      this.router.navigate([basePath, course.id]);
    }
  }

  viewExercises() {
    if (this.isStudentView()) {
      return;
    }

    const course = this.course();
    if (course) {
      this.router.navigate(['/learning/exercise', course.id]);
    }
  }

  startExerciseLearning() {
    const course = this.course();
    if (course) {
      this.router.navigate([
        this.isStudentView() ? '/student/exercise-learning' : '/learning/exercise-learning',
        course.id,
      ]);
    }
  }
  
  enrollCourse() {
    console.log('Enrolling...');
  }
}
