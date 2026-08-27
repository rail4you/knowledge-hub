import { Component, OnInit, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { RecruitmentLiveService } from '../../recruitment-live/recruitment-live.service';
import { RecruitmentLiveDto, RecruitmentLiveStatus } from '../../recruitment-live/recruitment-live.models';

@Component({
  selector: 'app-student-recruitment-live',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
  ],
  templateUrl: './student-recruitment-live.component.html',
  styleUrls: ['./student-recruitment-live.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentRecruitmentLiveComponent implements OnInit {
  private liveService = inject(RecruitmentLiveService);
  private router = inject(Router);
  private message = inject(NzMessageService);

  loading = signal(false);
  lives = signal<RecruitmentLiveDto[]>([]);
  activeCount = computed(() => this.lives().filter(l => l.status === RecruitmentLiveStatus.Active).length);

  ngOnInit() {
    this.loadLives();
  }

  loadLives() {
    this.loading.set(true);
    this.liveService.getStudentLives({
      skipCount: 0,
      maxResultCount: 50,
    }).subscribe({
      next: (res) => {
        this.lives.set(res.items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载直播列表失败');
      },
    });
  }

  enterLive(live: RecruitmentLiveDto) {
    this.router.navigate(['/student/recruitment-live', live.id]);
  }

  statusText(status: RecruitmentLiveStatus): string {
    switch (status) {
      case RecruitmentLiveStatus.Waiting: return '等待中';
      case RecruitmentLiveStatus.Active: return '进行中';
      case RecruitmentLiveStatus.Ended: return '已结束';
      case RecruitmentLiveStatus.Cancelled: return '已取消';
      default: return '未知';
    }
  }

  canEnter(live: RecruitmentLiveDto): boolean {
    // 仅在时间范围内（未到计划结束时间）可进入，避免已过期直播仍可进入
    if (live.status !== RecruitmentLiveStatus.Waiting && live.status !== RecruitmentLiveStatus.Active)
      return false;
    if (live.scheduledEndAt && new Date(live.scheduledEndAt) < new Date()) return false;
    return true;
  }

  /** 格式化计划时间范围，如“2026-08-01 09:00 ~ 12:00” */
  scheduleText(live: RecruitmentLiveDto): string {
    const pad = (n: number) => (n < 10 ? `0${n}` : `${n}`);
    if (!live.scheduledAt) return '';
    const start = new Date(live.scheduledAt);
    const s = `${start.getFullYear()}-${pad(start.getMonth() + 1)}-${pad(start.getDate())} ${pad(start.getHours())}:${pad(start.getMinutes())}`;
    if (!live.scheduledEndAt) return s;
    const end = new Date(live.scheduledEndAt);
    return `${s} ~ ${pad(end.getHours())}:${pad(end.getMinutes())}`;
  }
}
