import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { DoubleHighProjectDto, DoubleHighReportDto, DoubleHighService } from './double-high.service';

@Component({
  selector: 'app-double-high-report-center',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzCardModule, NzTableModule],
  templateUrl: './double-high-report-center.component.html',
  styleUrls: ['./double-high-report-center.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DoubleHighReportCenterComponent implements OnInit {
  private readonly doubleHighService = inject(DoubleHighService);
  private readonly message = inject(NzMessageService);

  readonly reports = signal<DoubleHighReportDto[]>([]);
  readonly projects = signal<DoubleHighProjectDto[]>([]);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.doubleHighService.getReportList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.reports.set(result.items || []));

    this.doubleHighService.getList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.projects.set(result.items || []));
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
