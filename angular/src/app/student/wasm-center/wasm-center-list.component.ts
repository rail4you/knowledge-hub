import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { PracticumSimulationService } from '../../proxy/practicums/practicum-simulation.service';
import { PracticumSimulationStatus } from '../../proxy/practicums/enums/practicum-simulation-status.enum';
import type { PracticumSimulationDto } from '../../proxy/practicums/simulations/models';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';

/**
 * 学生端 · 仿真实训中心。
 *
 * 数据来自 `GET /api/app/practicum-simulation`（返回全部镜像，无分页参数）。
 * 业务规则：**只展示 Status = Ready 的镜像**，Processing / Invalid 不对学生开放。
 */
@Component({
  selector: 'app-wasm-center-list',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
  ],
  templateUrl: './wasm-center-list.component.html',
  styleUrls: ['./wasm-center-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WasmCenterListComponent implements OnInit {
  private readonly simulationService = inject(PracticumSimulationService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly all = signal<PracticumSimulationDto[]>([]);

  /** 只展示已就绪（Status = Ready）的镜像 */
  readonly readyList = computed<PracticumSimulationDto[]>(() =>
    this.all().filter(x => x.status === PracticumSimulationStatus.Ready),
  );

  ngOnInit(): void {
    this.load();
  }

  /** 加载镜像列表 */
  load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.simulationService.getAll().subscribe({
      next: list => {
        this.all.set(Array.isArray(list) ? list : []);
        this.loading.set(false);
      },
      error: () => {
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  /** 前往实训列表 */
  navigateToPracticums(): void {
    this.router.navigate(['/student/practicums']);
  }

  /** 重新加载 */
  reload(): void {
    this.load();
  }

  /** 文件体积文案 */
  formatBytes(bytes?: number | null): string {
    if (bytes === null || bytes === undefined || isNaN(bytes)) return '—';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
  }
}
