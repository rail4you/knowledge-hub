import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzInputModule } from 'ng-zorro-antd/input';
import { WasmMirrorService, type WasmMirrorInfoDto } from '../../shared/wasm-mirror.service';

/**
 * 教师端「仿真实训 WASM 镜像管理」页面。
 *
 * - 表格展示所有镜像（含 missing / invalid / syncing 等状态，方便管理员排查）
 * - 「重新同步」按钮：弹 modal 显示 shell 命令，让管理员手动复制到终端执行
 *   （避免后端 sub-process 跨平台问题与安全审计）
 * - 60s 自动刷新（对齐后端 60s 缓存，简单做法）
 */
@Component({
  selector: 'app-wasm-mirror-admin',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    FormsModule,
    NzButtonModule,
    NzIconModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
    NzModalModule,
    NzEmptyModule,
    NzTooltipModule,
    NzInputModule,
  ],
  templateUrl: './wasm-mirror-admin.component.html',
  styleUrls: ['./wasm-mirror-admin.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WasmMirrorAdminComponent implements OnInit, OnDestroy {
  private readonly wasmMirrorService = inject(WasmMirrorService);
  private readonly message = inject(NzMessageService);

  /** 镜像全量列表（含 missing / invalid 等）。 */
  readonly items = signal<WasmMirrorInfoDto[]>([]);
  readonly loading = signal(true);
  readonly lastRefreshedAt = signal<Date | null>(null);

  /** 「重新同步」modal 状态。 */
  readonly syncModalVisible = signal(false);
  /** modal 中展示的 shell 命令。 */
  readonly syncCommand = signal('');

  private autoRefreshTimer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.load();
    // 每 60s 自动拉一遍，对齐后端 60s 缓存；用户重新同步镜像后最多等 60s 就能在表格里看到。
    this.autoRefreshTimer = setInterval(() => this.load(true), 60_000);
  }

  ngOnDestroy(): void {
    if (this.autoRefreshTimer != null) {
      clearInterval(this.autoRefreshTimer);
      this.autoRefreshTimer = null;
    }
  }

  load(silent = false): void {
    if (!silent) this.loading.set(true);
    this.wasmMirrorService.getAll().subscribe({
      next: list => {
        this.items.set(Array.isArray(list) ? list : []);
        this.lastRefreshedAt.set(new Date());
        this.loading.set(false);
      },
      error: err => {
        console.error('[wasm-mirror-admin] failed to load mirrors', err);
        this.message.error('加载镜像列表失败');
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

  /** 状态 → nz-tag 颜色。 */
  statusColor(status?: string | null): string {
    const s = (status ?? '').toLowerCase();
    if (s === 'ready') return 'success';
    if (s === 'invalid') return 'error';
    if (s === 'syncing') return 'processing';
    return 'warning';
  }

  /** 打开「重新同步」modal：拼 shell 命令展示。 */
  openSyncModal(item: WasmMirrorInfoDto): void {
    const cmd = this.buildSyncCommand(item);
    this.syncCommand.set(cmd);
    this.syncModalVisible.set(true);
  }

  closeSyncModal(): void {
    this.syncModalVisible.set(false);
  }

  /** 构造重新同步命令。 */
  private buildSyncCommand(item: WasmMirrorInfoDto): string {
    const source = item.sourceUrl || '';
    const slug = item.slug || '';
    const title = item.title || '';
    const cover = item.cover || '';
    const desc = item.description || '';

    // 标题/描述含空格或中文，必须加引号；其他字段 slug/url 通常不需要引号
    const quote = (s: string): string => {
      // 简单 JSON 字符串转义：处理 \ 与 "
      const escaped = s.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
      return `"${escaped}"`;
    };

    const parts = ['bash scripts/fetch-wasm.sh', quote(source), slug];
    if (title) parts.push(quote(title));
    if (cover) parts.push(quote(cover));
    if (desc) parts.push(quote(desc));
    return parts.join(' ');
  }

  /** 复制命令到剪贴板。失败时显示 modal 让用户手动复制。 */
  async copyCommand(): Promise<void> {
    const cmd = this.syncCommand();
    try {
      if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(cmd);
        this.message.success('已复制到剪贴板');
      } else {
        throw new Error('Clipboard API 不可用');
      }
    } catch (err) {
      console.warn('[wasm-mirror-admin] copy failed', err);
      this.message.warning('复制失败，请手动选中并复制');
    }
  }

  trackBySlug(_idx: number, item: WasmMirrorInfoDto): string {
    return item.slug;
  }
}