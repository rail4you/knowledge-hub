import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { DoubleHighProjectDto, DoubleHighReportDto, DoubleHighService } from './double-high.service';

@Component({
  selector: 'app-double-high-report-center',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzCardModule, NzEmptyModule, NzTableModule],
  templateUrl: './double-high-report-center.component.html',
  styleUrls: ['./double-high-report-center.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DoubleHighReportCenterComponent implements OnInit {
  private readonly doubleHighService = inject(DoubleHighService);
  private readonly message = inject(NzMessageService);

  readonly reports = signal<DoubleHighReportDto[]>([]);
  readonly projects = signal<DoubleHighProjectDto[]>([]);

  // 两张表分页（前端分页：数据已全量加载，按页切片展示）
  readonly projectPageIndex = signal(1);
  readonly projectPageSize = signal(10);
  readonly pagedProjects = computed(() => {
    const all = this.projects();
    const start = (this.projectPageIndex() - 1) * this.projectPageSize();
    return all.slice(start, start + this.projectPageSize());
  });
  readonly reportPageIndex = signal(1);
  readonly reportPageSize = signal(10);
  readonly pagedReports = computed(() => {
    const all = this.reports();
    const start = (this.reportPageIndex() - 1) * this.reportPageSize();
    return all.slice(start, start + this.reportPageSize());
  });

  onProjectPageIndexChange(i: number): void { this.projectPageIndex.set(i); }
  onProjectPageSizeChange(s: number): void { this.projectPageSize.set(s); this.projectPageIndex.set(1); }
  onReportPageIndexChange(i: number): void { this.reportPageIndex.set(i); }
  onReportPageSizeChange(s: number): void { this.reportPageSize.set(s); this.reportPageIndex.set(1); }

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.projectPageIndex.set(1);
    this.reportPageIndex.set(1);
    this.doubleHighService.getReportList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.reports.set(result.items || []));

    this.doubleHighService.getList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.projects.set(result.items || []));
  }

  formatLocalDate(value?: string): string {
    if (!value) {
      return '';
    }
    const date = new Date(value);
    if (isNaN(date.getTime())) {
      return value;
    }
    // 使用本地时间显示，格式：2026-07-01 17:00
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  export(projectId: string): void {
    this.doubleHighService.exportReport(projectId).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `双高评估报表_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.reload();
      },
      error: () => this.message.error('导出失败'),
    });
  }
}
