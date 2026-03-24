import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ResourceService } from '../../proxy/resources/resource.service';
import type { ResourceDto } from '../../proxy/resources/models';

@Component({
  selector: 'app-student-resources',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzIconModule,
    NzTableModule,
    NzTagModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzTooltipModule
  ],
  templateUrl: './student-resources.component.html',
  styleUrls: ['./student-resources.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentResourcesComponent implements OnInit {
  private readonly resourceService = inject(ResourceService);
  private readonly message = inject(NzMessageService);

  resources = signal<ResourceDto[]>([]);
  loading = signal(false);
  filterText = signal('');
  selectedType = signal<number | null>(null);
  selectedCategory = signal<string | null>(null);

  resourceTypes = [
    { label: '全部', value: null as number | null },
    { label: '文档', value: 0 as number | null },
    { label: '视频', value: 1 as number | null },
    { label: '音频', value: 2 as number | null },
    { label: '图片', value: 3 as number | null },
    { label: 'PPT', value: 4 as number | null }
  ];

  categories = signal<{ id: string; name: string }[]>([]);

  ngOnInit() {
    this.loadResources();
    this.loadCategories();
  }

  loadResources() {
    this.loading.set(true);
    this.resourceService.getLeagueApproved({
      maxResultCount: 100,
      skipCount: 0
    } as any).subscribe({
      next: (result) => {
        let items = result.items || [];
        if (this.filterText()) {
          const filter = this.filterText().toLowerCase();
          items = items.filter(r => 
            r.name?.toLowerCase().includes(filter) ||
            r.description?.toLowerCase().includes(filter)
          );
        }
        if (this.selectedType() !== null) {
          items = items.filter(r => r.resourceType === this.selectedType()!);
        }
        this.resources.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  loadCategories() {
    this.resourceService.getCategories().subscribe({
      next: (cats) => {
        this.categories.set(cats.map(c => ({ id: c.id!, name: c.name || '' })));
      }
    });
  }

  getResourceTypeName(type?: number): string {
    const types = ['文档', '视频', '音频', '图片', 'PPT'];
    return types[type || 0] || '文档';
  }

  getResourceTypeColor(type?: number): string {
    const colors = ['blue', 'purple', 'orange', 'green', 'red'];
    return colors[type || 0] || 'blue';
  }

  downloadResource(id?: string) {
    if (!id) return;
    this.resourceService.download(id).subscribe({
      next: () => {
        this.message.success('下载成功');
      },
      error: () => {
        this.message.error('下载失败');
      }
    });
  }

  viewResource(id?: string) {
    if (!id) return;
    window.open(`/document-viewer/${id}`, '_blank');
  }

  collectResource(id?: string) {
    if (!id) return;
    this.resourceService.collect(id).subscribe({
      next: () => {
        this.message.success('收藏成功');
      },
      error: () => {
        this.message.error('收藏失败');
      }
    });
  }
}