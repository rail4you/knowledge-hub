import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTagModule } from 'ng-zorro-antd/tag';
import {
  EmploymentOutcomeDto,
  EmploymentOutcomeStatus,
  EmploymentService,
} from '../../employment/employment.service';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';

@Component({
  selector: 'app-student-my-outcomes',
  standalone: true,
  imports: [
    CommonModule, DatePipe,
    NzIconModule, NzSpinModule, NzEmptyModule, NzTagModule,
    StudentHeroComponent,
  ],
  templateUrl: './student-my-outcomes.component.html',
  styleUrls: ['./student-my-outcomes.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyOutcomesComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  readonly items = signal<EmploymentOutcomeDto[]>([]);
  readonly loading = signal(false);
  readonly statuses = EmploymentOutcomeStatus;
  readonly statusesArr = [
    { value: EmploymentOutcomeStatus.Intention, label: '就业意向' },
    { value: EmploymentOutcomeStatus.Signed, label: '已签约' },
    { value: EmploymentOutcomeStatus.Employed, label: '已就业' },
    { value: EmploymentOutcomeStatus.FurtherStudy, label: '升学' },
    { value: EmploymentOutcomeStatus.Entrepreneurship, label: '创业' },
    { value: EmploymentOutcomeStatus.Unemployed, label: '待就业' },
  ];

  readonly primaryCount = computed(() => this.items().filter(x => x.isPrimary).length);

  /** Hero 区数据总览 */
  readonly heroStats = computed(() => {
    const items = this.items();
    const signed = items.filter(x => x.status === EmploymentOutcomeStatus.Signed).length;
    const employed = items.filter(x => x.status === EmploymentOutcomeStatus.Employed).length;
    return [
      { label: '总记录数', value: items.length, suffix: '条', icon: 'compass', color: '#1e6ce8' },
      { label: '已签约', value: signed, suffix: '条', icon: 'file-protect', color: '#10b981' },
      { label: '已就业', value: employed, suffix: '条', icon: 'rocket', color: '#0891b2' },
      { label: '主要去向', value: this.primaryCount(), suffix: '条', icon: 'star', color: '#f59e0b' },
    ];
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.employmentService.getOutcomeList({ skipCount: 0, maxResultCount: 100 }).subscribe({
      next: result => {
        this.items.set(result.items || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载就业去向失败');
      },
    });
  }

  statusLabel(s: EmploymentOutcomeStatus): string {
    return this.statusesArr.find(x => x.value === s)?.label ?? '未知';
  }

  statusColor(s: EmploymentOutcomeStatus): string {
    switch (s) {
      case EmploymentOutcomeStatus.Intention: return 'blue';
      case EmploymentOutcomeStatus.Signed: return 'cyan';
      case EmploymentOutcomeStatus.Employed: return 'green';
      case EmploymentOutcomeStatus.FurtherStudy: return 'purple';
      case EmploymentOutcomeStatus.Entrepreneurship: return 'gold';
      case EmploymentOutcomeStatus.Unemployed: return 'default';
      default: return 'default';
    }
  }
}
