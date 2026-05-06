import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTableModule } from 'ng-zorro-antd/table';
import {
  PracticumEnrollmentDto,
  PracticumEnrollmentStatus,
  PracticumService,
  PracticumTimelineItemDto,
} from './practicum.service';

@Component({
  selector: 'app-my-practicum',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzCardModule, NzModalModule, NzProgressModule, NzTableModule],
  templateUrl: './my-practicum.component.html',
  styleUrls: ['./my-practicum.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyPracticumComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);

  readonly items = signal<PracticumEnrollmentDto[]>([]);
  readonly timeline = signal<PracticumTimelineItemDto[]>([]);
  readonly statuses = PracticumEnrollmentStatus;

  timelineVisible = false;
  timelineTitle = '';

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.practicumService.getMyEnrollments().subscribe(items => this.items.set(items || []));
  }

  openTimeline(item: PracticumEnrollmentDto): void {
    this.timelineTitle = item.projectTitle || '实训时间线';
    this.practicumService.getTimeline(item.id).subscribe(result => {
      this.timeline.set(result || []);
      this.timelineVisible = true;
    });
  }

  getStatusLabel(status: PracticumEnrollmentStatus): string {
    const labels: Record<number, string> = {
      [PracticumEnrollmentStatus.Enrolled]: '已参与',
      [PracticumEnrollmentStatus.InProgress]: '进行中',
      [PracticumEnrollmentStatus.Submitted]: '待评阅',
      [PracticumEnrollmentStatus.Reviewed]: '已评阅',
      [PracticumEnrollmentStatus.Completed]: '已完成',
      [PracticumEnrollmentStatus.Cancelled]: '已取消',
    };
    return labels[status] || '未知';
  }
}
