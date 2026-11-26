import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import type { CourseVideoDto, VideoKnowledgeMarkDto } from '../../../proxy/courses/dtos/models';
import { AuthFetchService } from '../../../shared/utils/auth-fetch.service';

/**
 * 课程视频播放器 + 时间轴知识点切片。
 *
 * 设计要点：
 * ① `<video src>` 不能带 Authorization 头，所以播放地址必须用 `authFetch.withAccessToken()`
 *    拼上 access_token 查询参数（后端按课程可见性鉴权，见 CourseVideoController）；
 * ② 时长不由服务端 ffmpeg 探测：`loadedmetadata` 拿到真实时长后通过 `durationDetected` 回填，
 *    省掉一个后台转码依赖；
 * ③ 标记点是「秒 → 知识点」，点一下 seek 到该秒；`editable` 时可在当前秒打点。
 */
@Component({
  selector: 'app-course-video-player',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzIconModule, NzInputModule, NzTooltipModule],
  templateUrl: './course-video-player.component.html',
  styleUrls: ['./course-video-player.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseVideoPlayerComponent {
  private readonly authFetch = inject(AuthFetchService);

  readonly video = input<CourseVideoDto | null>(null);
  readonly marks = input<VideoKnowledgeMarkDto[]>([]);
  readonly editable = input(false);

  readonly durationDetected = output<number>();
  readonly markRequested = output<{ pointSec: number; label: string }>();
  readonly markRemoved = output<string>();
  readonly practiceRequested = output<VideoKnowledgeMarkDto>();

  private readonly videoRef = viewChild<ElementRef<HTMLVideoElement>>('videoEl');

  readonly currentSec = signal(0);
  readonly durationSec = signal(0);
  readonly markLabel = signal('');
  readonly playing = signal(false);

  readonly src = computed(() => {
    const url = this.video()?.playUrl;
    if (!url) return '';
    // 外链直接透传；本站流式端点需要带令牌（<video> 无法自定义请求头）
    return url.startsWith('/api/') ? this.authFetch.withAccessToken(url) : url;
  });

  constructor() {
    // 切换视频时重置播放状态（同一个组件实例复用）
    effect(() => {
      const v = this.video();
      this.currentSec.set(0);
      this.durationSec.set(v?.durationSeconds ?? 0);
      this.markLabel.set('');
    });
  }

  onTimeUpdate(): void {
    const el = this.videoRef()?.nativeElement;
    if (el) this.currentSec.set(Math.floor(el.currentTime));
  }

  onLoadedMetadata(): void {
    const el = this.videoRef()?.nativeElement;
    if (!el) return;
    const seconds = Math.round(el.duration || 0);
    this.durationSec.set(seconds);
    if (seconds > 0 && seconds !== (this.video()?.durationSeconds ?? 0)) {
      this.durationDetected.emit(seconds);
    }
  }

  onPlayStateChange(playing: boolean): void {
    this.playing.set(playing);
  }

  seek(sec: number): void {
    const el = this.videoRef()?.nativeElement;
    if (!el) return;
    el.currentTime = Math.max(0, sec);
    el.play().catch(() => undefined);
  }

  addMarkAtCurrent(): void {
    const label = this.markLabel()?.trim();
    if (!label) return;
    this.markRequested.emit({ pointSec: this.currentSec(), label });
    this.markLabel.set('');
  }

  removeMark(mark: VideoKnowledgeMarkDto): void {
    this.markRemoved.emit(mark.id!);
  }

  practice(mark: VideoKnowledgeMarkDto): void {
    this.practiceRequested.emit(mark);
  }

  /** 时间文本：mm:ss */
  timeText(sec: number): string {
    const safe = Math.max(0, Math.floor(sec || 0));
    const m = Math.floor(safe / 60);
    const s = safe % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }

  /** 进度条上标记的相对位置（%） */
  markPosition(mark: VideoKnowledgeMarkDto): number {
    const total = this.durationSec() || this.video()?.durationSeconds || 0;
    if (!total) return 0;
    return Math.min(100, Math.max(0, ((mark.pointSec ?? 0) / total) * 100));
  }
}
