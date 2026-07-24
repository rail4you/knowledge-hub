import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzMessageService } from 'ng-zorro-antd/message';
import { EmploymentService, StudentApplicationStatDto } from '../../employment/employment.service';

const STATUS_LABELS: Record<number, string> = {
  0: '已投递', 1: '已查看', 2: '面试中', 3: '已录取', 4: '已拒绝', 5: '已撤回', 6: '面试完成',
};
const STATUS_COLORS: Record<number, string> = {
  0: 'blue', 1: 'cyan', 2: 'orange', 3: 'green', 4: 'red', 5: 'default', 6: 'purple',
};

interface StudentGroup {
  studentId: string;
  studentName: string;
  items: StudentApplicationStatDto[];
}

@Component({
  selector: 'app-employment-statistics',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzCardModule, NzTagModule, NzTableModule, NzIconModule, NzEmptyModule, NzSpinModule, NzRadioModule, NzDividerModule],
  templateUrl: './employment-statistics.component.html',
  styleUrls: ['./employment-statistics.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentStatisticsComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly items = signal<StudentApplicationStatDto[]>([]);
  readonly days = signal<number | null>(null); // 默认显示全部（切换后可看近7/30天）

  /** 按学生分组 */
  readonly groups = computed<StudentGroup[]>(() => {
    const map = new Map<string, StudentGroup>();
    for (const item of this.items()) {
      const key = item.studentId || 'unknown';
      if (!map.has(key)) {
        map.set(key, { studentId: key, studentName: item.studentName || '未知', items: [] });
      }
      map.get(key)!.items.push(item);
    }
    return Array.from(map.values()).sort((a, b) => b.items.length - a.items.length);
  });

  readonly totalStudents = computed(() => this.groups().length);
  readonly totalApplications = computed(() => this.items().length);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.employmentService.getApplicationStats({
      days: this.days() ?? undefined,
    }).subscribe({
      next: data => {
        this.items.set(data || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载失败');
      },
    });
  }

  setDays(d: number | null): void {
    this.days.set(d);
    this.load();
  }

  statusLabel(s: number): string { return STATUS_LABELS[s] ?? `未知(${s})`; }
  statusColor(s: number): string { return STATUS_COLORS[s] ?? 'default'; }

  export(): void {
    this.employmentService.exportStatistics({
      days: this.days() ?? undefined,
    }).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `employment-stats-${Date.now()}.xlsx`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.message.error('导出失败'),
    });
  }

}
