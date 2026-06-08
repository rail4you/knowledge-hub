import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MicroMajorService } from '../../proxy/micro-majors/micro-major.service';
import type { MicroMajorDetailDto, MicroMajorCourseDto, MicroMajorResourceDto } from '../../proxy/micro-majors/dtos/models';

@Component({
  selector: 'app-student-micro-major-detail',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, RouterModule,
    NzButtonModule, NzIconModule, NzSpinModule, NzProgressModule, NzTabsModule,
  ],
  templateUrl: './student-micro-major-detail.component.html',
  styleUrls: ['./student-micro-major-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMicroMajorDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly microMajorService = inject(MicroMajorService);
  private readonly message = inject(NzMessageService);

  readonly detail = signal<MicroMajorDetailDto | null>(null);
  readonly resources = signal<MicroMajorResourceDto[]>([]);
  readonly loading = signal(true);
  readonly activeTab = signal<'courses' | 'resources'>('courses');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.loadDetail(id);
    this.loadResources(id);
  }

  loadDetail(id: string): void {
    this.loading.set(true);
    this.microMajorService.getDetail(id).subscribe({
      next: result => { this.detail.set(result); this.loading.set(false); },
      error: () => { this.loading.set(false); this.message.error('加载微专业详情失败'); },
    });
  }

  loadResources(id: string): void {
    this.microMajorService.getResources(id).subscribe({
      next: result => this.resources.set(result || []),
      error: () => {},
    });
  }

  enroll(): void {
    const id = this.detail()?.id;
    if (!id) return;
    this.microMajorService.enroll(id).subscribe({
      next: () => { this.message.success('报名成功'); this.loadDetail(id); },
      error: () => this.message.error('报名失败'),
    });
  }

  coverGradient(course: MicroMajorCourseDto): string {
    const palettes = [
      '#2563eb',
      '#1e6ce8',
      '#0891b2',
      '#0284c7',
    ];
    const key = course.courseTitle || course.courseId || '';
    let hash = 0;
    for (let i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) | 0;
    return palettes[Math.abs(hash) % palettes.length];
  }
}
