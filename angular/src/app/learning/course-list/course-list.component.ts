import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';

@Component({
  selector: 'app-course-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzTagModule,
    NzSpinModule,
    NzEmptyModule,
    NzGridModule,
    NzIconModule
  ],
  templateUrl: './course-list.component.html',
  styleUrls: ['./course-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CourseListComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  
  courses = signal<CourseDto[]>([]);
  loading = signal(false);
  filterText = signal('');
  selectedMajor = signal<string | null>(null);
  selectedDifficulty = signal<number | null>(null);
  selectedSemester = signal<string | null>(null);
  
  majors = signal<string[]>([]);
  semesters = signal<string[]>([]);
  
  difficulties = [
    { label: '入门', value: 1 },
    { label: '初级', value: 2 },
    { label: '中级', value: 3 },
    { label: '高级', value: 4 }
  ];
  
  ngOnInit() {
    this.loadFilterOptions();
    this.loadCourses();
  }

  loadFilterOptions() {
    this.courseService.getMajors().subscribe({
      next: (majors) => this.majors.set(majors || [])
    });
    this.courseService.getSemesters().subscribe({
      next: (semesters) => this.semesters.set(semesters || [])
    });
  }
  
  loadCourses() {
    this.loading.set(true);
    this.courseService.getPublished({
      filter: this.filterText() || undefined,
      major: this.selectedMajor() || undefined,
      semester: this.selectedSemester() || undefined,
      difficulty: this.selectedDifficulty() || undefined,
      maxResultCount: 100,
      skipCount: 0
    } as any).subscribe({
      next: (result) => {
        this.courses.set(result.items || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }
  
  getDifficultyColor(difficulty?: number): string {
    const colors: Record<number, string> = {
      1: '#52c41a',
      2: '#73d13d',
      3: '#faad14',
      4: '#fa8c16'
    };
    return colors[difficulty || 1] || '#d9d9d9';
  }
  
  getDifficultyLabel(difficulty?: number): string {
    return this.difficulties.find(d => d.value === difficulty)?.label || '入门';
  }
  
  viewCourse(id?: string) {
    if (id) {
      this.router.navigate(['/learning/course-detail', id]);
    }
  }
  
  enrollCourse(id?: string) {
    if (!id) return;
    this.courseService.enroll(id).subscribe({
      next: () => {
        this.message.success('选课成功');
        this.loadCourses();
      },
      error: (err) => {
        this.message.error(err?.message || '选课失败');
      }
    });
  }
}