import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzMessageService } from 'ng-zorro-antd/message';
import { forkJoin } from 'rxjs';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import { PracticumMaterialType } from '../../proxy/practicums/enums/practicum-material-type.enum';
import type { PracticumProjectDetailDto, PracticumMaterialDto } from '../../proxy/practicums/dtos/models';
import { SafeResourceUrlPipe } from '../../shared/safe-resource-url.pipe';
import { WasmMirrorUrlPipe } from '../../shared/wasm-mirror-url.pipe';
import { WasmMirrorService } from '../../shared/wasm-mirror.service';

@Component({
  selector: 'app-student-practicum-detail',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, FormsModule, RouterModule,
    NzButtonModule, NzIconModule, NzSpinModule, NzTabsModule, NzInputModule, NzModalModule,
    SafeResourceUrlPipe, WasmMirrorUrlPipe,
  ],
  templateUrl: './student-practicum-detail.component.html',
  styleUrls: ['./student-practicum-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentPracticumDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly practicumService = inject(PracticumService);
  private readonly wasmMirrorService = inject(WasmMirrorService);
  private readonly message = inject(NzMessageService);

  readonly detail = signal<PracticumProjectDetailDto | null>(null);
  readonly loading = signal(true);
  readonly activeTab = signal<'tasks' | 'materials' | 'simulations'>('tasks');
  readonly submitting = signal(false);
  readonly submitModalVisible = signal(false);
  readonly selectedTaskId = signal<string | null>(null);
  readonly submissionContent = signal('');
  readonly submissionUrl = signal('');

  /**
   * 仿真实训 sourceUrl → 本地镜像 publicUrl 映射。
   * 由 ngOnInit 一次性 forkJoin 加载；pipe 读取此 signal 做纯字符串映射。
   * mapping 失败/为空时 pipe 直接返回原 URL，自动回退到 safeResourceUrl 的代理路径。
   */
  readonly wasmMirrorMap = signal<Map<string, string>>(new Map());

  /** 仿真实训材料（materialType === 4）。空列表时不显示 tab。 */
  readonly simulations = computed<PracticumMaterialDto[]>(() => {
    const materials = this.detail()?.materials ?? [];
    return materials.filter(m => m.materialType === PracticumMaterialType.Simulation && !!m.resourceUrl);
  });

  /** 当前 simulations 中是否存在尚未就绪的镜像（用于展示回退 banner）。 */
  readonly hasUnmirroredSimulation = computed(() => {
    const sims = this.simulations();
    if (sims.length === 0) return false;
    const map = this.wasmMirrorMap();
    // mirror mapping 加载完毕后才判断；加载中显示 banner 容易闪烁
    return sims.some(s => !!s.resourceUrl && !map.has(normalizeSourceUrl(s.resourceUrl!)));
  });

  /** 把 sim.resourceUrl 规范化后返回镜像 publicUrl，未命中返回原 URL。 */
  resolveMirror(url: string): string {
    if (!url) return '';
    const map = this.wasmMirrorMap();
    return map.get(normalizeSourceUrl(url)) ?? url;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.loadDetailAndMirrors(id);
  }

  loadDetailAndMirrors(id: string): void {
    this.loading.set(true);
    forkJoin({
      detail: this.practicumService.getDetail(id),
      mapping: this.wasmMirrorService.getMapping(),
    }).subscribe({
      next: ({ detail, mapping }) => {
        this.detail.set(detail);
        const map = new Map<string, string>();
        for (const m of mapping ?? []) {
          if (m?.sourceUrl && m?.publicUrl) {
            map.set(m.sourceUrl, m.publicUrl);
          }
        }
        this.wasmMirrorMap.set(map);
        this.loading.set(false);
      },
      error: () => {
        // mapping 失败不应硬失败：兜底走源站（safeResourceUrl → /api/proxy）。
        // 此处单一硬失败只展示一次错误，详细错误由浏览器 console 显示。
        this.loading.set(false);
        this.message.error('加载实训详情失败');
      },
    });
  }

  enroll(): void {
    const id = this.detail()?.id;
    if (!id) return;
    this.practicumService.enroll(id).subscribe({
      next: () => { this.message.success('报名成功，等待教师审核'); this.loadDetailAndMirrors(id); },
      error: () => this.message.error('报名失败'),
    });
  }

  openSubmit(taskId?: string): void {
    this.selectedTaskId.set(taskId || null);
    this.submissionContent.set('');
    this.submissionUrl.set('');
    this.submitModalVisible.set(true);
  }

  submitWork(): void {
    const projectId = this.detail()?.id;
    const taskId = this.selectedTaskId();
    const content = this.submissionContent().trim();
    if (!projectId || !taskId || !content) {
      this.message.warning('请填写提交内容');
      return;
    }
    this.submitting.set(true);
    this.practicumService.createSubmission({
      projectId, taskId,
      content,
      linkUrl: this.submissionUrl().trim() || undefined,
    }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.submitModalVisible.set(false);
        this.message.success('提交成功');
      },
      error: () => { this.submitting.set(false); this.message.error('提交失败'); },
    });
  }

  downloadMaterial(material: PracticumMaterialDto): void {
    if (!material.resourceUrl) return;
    window.open(material.resourceUrl, '_blank');
  }

  openChat(): void {
    const id = this.detail()?.id;
    if (!id) return;
    this.router.navigate(['/student/practicums', id, 'chat']);
  }
}

/**
 * 与 wasm-mirror-url.pipe 中 normalizeSource 保持一致，
 * 也与后端 WasmMirrorAppService.NormalizeSourceUrl 保持一致：
 *   - 去除末尾斜杠
 *   - 去除 query / fragment
 *   - host + scheme 小写
 */
export function normalizeSourceUrl(url: string): string {
  if (!url) return '';
  const trimmed = url.trim();
  try {
    const u = new URL(trimmed);
    const path = u.pathname.replace(/\/+$/, '');
    const portPart = (u.port && !isDefaultPort(u.protocol, u.port)) ? `:${u.port}` : '';
    return `${u.protocol}//${u.hostname.toLowerCase()}${portPart}${path}`.toLowerCase();
  } catch {
    return trimmed.replace(/\/+$/, '').toLowerCase();
  }
}

function isDefaultPort(protocol: string, port: string): boolean {
  if (!port) return true;
  if (protocol === 'http:' && port === '80') return true;
  if (protocol === 'https:' && port === '443') return true;
  return false;
}
