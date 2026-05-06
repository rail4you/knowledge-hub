import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { EmploymentOutcomeStatus, EmploymentService, EmploymentStatisticsRowDto } from '../../employment/employment.service';

@Component({
  selector: 'app-employment-statistics',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzCardModule, NzInputModule, NzTableModule],
  templateUrl: './employment-statistics.component.html',
  styleUrls: ['./employment-statistics.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentStatisticsComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  readonly rows = signal<EmploymentStatisticsRowDto[]>([]);
  readonly statuses = EmploymentOutcomeStatus;
  major = '';
  grade = '';
  status?: EmploymentOutcomeStatus;

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.employmentService.getStatistics({
      major: this.major || undefined,
      grade: this.grade || undefined,
      status: this.status,
    }).subscribe(items => this.rows.set(items || []));
  }

  export(): void {
    this.employmentService.exportStatistics({
      major: this.major || undefined,
      grade: this.grade || undefined,
      status: this.status,
    }).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `employment-statistics-${Date.now()}.xlsx`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.message.error('导出失败'),
    });
  }
}
