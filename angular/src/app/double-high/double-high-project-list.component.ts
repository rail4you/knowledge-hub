import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { DoubleHighProjectDto, DoubleHighProjectStatus, DoubleHighService } from './double-high.service';

@Component({
  selector: 'app-double-high-project-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, NzButtonModule, NzCardModule, NzInputModule, NzProgressModule, NzTagModule],
  templateUrl: './double-high-project-list.component.html',
  styleUrls: ['./double-high-project-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DoubleHighProjectListComponent implements OnInit {
  private readonly doubleHighService = inject(DoubleHighService);

  readonly items = signal<DoubleHighProjectDto[]>([]);
  readonly statuses = DoubleHighProjectStatus;
  keyword = '';

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.doubleHighService.getList({
      filter: this.keyword || undefined,
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.items.set(result.items || []));
  }

  getStatusLabel(status: DoubleHighProjectStatus): string {
    const labels: Record<number, string> = {
      [DoubleHighProjectStatus.Draft]: '草稿',
      [DoubleHighProjectStatus.Active]: '进行中',
      [DoubleHighProjectStatus.Closed]: '已关闭',
    };
    return labels[status] || '未知';
  }
}
