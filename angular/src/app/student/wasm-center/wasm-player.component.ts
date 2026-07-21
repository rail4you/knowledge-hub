import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { SafeResourceUrlPipe } from '../../shared/safe-resource-url.pipe';
import { WasmMirrorService, type WasmMirrorInfoDto } from '../../shared/wasm-mirror.service';

/**
 * 学生端「仿真实训中心」全屏 iframe 播放页。
 *
 * - 路由 /student/wasm-center/:slug
 * - 通过 slug 在镜像列表中查找对应项（拉取 /api/app/wasm-mirror/all）
 * - 顶部工具栏：返回 + 标题 + 状态徽章 + 新窗口打开
 * - 主区域：iframe 指向本地镜像 /wasm/{slug}/index.html
 *
 * 注意：本组件不依赖 wasm-mirror-url pipe，因为我们已经拿到了 slug，
 * 可以直接拼本地镜像的 publicUrl，不再做 sourceUrl → publicUrl 的 lookup。
 */
@Component({
  selector: 'app-wasm-player',
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
    SafeResourceUrlPipe,
  ],
  templateUrl: './wasm-player.component.html',
  styleUrls: ['./wasm-player.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WasmPlayerComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly wasmMirrorService = inject(WasmMirrorService);
  private readonly message = inject(NzMessageService);

  /** 路由参数中的 slug。 */
  readonly slug = signal<string>('');
  /** 当前 slug 对应的镜像详情。找不到时为 null。 */
  readonly mirror = signal<WasmMirrorInfoDto | null>(null);
  readonly loading = signal(true);

  /** iframe 目标 URL：本地镜像 /wasm/{slug}/index.html。 */
  readonly iframeSrc = computed(() => {
    const slug = this.slug();
    if (!slug) return '';
    return `/wasm/${slug}/index.html`;
  });

  /** 新窗口打开时的完整 URL：拼接 window.location.origin。 */
  readonly openInNewTabUrl = computed(() => {
    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    return `${origin}${this.iframeSrc()}`;
  });

  ngOnInit(): void {
    const slug = this.route.snapshot.paramMap.get('slug') ?? '';
    this.slug.set(slug);
    if (!slug) {
      this.message.error('参数错误：缺少 slug');
      this.loading.set(false);
      return;
    }
    this.load(slug);
  }

  private load(slug: string): void {
    this.loading.set(true);
    this.wasmMirrorService.getAll().subscribe({
      next: list => {
        const found = (list ?? []).find(x => x.slug === slug) ?? null;
        this.mirror.set(found);
        this.loading.set(false);
        if (!found) {
          this.message.warning(`未找到仿真镜像 "${slug}"`);
        } else if ((found.status ?? '').toLowerCase() !== 'ready') {
          this.message.warning(`镜像 "${slug}" 当前状态：${found.status}，可能无法加载`);
        }
      },
      error: err => {
        console.error('[wasm-player] failed to load mirrors', err);
        this.message.error('加载仿真信息失败');
        this.loading.set(false);
      },
    });
  }
}