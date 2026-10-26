import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { PracticumProjectDto, PracticumProjectStatus, PracticumService } from './practicum.service';
import { TeachingResourceLibraryService } from '../proxy/resource-libraries/teaching-resource-library.service';

@Component({
  selector: 'app-practicum-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NzButtonModule,
    NzCardModule,
    NzIconModule,
    NzInputModule,
    NzTagModule,
  ],
  templateUrl: './practicum-list.component.html',
  styleUrls: ['./practicum-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticumListComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);
  private readonly libraryService = inject(TeachingResourceLibraryService);
  private readonly route = inject(ActivatedRoute);

  readonly items = signal<PracticumProjectDto[]>([]);
  readonly statuses = PracticumProjectStatus;
  keyword = '';
  /** 教学资源库筛选（来自教学资源库工作台的深链）。 */
  libraryId: string | null = null;
  /** 当前筛选的资源库名称（用于顶部筛选提示）。 */
  readonly libraryName = signal<string | null>(null);

  ngOnInit(): void {
    const deepLinkLibraryId = this.route.snapshot.queryParamMap.get('libraryId');
    if (deepLinkLibraryId) {
      this.libraryId = deepLinkLibraryId;
      this.loadLibraryName(deepLinkLibraryId);
    }
    this.reload();
  }

  private loadLibraryName(id: string): void {
    this.libraryService.getLibrary(id).subscribe({
      next: detail => this.libraryName.set(detail?.name || null),
      error: () => this.libraryName.set(null),
    });
  }

  /** 清除资源库筛选，回到全部实训项目。 */
  clearLibraryFilter(): void {
    this.libraryId = null;
    this.libraryName.set(null);
    this.reload();
  }

  reload(): void {
    const request = {
      filter: this.keyword || undefined,
      libraryId: this.libraryId || undefined,
      skipCount: 0,
      maxResultCount: 100,
    };
    // 指定资源库时看全部状态（含草稿）：教师刚在资源库里建的实训是草稿。
    const source = this.libraryId
      ? this.practicumService.getList(request)
      : this.practicumService.getPublished(request);
    source.subscribe(result => this.items.set(result.items || []));
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
