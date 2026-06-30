import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { EmploymentApplicationStatus, EmploymentJobType, EmploymentService, JobPostingDto } from '../../employment/employment.service';

@Component({
  selector: 'app-student-jobs',
  standalone: true,
  imports: [CommonModule, DatePipe, DecimalPipe, FormsModule, NzIconModule, NzSpinModule, NzEmptyModule, RouterLink],
  templateUrl: './student-jobs.component.html',
  styleUrls: ['./student-jobs.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentJobsComponent implements OnInit {
  private readonly svc = inject(EmploymentService);
  private readonly router = inject(Router);
  private readonly msg = inject(NzMessageService);

  readonly jobs = signal<JobPostingDto[]>([]);
  readonly loading = signal(false);
  readonly keyword = signal('');
  readonly location = signal('');
  readonly jobType = signal<EmploymentJobType | null>(null);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(12);
  readonly jobTypes = EmploymentJobType;
  readonly as = EmploymentApplicationStatus;

  readonly stats = computed(() => {
    const all = this.jobs();
    const applied = all.filter(x => x.hasApplied).length;
    const interview = all.filter(x =>
      x.applicationStatus === EmploymentApplicationStatus.InterviewScheduled ||
      x.applicationStatus === EmploymentApplicationStatus.InterviewCompleted
    ).length;
    return [
      { label: '在招岗位', value: this.totalCount(), suffix: '个', color: '#1e6ce8', icon: 'briefcase' },
      { label: '已投递', value: applied, suffix: '份', color: '#0c4cb8', icon: 'paper-plane' },
      { label: '面试邀请', value: interview, suffix: '份', color: '#0891b2', icon: 'calendar' },
    ];
  });

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.svc.getPublishedJobList({
      filter: this.keyword() || undefined, location: this.location() || undefined,
      jobType: this.jobType() ?? undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(), maxResultCount: this.pageSize(),
    }).subscribe({
      next: r => { this.jobs.set(r.items || []); this.totalCount.set(r.totalCount || 0); this.loading.set(false); },
      error: () => { this.loading.set(false); this.msg.error('加载失败'); },
    });
  }

  onSearch() { this.pageIndex.set(1); this.load(); }
  onPage(p: number) { this.pageIndex.set(p); this.load(); }
  reset() { this.keyword.set(''); this.location.set(''); this.jobType.set(null); this.pageIndex.set(1); this.load(); }
  go(item: JobPostingDto) { this.router.navigate(['/student/employment/jobs', item.id]); }

  // ---- 状态显示 ----
  label(s?: EmploymentApplicationStatus): string {
    const m: Record<number,string> = {0:'已投递',1:'已查看',2:'等待面试',3:'已录用',4:'未通过',5:'已撤回',6:'面试完成'};
    return m[s??-1] ?? '已投递';
  }
  color(s?: EmploymentApplicationStatus): string {
    const m: Record<number,string> = {0:'#1e6ce8',1:'#6366f1',2:'#0891b2',3:'#10b981',4:'#ef4444',5:'#94a3b8',6:'#7c3aed'};
    return m[s??-1] ?? '#1e6ce8';
  }

  // ---- 类型 ----
  tl(t: EmploymentJobType) { const m: Record<number,string> = {0:'全职',1:'实习',2:'兼职',3:'学徒'}; return m[t]||'其他'; }
  tc(t: EmploymentJobType) { const m: Record<number,string> = {0:'#1e6ce8',1:'#00b7ff',2:'#10b981',3:'#f59e0b'}; return m[t]||'#6b7280'; }

  tags(item: JobPostingDto): string[] {
    if (!item.skillTags) return [];
    return item.skillTags.split(/[,,;;\n]/).map(s=>s.trim()).filter(Boolean).slice(0,4);
  }

  cg(item: JobPostingDto): string {
    const p=['#1e6ce8','#0c4cb8','#1d4ed8','#2563eb','#0284c7'];
    let h=0; for(let i=0;i<(item.id||'').length;i++)h=(h*31+(item.id||'').charCodeAt(i))|0;
    return p[Math.abs(h)%p.length];
  }
}
