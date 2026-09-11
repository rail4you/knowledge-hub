import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import { ResourceMediaStatus } from '../../proxy/resources/enums/resource-media-status.enum';

type CoverMode = 'video' | 'image' | 'doc' | 'icon';

const OFFICE_EXTENSIONS = new Set(['pptx', 'ppt', 'docx', 'doc', 'xlsx', 'xls']);

/**
 * 资源封面：优先使用服务端生成的缩略图（图片缩放 / 视频抽帧 / PDF&Office 首页图）。
 *
 * - 进入视口才加载（IntersectionObserver）；
 * - 缩略图请求带 countView=false，不计入浏览量；
 * - Office/PDF 使用服务端首页图（由媒体处理流水线生成），失败回退图标；
 * - 图片/视频缩略图失败回退原图/视频；
 * - 媒体生成中展示"生成中"提示，不请求缩略图。
 */
@Component({
  selector: 'app-resource-cover',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './resource-cover.component.html',
  styleUrls: ['./resource-cover.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceCoverComponent {
  readonly resourceId = input('');
  readonly resourceType = input<number>(ResourceType.Document);
  /** 用于判断扩展名（originalFileName 优先） */
  readonly fileName = input('');
  readonly alt = input('');
  /** 资源媒体处理状态（ResourceMediaStatus），Processing 时展示"生成中"提示且不请求缩略图 */
  readonly mediaStatus = input<number>(0);

  /** 是否正在生成缩略图/预览 */
  readonly mediaProcessing = computed(() => this.mediaStatus() === ResourceMediaStatus.Processing);

  private readonly destroyRef = inject(DestroyRef);
  private readonly hostRef = viewChild<ElementRef<HTMLElement>>('host');

  private observer: IntersectionObserver | null = null;
  private destroyed = false;

  /** 小写无点扩展名 */
  private readonly ext = computed(() => {
    const name = this.fileName() || '';
    const dot = name.lastIndexOf('.');
    return dot >= 0 ? name.substring(dot + 1).toLowerCase() : '';
  });

  readonly mode = computed<CoverMode>(() => {
    if (!this.resourceId()) return 'icon';
    if (this.resourceType() === ResourceType.Video) return 'video';
    if (this.resourceType() === ResourceType.Image) return 'image';
    const e = this.ext();
    if (e === 'pdf' || OFFICE_EXTENSIONS.has(e)) return 'doc';
    return 'icon';
  });

  /** 进入视口后才加载真实媒体 */
  readonly loadMedia = signal(false);
  readonly failed = signal(false);
  /** 缩略图加载失败后回退原图/视频 */
  readonly thumbFailed = signal(false);

  readonly previewUrl = computed(() =>
    this.resourceId() ? `/api/resource-file/${this.resourceId()}/preview?countView=false` : ''
  );

  /** 服务端生成的缩略图，列表封面优先使用，避免下载原文件 */
  readonly thumbnailUrl = computed(() =>
    this.resourceId() ? `/api/resource-file/${this.resourceId()}/thumbnail?w=400` : ''
  );

  constructor() {
    // 输入变化时重置状态并重新监听（组件被复用时）
    effect(() => {
      const el = this.hostRef()?.nativeElement;
      // 显式订阅输入
      const rid = this.resourceId();
      this.resourceType();
      this.fileName();
      this.failed.set(false);
      this.thumbFailed.set(false);
      this.loadMedia.set(false);
      if (el && rid) {
        this.observe(el, rid);
      }
    });
    this.destroyRef.onDestroy(() => {
      this.destroyed = true;
      this.disconnect();
    });
  }

  onMediaError() {
    this.failed.set(true);
  }

  /** 缩略图加载失败：图片回退原图，视频回退 <video>，Office/PDF 直接回退图标 */
  onThumbError() {
    if (this.thumbFailed()) {
      this.failed.set(true);
      return;
    }
    this.thumbFailed.set(true);
  }

  private observe(el: HTMLElement, rid: string) {
    this.disconnect();
    // 不支持 IntersectionObserver 的环境直接加载
    if (typeof IntersectionObserver === 'undefined') {
      this.handleVisible(rid);
      return;
    }
    this.observer = new IntersectionObserver(
      entries => {
        if (entries.some(e => e.isIntersecting)) {
          this.handleVisible(rid);
          this.disconnect();
        }
      },
      { rootMargin: '200px' }
    );
    this.observer.observe(el);
  }

  private handleVisible(rid: string) {
    if (this.destroyed || rid !== this.resourceId()) return;
    this.loadMedia.set(true);
  }

  private disconnect() {
    this.observer?.disconnect();
    this.observer = null;
  }
}
