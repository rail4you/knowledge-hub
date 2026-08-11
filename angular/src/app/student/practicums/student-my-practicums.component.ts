import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzMessageService } from 'ng-zorro-antd/message';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import {
  PracticumEnrollmentDto,
  PracticumGuidanceRecordDto,
  PracticumTimelineItemDto,
} from '../../proxy/practicums/dtos/models';
import { PracticumEnrollmentStatus } from '../../proxy/practicums/enums/practicum-enrollment-status.enum';

@Component({
  selector: 'app-student-my-practicums',
  standalone: true,
  imports: [
    CommonModule, DatePipe, RouterModule,
    NzButtonModule, NzIconModule, NzSpinModule, NzProgressModule, NzTagModule, NzEmptyModule, NzModalModule,
  ],
  templateUrl: './student-my-practicums.component.html',
  styleUrls: ['./student-my-practicums.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyPracticumsComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly items = signal<PracticumEnrollmentDto[]>([]);
  readonly loading = signal(true);

  readonly guidanceVisible = signal(false);
  readonly guidanceLoading = signal(false);
  readonly guidanceItems = signal<PracticumGuidanceRecordDto[]>([]);
  guidanceTitle = '';

  readonly timelineVisible = signal(false);
  readonly timelineLoading = signal(false);
  readonly timelineItems = signal<PracticumTimelineItemDto[]>([]);
  timelineTitle = '';

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.practicumService.getMyEnrollments().subscribe({
      next: items => {
        this.items.set(items || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载我的实训失败');
      },
    });
  }

  openDetail(item: PracticumEnrollmentDto): void {
    this.router.navigate(['/student/practicums', item.projectId]);
  }

  openGuidance(item: PracticumEnrollmentDto): void {
    this.guidanceTitle = `${item.projectTitle || '实训项目'} · 指导记录`;
    this.guidanceVisible.set(true);
    this.guidanceLoading.set(true);
    this.guidanceItems.set([]);
    this.practicumService.getGuidanceList(item.id).subscribe({
      next: list => {
        this.guidanceItems.set(list || []);
        this.guidanceLoading.set(false);
      },
      error: () => {
        this.guidanceLoading.set(false);
        this.message.error('加载指导记录失败');
      },
    });
  }

  openTimeline(item: PracticumEnrollmentDto): void {
    this.timelineTitle = `${item.projectTitle || '实训项目'} · 过程追溯`;
    this.timelineVisible.set(true);
    this.timelineLoading.set(true);
    this.timelineItems.set([]);
    this.practicumService.getTimeline(item.id).subscribe({
      next: list => {
        this.timelineItems.set(list || []);
        this.timelineLoading.set(false);
      },
      error: () => {
        this.timelineLoading.set(false);
        this.message.error('加载过程记录失败');
      },
    });
  }

  getStatusLabel(status?: PracticumEnrollmentStatus): string {
    const labels: Record<number, string> = {
      [PracticumEnrollmentStatus.Enrolled]: '已参与',
      [PracticumEnrollmentStatus.InProgress]: '进行中',
      [PracticumEnrollmentStatus.Submitted]: '待评阅',
      [PracticumEnrollmentStatus.Reviewed]: '已评阅',
      [PracticumEnrollmentStatus.Completed]: '已完成',
      [PracticumEnrollmentStatus.Cancelled]: '已取消',
    };
    return labels[status ?? -1] || '未知';
  }

  getStatusColor(status?: PracticumEnrollmentStatus): string {
    switch (status) {
      case PracticumEnrollmentStatus.InProgress: return 'processing';
      case PracticumEnrollmentStatus.Submitted: return 'warning';
      case PracticumEnrollmentStatus.Reviewed: return 'cyan';
      case PracticumEnrollmentStatus.Completed: return 'success';
      case PracticumEnrollmentStatus.Cancelled: return 'default';
      default: return 'blue';
    }
  }

  hasScore(item: PracticumEnrollmentDto): boolean {
    return item.finalScore != null;
  }

  hasMetadata(item: PracticumTimelineItemDto, key: string): boolean {
    return item.metadata?.[key] != null;
  }

  timelineTypeLabel(type?: string): string {
    switch (type) {
      case 'Enrollment': return '参与实训';
      case 'Submission': return '提交成果';
      case 'Guidance': return '教师指导';
      case 'Assessment': return '教师评分';
      default: return type || '';
    }
  }

  timelineTypeColor(type?: string): string {
    switch (type) {
      case 'Submission': return 'blue';
      case 'Guidance': return 'green';
      case 'Assessment': return 'gold';
      default: return 'default';
    }
  }
}
