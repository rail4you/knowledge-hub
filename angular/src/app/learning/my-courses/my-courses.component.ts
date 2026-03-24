import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';

@Component({
  selector: 'app-my-courses',
  standalone: true,
  imports: [
    CommonModule,
    NzCardModule,
    NzButtonModule,
    NzProgressModule,
    NzTagModule,
    NzSpinModule,
    NzEmptyModule,
    NzGridModule,
    NzIconModule
  ],
  templateUrl: './my-courses.component.html',
  styleUrls: ['./my-courses.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MyCoursesComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly router = inject(Router);
  
  courses = signal<CourseDto[]>([]);
  loading = signal(false);

  ngOnInit() {
    this.loadCourses();
  }
  
  loadCourses() {
    this.loading.set(true);
    this.courseService.getMyCourses({ maxResultCount: 100 } as any).subscribe({
      next: (result) => {
        this.courses.set(result.items || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }
  
  getDifficultyLabel(difficulty?: number): string {
    switch (difficulty) {
      case 1: return '初级';
      case 2: return '中级';
      case 3: return '高级';
      default: return '初级';
    }
  }
  
  getDifficultyColor(difficulty?: number): string {
    switch (difficulty) {
      case 1: return 'green';
      case 2: return 'orange';
      case 3: return 'red';
      default: return 'green';
    }
  }

  continueLearning(courseId: string) {
    this.router.navigate(['/learning/course-detail', courseId]);
  }
  
  viewDetail(courseId: string) {
    this.router.navigate(['/learning/course-detail', courseId]);
  }
}