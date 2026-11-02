import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ResourceLibraryLearnerService } from '../../proxy/resource-libraries/resource-library-learner.service';
import type { ResourceLibraryLearnerDto } from '../../proxy/resource-libraries/dtos/models';
import { ResourceLibraryLearnerSource } from '../../proxy/resource-libraries/resource-library-learner-source.enum';
import { ResourceLibraryService } from '../../proxy/resource-libraries/resource-library.service';
import type { ResourceLibraryDto } from '../../proxy/resource-libraries/dtos/models';
import { AuthFetchService } from '../../shared/utils/auth-fetch.service';

/**
 * 教师端「其他学员」目录（PRD 5.3）。
 *
 * <para>
 * 「其他学员」是系统级默认目录，不需要老师手动创建：第一个其他学员开始学习时系统自动建档。
 * 该目录内学员不归属任何班级，与老师自建班级天然隔离，支持按学员维度查看、导出。
 * </para>
 */
@Component({
  selector: 'app-other-learners',
  standalone: true,
  imports: [
    CommonModule,
    NzCardModule,
    FormsModule,
    NzButtonModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './other-learners.component.html',
  styleUrls: ['./other-learners.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OtherLearnersComponent implements OnInit {
  private readonly learnerService = inject(ResourceLibraryLearnerService);
  private readonly libraryService = inject(ResourceLibraryService);
  private readonly authFetch = inject(AuthFetchService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly exporting = signal(false);
  readonly learners = signal<ResourceLibraryLearnerDto[]>([]);
  readonly total = signal(0);
  readonly libraries = signal<ResourceLibraryDto[]>([]);

  filterText = '';
  libraryId: string | null = null;
  source: ResourceLibraryLearnerSource | null = ResourceLibraryLearnerSource.SelfLearner;

  ngOnInit(): void {
    this.loadLibraries();
    this.load();
  }

  loadLibraries(): void {
    this.libraryService.getList({}).subscribe({
      next: list => this.libraries.set(list || []),
      error: () => undefined,
    });
  }

  load(): void {
    this.loading.set(true);
    this.learnerService
      .getLearners({
        libraryId: this.libraryId ?? undefined,
        source: this.source ?? undefined,
        filter: this.filterText || undefined,
        skipCount: 0,
        maxResultCount: 500,
      })
      .subscribe({
        next: res => {
          this.learners.set(res?.items || []);
          this.total.set(res?.totalCount || 0);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载「其他学员」失败');
        },
      });
  }

  async export(): Promise<void> {
    this.exporting.set(true);
    try {
      const res = await this.authFetch.fetch('/api/app/resource-library-learner/export-learners', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          libraryId: this.libraryId ?? undefined,
          source: this.source ?? undefined,
          filter: this.filterText || undefined,
        }),
      });
      if (!res.ok) {
        throw new Error(`导出失败: ${res.status}`);
      }
      const blob = await res.blob();
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `其他学员-${new Date().toISOString().slice(0, 10)}.xlsx`;
      a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 5000);
    } catch (e: any) {
      this.message.error(e?.message || '导出失败');
    } finally {
      this.exporting.set(false);
    }
  }

  formatDate(value?: string | null): string {
    if (!value) return '—';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '—';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }
}
