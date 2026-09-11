import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { PracticumSimulationService } from '../../proxy/practicums/practicum-simulation.service';
import { PracticumSimulationStatus } from '../../proxy/practicums/enums/practicum-simulation-status.enum';
import type { PracticumSimulationDto } from '../../proxy/practicums/simulations/models';

@Component({
  selector: 'app-wasm-center-list',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './wasm-center-list.component.html',
  styleUrls: ['./wasm-center-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WasmCenterListComponent implements OnInit {
  private readonly simulationService = inject(PracticumSimulationService);
  private readonly message = inject(NzMessageService);

  readonly all = signal<PracticumSimulationDto[]>([]);
  readonly loading = signal(true);
  readonly readyList = computed<PracticumSimulationDto[]>(() =>
    this.all().filter(x => x.status === PracticumSimulationStatus.Ready),
  );

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.simulationService.getAll().subscribe({
      next: list => {
        this.all.set(Array.isArray(list) ? list : []);
        this.loading.set(false);
      },
      error: err => {
        console.error('[wasm-center] failed to load simulations', err);
        this.message.error('加载仿真实训列表失败，请稍后重试');
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

  trackBySlug(_idx: number, item: PracticumSimulationDto): string {
    return item.slug ?? item.id ?? '';
  }
}
