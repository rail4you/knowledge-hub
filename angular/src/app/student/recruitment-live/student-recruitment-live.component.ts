import { Component, OnInit, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { RecruitmentLiveService } from '../../recruitment-live/recruitment-live.service';
import { RecruitmentLiveDto, RecruitmentLiveStatus } from '../../recruitment-live/recruitment-live.models';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';

@Component({
  selector: 'app-student-recruitment-live',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    StudentHeroComponent,
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

  /** Hero 区数据总览 */
  readonly heroStats = computed(() => {
    const lives = this.lives();
    return [
      { label: '直播总数', value: lives.length, suffix: '场', icon: 'video-camera', color: '#2b6cd4' },
      { label: '进行中', value: lives.filter(l => l.status === RecruitmentLiveStatus.Active).length, suffix: '场', icon: 'play-circle', color: '#10b981' },
      { label: '等待中', value: lives.filter(l => l.status === RecruitmentLiveStatus.Waiting && !this.isExpired(l)).length, suffix: '场', icon: 'clock-circle', color: '#f59e0b' },
      { label: '已结束', value: lives.filter(l => l.status === RecruitmentLiveStatus.Ended).length, suffix: '场', icon: 'check-circle', color: '#94a3b8' },
    ];
  });

  /** 状态过滤：全部 / 进行中 / 等待中 / 已过期 / 已结束 / 已取消 */
  statusFilter = signal<RecruitmentLiveStatus | 'all' | 'expired'>('all');

  readonly filteredLives = computed(() => {
    const list = this.lives();
    const f = this.statusFilter();
    if (f === 'all') return list;
    return list.filter(l => (f === 'expired' ? this.isExpired(l) : l.status === f));
  });

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

  setStatusFilter(f: RecruitmentLiveStatus | 'all' | 'expired') {
    this.statusFilter.set(f);
  }

  /** 状态文本：过期优先显示“已过期” */
  statusText(live: RecruitmentLiveDto): string {
    if (this.isExpired(live)) return '已过期';
    switch (live.status) {
      case RecruitmentLiveStatus.Waiting: return '等待中';
      case RecruitmentLiveStatus.Active: return '进行中';
      case RecruitmentLiveStatus.Ended: return '已结束';
      case RecruitmentLiveStatus.Cancelled: return '已取消';
      default: return '未知';
    }
  }

  /** 是否已过期：仅“等待中且已超出计划结束时间”视为过期；已开始的直播不会过期 */
  isExpired(live: RecruitmentLiveDto): boolean {
    return live.status === RecruitmentLiveStatus.Waiting
      && !!live.scheduledEndAt
      && new Date(live.scheduledEndAt) < new Date();
  }

  /** 是否可进入：进行中始终可进；等待中仅在未过期时可进 */
  canEnter(live: RecruitmentLiveDto): boolean {
    if (live.status === RecruitmentLiveStatus.Active) return true;
    if (live.status === RecruitmentLiveStatus.Waiting) return !this.isExpired(live);
    return false;
  }

  /** 操作区按钮文本：所有状态统一展示按钮，不可进入时禁用 */
  actionButtonText(live: RecruitmentLiveDto): string {
    if (live.status === RecruitmentLiveStatus.Active) return '回到直播间';
    if (live.status === RecruitmentLiveStatus.Waiting) {
      return this.isExpired(live) ? '直播已过期' : '进入直播间';
    }
    if (live.status === RecruitmentLiveStatus.Ended) return '直播已结束';
    if (live.status === RecruitmentLiveStatus.Cancelled) return '直播已取消';
    return '进入直播间';
  }

  /** 操作区按钮图标：可进入用视频图标，不可进入用对应状态图标 */
  actionIcon(live: RecruitmentLiveDto): string {
    if (this.canEnter(live)) return 'video-camera';
    if (live.status === RecruitmentLiveStatus.Ended) return 'check-circle';
    if (live.status === RecruitmentLiveStatus.Cancelled) return 'close-circle';
    if (this.isExpired(live)) return 'clock-circle';
    return 'video-camera';
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
