import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzMessageService } from 'ng-zorro-antd/message';
import { RecruitmentLiveService } from '../../recruitment-live/recruitment-live.service';
import { RecruitmentLiveDto, RecruitmentLiveStatus } from '../../recruitment-live/recruitment-live.models';

@Component({
  selector: 'app-recruitment-live-management',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzTableModule,
    NzButtonModule,
    NzTagModule,
    NzIconModule,
    NzPopconfirmModule,
  ],
  templateUrl: './recruitment-live-management.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecruitmentLiveManagementComponent implements OnInit {
  private liveService = inject(RecruitmentLiveService);
  private router = inject(Router);
  private message = inject(NzMessageService);

  loading = signal(false);
  lives = signal<RecruitmentLiveDto[]>([]);
  total = signal(0);
  pageIndex = signal(1);
  pageSize = signal(10);
  filter = signal('');

  ngOnInit() {
    this.loadLives();
  }

  loadLives() {
    this.loading.set(true);
    this.liveService.getTeacherLives({
      filter: this.filter() || undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    }).subscribe({
      next: (res) => {
        this.lives.set(res.items);
        this.total.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载直播列表失败');
      },
    });
  }

  createLive() {
    this.router.navigate(['/admin/recruitment-live/create']);
  }

  enterLive(live: RecruitmentLiveDto) {
    this.router.navigate(['/admin/recruitment-live', live.id]);
  }

  cancelLive(live: RecruitmentLiveDto) {
    this.liveService.cancelLive(live.id).subscribe({
      next: () => {
        this.message.success('直播已取消');
        this.loadLives();
      },
    });
  }

  deleteLive(live: RecruitmentLiveDto) {
    this.liveService.deleteLive(live.id).subscribe({
      next: () => {
        this.message.success('直播已删除');
        this.loadLives();
      },
    });
  }

  search() {
    this.pageIndex.set(1);
    this.loadLives();
  }

  pageChange(index: number) {
    this.pageIndex.set(index);
    this.loadLives();
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

  canCancel(live: RecruitmentLiveDto): boolean {
    return live.status === RecruitmentLiveStatus.Waiting;
  }

  canDelete(live: RecruitmentLiveDto): boolean {
    return live.status === RecruitmentLiveStatus.Ended || live.status === RecruitmentLiveStatus.Cancelled;
  }
}
