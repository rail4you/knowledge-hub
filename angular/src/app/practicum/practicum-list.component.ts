import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { PracticumProjectDto, PracticumProjectStatus, PracticumService } from './practicum.service';

@Component({
  selector: 'app-practicum-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, NzButtonModule, NzCardModule, NzInputModule, NzTagModule],
  templateUrl: './practicum-list.component.html',
  styleUrls: ['./practicum-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticumListComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);

  readonly items = signal<PracticumProjectDto[]>([]);
  readonly statuses = PracticumProjectStatus;
  keyword = '';

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.practicumService.getPublished({
      filter: this.keyword || undefined,
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.items.set(result.items || []));
  }

  getStatusLabel(status: PracticumProjectStatus): string {
    const labels: Record<number, string> = {
      [PracticumProjectStatus.Draft]: '草稿',
      [PracticumProjectStatus.Published]: '已发布',
      [PracticumProjectStatus.Archived]: '已归档',
    };
    return labels[status] || '未知';
  }
}
