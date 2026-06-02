import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { RecruitmentLiveService } from '../../recruitment-live/recruitment-live.service';
import { RecruitmentLiveDto, RecruitmentLiveStatus } from '../../recruitment-live/recruitment-live.models';

@Component({
  selector: 'app-student-recruitment-live',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzCardModule,
    NzButtonModule,
    NzTagModule,
    NzIconModule,
    NzEmptyModule,
    NzSpinModule,
  ],
  templateUrl: './student-recruitment-live.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentRecruitmentLiveComponent implements OnInit {
  private liveService = inject(RecruitmentLiveService);
  private router = inject(Router);
  private message = inject(NzMessageService);

  loading = signal(false);
  lives = signal<RecruitmentLiveDto[]>([]);

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

  statusColor(status: RecruitmentLiveStatus): string {
    switch (status) {
      case RecruitmentLiveStatus.Waiting: return 'gold';
      case RecruitmentLiveStatus.Active: return 'green';
      case RecruitmentLiveStatus.Ended: return 'default';
      case RecruitmentLiveStatus.Cancelled: return 'red';
      default: return 'default';
    }
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
    return live.status === RecruitmentLiveStatus.Waiting || live.status === RecruitmentLiveStatus.Active;
  }

  statusIcon(status: RecruitmentLiveStatus): string {
    switch (status) {
      case RecruitmentLiveStatus.Waiting: return 'clock-circle';
      case RecruitmentLiveStatus.Active: return 'play-circle';
      case RecruitmentLiveStatus.Ended: return 'check-circle';
      case RecruitmentLiveStatus.Cancelled: return 'close-circle';
      default: return 'question-circle';
    }
  }
}
