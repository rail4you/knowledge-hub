import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { WasmMirrorService, type WasmMirrorInfoDto } from '../../shared/wasm-mirror.service';

/**
 * 学生端「仿真实训中心」列表页。
 *
 * - 调用 GET /api/app/wasm-mirror/all 拉取所有镜像
 * - 仅展示 status === 'ready' 的镜像
 * - 卡片网格：封面图（无则 nz-icon 占位）+ 标题 + 描述 + 文件大小 + 状态徽章
 * - 点击卡片 → /student/wasm-center/:slug 走 wasm-player 全屏打开
 *
 * 学生侧不展示 missing/invalid 状态的镜像；遇到任何拉取失败给出友好提示。
 */
@Component({
  selector: 'app-wasm-center-list',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzTagModule,
    NzButtonModule,
  ],
  templateUrl: './wasm-center-list.component.html',
  styleUrls: ['./wasm-center-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WasmCenterListComponent implements OnInit {
  private readonly wasmMirrorService = inject(WasmMirrorService);
  private readonly message = inject(NzMessageService);

  /** 全部镜像（含 missing 等状态，仅诊断时用）。 */
  readonly all = signal<WasmMirrorInfoDto[]>([]);
  readonly loading = signal(true);

  /** 学生端可见的列表：仅 ready。 */
  readonly readyList = computed<WasmMirrorInfoDto[]>(() =>
    this.all().filter(x => (x.status ?? '').toLowerCase() === 'ready'),
  );

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.wasmMirrorService.getAll().subscribe({
      next: list => {
        this.all.set(Array.isArray(list) ? list : []);
        this.loading.set(false);
      },
      error: err => {
        console.error('[wasm-center] failed to load mirrors', err);
        this.message.error('加载仿真实训列表失败，请稍后重试');
        this.loading.set(false);
      },
    });
  }

  /** 把字节数格式化为人类可读（KB/MB/GB）。 */
  formatBytes(bytes?: number | null): string {
    if (bytes == null || isNaN(bytes)) return '—';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
  }

  trackBySlug(_idx: number, item: WasmMirrorInfoDto): string {
    return item.slug;
  }
}