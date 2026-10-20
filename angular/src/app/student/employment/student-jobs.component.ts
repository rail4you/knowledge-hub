import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { EmploymentApplicationStatus, EmploymentJobType, EmploymentService, JobPostingDto } from '../../employment/employment.service';
import { ClientCacheService } from '../../shared/cache/client-cache.service';
import { StudentJobApplyModalComponent } from './student-job-apply-modal.component';
import { SectionBannerComponent } from '../shared/section-banner/section-banner.component';

@Component({
  selector: 'app-student-jobs',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, RouterLink, NzIconModule, NzSpinModule, NzPaginationModule, StudentJobApplyModalComponent, SectionBannerComponent],
  templateUrl: './student-jobs.component.html',
  styleUrls: ['./student-jobs.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentJobsComponent implements OnInit {
  private readonly svc = inject(EmploymentService);
  private readonly msg = inject(NzMessageService);
  private readonly cache = inject(ClientCacheService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly jobs = signal<JobPostingDto[]>([]);
  readonly loading = signal(false);
  readonly keyword = signal('');
  readonly location = signal('');
  readonly jobType = signal<EmploymentJobType | null>(null);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  readonly jobTypes = EmploymentJobType;
  readonly as = EmploymentApplicationStatus;
  /** 待投递的岗位 id：有值时弹出独立投递弹窗 */
  readonly applyJobId = signal<string | null>(null);
  /** 正在卡片内播放视频的岗位 id：同一时刻只播一条，播完自动复位 */
  readonly playingId = signal<string | null>(null);
  /** 搜索框：清除关键词后把焦点还给输入框 */
  readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('searchInput');

  readonly stats = computed(() => {
    const all = this.jobs();
    const applied = all.filter(x => x.hasApplied).length;
    const interview = all.filter(x =>
      x.applicationStatus === EmploymentApplicationStatus.InterviewScheduled ||
      x.applicationStatus === EmploymentApplicationStatus.InterviewCompleted
    ).length;
    return [
      { label: '在招岗位', value: this.totalCount(), suffix: '个', color: '#087f7a', icon: 'solution' },
      { label: '已投递', value: applied, suffix: '份', color: '#086964', icon: 'send' },
      { label: '面试邀请', value: interview, suffix: '份', color: '#0891b2', icon: 'calendar' },
    ];
  });

  ngOnInit() {
    // 深链 ?keyword=xxx：岗位详情页「查看该公司其他岗位」会带着公司名跳过来，
    // 必须回填到搜索框并参与查询，否则筛选不生效（URL 有 keyword 但列表是全量）。
    const kw = (this.route.snapshot.queryParamMap.get('keyword') || '').trim();
    if (kw) this.keyword.set(kw);
    this.load();

    // 仅 query param 变化时 Angular 会复用组件实例（ngOnInit 不再执行），
    // 这里补一次响应：页内再次跳入带 keyword 的链接时立即生效。
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        const next = (params.get('keyword') || '').trim();
        // 与当前条件一致则不重复加载（回写 URL 触发的回调也走这里，靠它断开循环）。
        if (next === this.keyword()) return;
        this.keyword.set(next);
        this.pageIndex.set(1);
        this.load();
      });
  }

  load(force = false) {
    this.loading.set(true);
    const input = {
      filter: this.keyword().trim() || undefined, location: this.location() || undefined,
      jobType: this.jobType() ?? undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(), maxResultCount: this.pageSize(),
    };
    const loader = () => this.svc.getPublishedJobList(input);
    const key = `jobs:${JSON.stringify(input)}`;
    const request$ = force
      ? this.cache.reload<any>('student.employment', key, loader)
      : this.cache.load<any>('student.employment', key, loader);
    request$.subscribe({
      next: r => { this.jobs.set(r.items || []); this.totalCount.set(r.totalCount || 0); this.loading.set(false); this.playingId.set(null); },
      error: () => { this.loading.set(false); this.msg.error('加载失败'); },
    });
  }

  onSearch() { this.pageIndex.set(1); this.syncUrl(); this.load(); }
  onPage(p: number) { this.pageIndex.set(p); this.load(); }
  reset() { this.keyword.set(''); this.location.set(''); this.jobType.set(null); this.pageIndex.set(1); this.syncUrl(); this.load(); }

  /**
   * 清空搜索关键词：只清 keyword（不动地点/类型），并按全量重新查询。
   * 与 URL 同步，刷新后不会又被关键词过滤。
   */
  clearKeyword(): void {
    if (!this.keyword()) return;
    this.keyword.set('');
    this.pageIndex.set(1);
    this.syncUrl();
    this.load();
    // 焦点留在搜索框，便于直接继续输入
    this.searchInput()?.nativeElement.focus();
  }

  /**
   * 把当前关键词回写到 URL，保证「深链进来」与「页内搜索」看到的地址一致，
   * 刷新页面也不会丢失筛选条件。replaceUrl 避免每次搜索都塞一条历史记录。
   */
  private syncUrl(): void {
    const kw = this.keyword().trim();
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { keyword: kw || null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  /** 详情跳转到独立页面（原型 .detail-hero 布局），不再使用弹窗。已投递会看不了列表。 */
  openDetail(item: JobPostingDto) {
    this.playingId.set(null);
    if (item?.id) this.router.navigate(['/student/employment/jobs', item.id]);
  }

  /**
   * 卡片封面的视频地址：匿名可播，仅已发布岗位。
   * `#t=0.1` 让浏览器取第一帧，避免 Safari/preload=metadata 只显示黑屏。
   */
  videoSrc(item: JobPostingDto): string | null {
    if (!item?.videoUrl) return null;
    const url = this.svc.getJobVideoUrl(item.videoUrl);
    return url ? `${url}#t=0.1` : null;
  }

  /**
   * 点封面/播放按钮：在卡片内直接播放，不冒泡到卡片（否则会弹详情）。
   * 动态切换 `autoplay` 属性不会触发已加载元素的播放，所以这里显式调用 `play()`；
   * 正在播放时直接放行，交给原生 controls 处理暂停/进度。
   */
  startVideo(item: JobPostingDto, ev?: Event) {
    ev?.stopPropagation();
    ev?.preventDefault();
    if (!item?.id || this.playingId() === item.id) return;
    this.playingId.set(item.id);
    const target = ev?.currentTarget as HTMLElement | null;
    const video = (target?.tagName === 'VIDEO'
      ? target
      : target?.closest('.card-cover')?.querySelector('video')) as HTMLVideoElement | null;
    if (!video) return;
    video.muted = false;
    video.currentTime = 0;
    void video.play().catch(() => undefined);
  }
  /** 「立即应聘」：已投递的卡片仍进详情；未投递的直接弹独立投递表单，不进详情 */
  openApply(item: JobPostingDto, ev?: Event) {
    if (!item?.id || item.hasApplied) return;
    ev?.stopPropagation();
    this.applyJobId.set(item.id);
  }
  closeApply() { this.applyJobId.set(null); }

  // ---- 状态显示 ----
  label(s?: EmploymentApplicationStatus): string {
    const m: Record<number,string> = {0:'已投递',1:'已查看',2:'等待面试',3:'已录用',4:'未通过',5:'已撤回',6:'面试完成'};
    return m[s??-1] ?? '已投递';
  }
  color(s?: EmploymentApplicationStatus): string {
    const m: Record<number,string> = {0:'#087f7a',1:'#6366f1',2:'#0891b2',3:'#10b981',4:'#ef4444',5:'#94a3b8',6:'#7c3aed'};
    return m[s??-1] ?? '#087f7a';
  }

  // ---- 类型 ----
  tl(t: EmploymentJobType) { const m: Record<number,string> = {0:'全职',1:'实习',2:'兼职',3:'学徒'}; return m[t]||'其他'; }
  tc(t: EmploymentJobType) { const m: Record<number,string> = {0:'#087f7a',1:'#4aa79f',2:'#10b981',3:'#f59e0b'}; return m[t]||'#6b7280'; }

  tags(item: JobPostingDto): string[] {
    if (!item.skillTags) return [];
    return item.skillTags.split(/[,,;;\n]/).map(s=>s.trim()).filter(Boolean).slice(0,4);
  }

}
