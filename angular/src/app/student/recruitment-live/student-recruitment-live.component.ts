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
    return live.status === RecruitmentLiveStatus.Waiting || live.status === RecruitmentLiveStatus.Active;
  }
}
