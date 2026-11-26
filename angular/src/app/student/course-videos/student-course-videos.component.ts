import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CourseVideoService } from '../../proxy/courses/course-video.service';
import type { CourseVideoDto, VideoKnowledgeMarkDto } from '../../proxy/courses/dtos/models';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { LearningService } from '../../proxy/learning/learning.service';
import { CourseVideoPlayerComponent } from '../../learning/course-video/shared/course-video-player.component';

/**
 * 学生端「课程视频」：按课程看视频，时间轴标记点可跳转，标记旁「练一练」直达该知识点的专项练习。
 */
@Component({
  selector: 'app-student-course-videos',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzIconModule,
    NzTagModule,
    CourseVideoPlayerComponent,
  ],
  templateUrl: './student-course-videos.component.html',
  styleUrls: ['./student-course-videos.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentCourseVideosComponent implements OnInit {
  private readonly learningService = inject(LearningService);
  private readonly videoService = inject(CourseVideoService);
  private readonly message = inject(NzMessageService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly videos = signal<CourseVideoDto[]>([]);
  readonly current = signal<CourseVideoDto | null>(null);
  readonly loading = signal(false);

  ngOnInit(): void {
    this.loadCourses();
  }

  loadCourses(): void {
    this.learningService.getMyCourses().subscribe({
      next: list => {
        const mapped = (list ?? []).map(x => ({ id: x.courseId, title: x.courseTitle }) as CourseDto);
        this.courses.set(mapped);
        if (mapped.length > 0 && mapped[0].id) this.onCourseSelected(mapped[0].id);
      },
      error: () => this.courses.set([]),
    });
  }

  onCourseSelected(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    this.current.set(null);
    if (!courseId) {
      this.videos.set([]);
      return;
    }

    this.loading.set(true);
    this.videoService.getList(courseId).subscribe({
      next: rows => {
        const visible = (rows ?? []).filter(v => v.isPublished);
        this.videos.set(visible);
        this.current.set(visible.length > 0 ? visible[0] : null);
        this.loading.set(false);
        if (visible.length > 0) this.openVideo(visible[0]);
      },
      error: () => {
        this.videos.set([]);
        this.loading.set(false);
      },
    });
  }

  openVideo(video: CourseVideoDto): void {
    // 列表接口已带 marks；再取一次详情确保拿到最新打点
    this.videoService.get(video.id!).subscribe({
      next: full => this.current.set(full),
      error: () => this.current.set(video),
    });
  }

  /**
   * 标记旁「练一练」：直接练**该知识点**的题。
   *
   * 传 knowledgeResourceId（课程知识点树节点）：后端 `PracticeAppService` 支持按
   * `Exercise.KnowledgeResourceId` 直接抽题，不依赖「智学分析 KP」映射也能精确到知识点。
   */
  practice(mark: VideoKnowledgeMarkDto): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    const label = encodeURIComponent(mark.knowledgeResourceName || mark.label || '');
    const params = new URLSearchParams({ courseId, kpName: label });
    if (mark.knowledgeResourceId) params.set('knowledgeResourceId', mark.knowledgeResourceId);

    window.location.href = `/student/practice?${params.toString()}`;
  }

  onDurationDetected(seconds: number): void {
    const video = this.current();
    if (!video?.id) return;
    this.videoService.updateDuration(video.id, seconds).subscribe({
      next: updated => this.current.set({ ...video, durationSeconds: updated.durationSeconds }),
      error: () => undefined,
    });
  }

  timeText(sec?: number): string {
    const safe = Math.max(0, Math.floor(sec ?? 0));
    return `${Math.floor(safe / 60)
      .toString()
      .padStart(2, '0')}:${(safe % 60).toString().padStart(2, '0')}`;
  }
}
