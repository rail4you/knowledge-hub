import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { PracticumSimulationService } from '../../proxy/practicums/practicum-simulation.service';
import { PracticumSimulationStatus } from '../../proxy/practicums/enums/practicum-simulation-status.enum';
import type { PracticumSimulationDto } from '../../proxy/practicums/simulations/models';

@Component({
  selector: 'app-wasm-mirror-admin',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    NzButtonModule,
    NzIconModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
    NzEmptyModule,
    NzTooltipModule,
  ],
  templateUrl: './wasm-mirror-admin.component.html',
  styleUrls: ['./wasm-mirror-admin.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WasmMirrorAdminComponent implements OnInit {
  private readonly simulationService = inject(PracticumSimulationService);
  private readonly message = inject(NzMessageService);

  readonly items = signal<PracticumSimulationDto[]>([]);
  readonly loading = signal(true);
  readonly lastRefreshedAt = signal<Date | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.simulationService.getAll().subscribe({
      next: list => {
        this.items.set(Array.isArray(list) ? list : []);
        this.lastRefreshedAt.set(new Date());
        this.loading.set(false);
      },
      error: err => {
        console.error('[wasm-mirror-admin] failed to load simulations', err);
        this.message.error('加载仿真镜像列表失败');
        this.loading.set(false);
      },
    });
  }

  formatBytes(bytes?: number | null): string {
    if (bytes == null || isNaN(bytes)) return '—';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
  }

  statusLabel(status?: PracticumSimulationStatus): string {
    if (status === PracticumSimulationStatus.Ready) return '已就绪';
    if (status === PracticumSimulationStatus.Processing) return '处理中';
    if (status === PracticumSimulationStatus.Invalid) return '无效';
    return '未知';
  }

  statusColor(status?: PracticumSimulationStatus): string {
    if (status === PracticumSimulationStatus.Ready) return 'success';
    if (status === PracticumSimulationStatus.Invalid) return 'error';
    if (status === PracticumSimulationStatus.Processing) return 'processing';
    return 'warning';
  }

  delete(item: PracticumSimulationDto): void {
    if (!item.id || !window.confirm(`确定删除仿真镜像“${item.name || item.slug}”吗？`)) return;
    this.simulationService.delete(item.id).subscribe({
      next: () => {
        this.message.success('仿真镜像已删除');
        this.load();
      },
      error: () => this.message.error('删除仿真镜像失败'),
    });
  }

  trackById(_idx: number, item: PracticumSimulationDto): string {
    return item.id ?? item.slug ?? '';
  }
}
