import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { SafeResourceUrlPipe } from '../../shared/safe-resource-url.pipe';
import { PracticumSimulationService } from '../../proxy/practicums/practicum-simulation.service';
import { PracticumSimulationStatus } from '../../proxy/practicums/enums/practicum-simulation-status.enum';
import type { PracticumSimulationDto } from '../../proxy/practicums/simulations/models';

@Component({
  selector: 'app-wasm-player',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzTagModule,
    SafeResourceUrlPipe,
  ],
  templateUrl: './wasm-player.component.html',
  styleUrls: ['./wasm-player.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WasmPlayerComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly simulationService = inject(PracticumSimulationService);
  private readonly message = inject(NzMessageService);

  readonly slug = signal('');
  readonly mirror = signal<PracticumSimulationDto | null>(null);
  readonly loading = signal(true);

  readonly iframeSrc = computed(() => {
    const mirror = this.mirror();
    if (mirror?.publicUrl) return mirror.publicUrl;
    const slug = this.slug();
    return slug ? `/wasm/${encodeURIComponent(slug)}/index.html` : '';
  });

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

  statusLabel(status?: PracticumSimulationStatus): string {
    if (status === PracticumSimulationStatus.Ready) return '已就绪';
    if (status === PracticumSimulationStatus.Processing) return '处理中';
    if (status === PracticumSimulationStatus.Invalid) return '无效';
    return '未知';
  }

  private load(slug: string): void {
    this.loading.set(true);
    this.simulationService.getAll().subscribe({
      next: list => {
        const found = (list ?? []).find(x => x.slug === slug) ?? null;
        this.mirror.set(found);
        this.loading.set(false);
        if (!found) this.message.warning(`未找到仿真镜像 "${slug}"`);
        else if (found.status !== PracticumSimulationStatus.Ready) this.message.warning(`镜像 "${slug}" 当前不可用`);
      },
      error: err => {
        console.error('[wasm-player] failed to load simulations', err);
        this.message.error('加载仿真信息失败');
        this.loading.set(false);
      },
    });
  }
}
