import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { take } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzUploadModule, type NzUploadFile } from 'ng-zorro-antd/upload';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { CourseVideoService } from '../../proxy/courses/course-video.service';
import type { CourseVideoDto } from '../../proxy/courses/dtos/models';
import type { CourseDto, ChapterDto, KnowledgeResourceDto } from '../../proxy/courses/dtos/models';
import { ChapterService } from '../../proxy/courses/chapter.service';
import { KnowledgeResourceService } from '../../proxy/courses/knowledge-resource.service';
import { MyCoursesService } from '../shared/my-courses.service';
import { CourseContextService } from '../course-context.service';
import { CourseVideoUploadService } from './course-video.service';
import { CourseVideoPlayerComponent } from './shared/course-video-player.component';

/** 课程视频管理：上传 → 打点（时间轴知识点切片） → 排序 → 发布。 */
@Component({
  selector: 'app-course-video-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzIconModule,
    NzTableModule,
    NzTagModule,
    NzModalModule,
    NzFormModule,
    NzUploadModule,
    NzDrawerModule,
    CourseVideoPlayerComponent,
  ],
  templateUrl: './course-video-management.component.html',
  styleUrls: ['./course-video-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseVideoManagementComponent implements OnInit {
  private readonly myCourses = inject(MyCoursesService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(CourseVideoService);
  private readonly uploader = inject(CourseVideoUploadService);
  private readonly chapterService = inject(ChapterService);
  private readonly knowledgeService = inject(KnowledgeResourceService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly videos = signal<CourseVideoDto[]>([]);
  readonly loading = signal(false);

  /** 页头统计：当前课程全部视频的标记总数 */
  readonly totalMarks = computed(() => this.videos().reduce((sum, v) => sum + (v.markCount ?? 0), 0));

  readonly chapters = signal<ChapterDto[]>([]);
  readonly knowledgeOptions = signal<{ id: string; label: string }[]>([]);

  readonly isUploadOpen = signal(false);
  readonly uploading = signal(false);
  readonly uploadForm = signal<{
    title: string;
    chapterId: string | null;
    knowledgeResourceId: string | null;
    description: string;
  }>({ title: '', chapterId: null, knowledgeResourceId: null, description: '' });
  readonly selectedFile = signal<File | null>(null);

  /** 打点抽屉 */
  readonly editing = signal<CourseVideoDto | null>(null);
  readonly editingMarks = signal<CourseVideoDto['marks']>([]);

  ngOnInit(): void {
    this.loadCourses();
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      const courseId = params.get('courseId') || this.courseContext.currentCourseId();
      if (courseId) this.onCourseSelected(courseId);
    });
  }

  loadCourses(): void {
    this.myCourses.getCourses(300).subscribe({
      next: list => this.courses.set(list as any),
      error: () => this.courses.set([]),
    });
  }

  onCourseSelected(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    if (!courseId) {
      this.videos.set([]);
      return;
    }
    this.courseContext.setCourse(courseId);
    this.loadList();
    this.loadChapters(courseId);
    this.loadKnowledgeOptions(courseId);
  }

  loadList(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    this.loading.set(true);
    this.service.getList(courseId).subscribe({
      next: rows => {
        this.videos.set(rows ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.videos.set([]);
        this.loading.set(false);
      },
    });
  }

  loadChapters(courseId: string): void {
    this.chapterService.getChapterTree(courseId).subscribe({
      next: rows => this.chapters.set(rows ?? []),
      error: () => this.chapters.set([]),
    });
  }

  loadKnowledgeOptions(courseId: string): void {
    this.knowledgeService.getTree(courseId).subscribe({
      next: rows => {
        const flat: { id: string; label: string }[] = [];
        const walk = (nodes: KnowledgeResourceDto[], depth: number) => {
          for (const n of nodes) {
            if (!n.id) continue;
            flat.push({ id: n.id, label: '　'.repeat(depth) + (n.name ?? '') });
            if (n.children?.length) walk(n.children, depth + 1);
          }
        };
        walk(rows ?? [], 0);
        this.knowledgeOptions.set(flat);
      },
      error: () => this.knowledgeOptions.set([]),
    });
  }

  // ── 上传 ─────────────────────────────────────────────────────

  openUpload(): void {
    if (!this.selectedCourseId()) {
      this.message.warning('请先选择课程');
      return;
    }
    this.uploadForm.set({ title: '', chapterId: null, knowledgeResourceId: null, description: '' });
    this.selectedFile.set(null);
    this.isUploadOpen.set(true);
  }

  patchUpload(patch: Partial<ReturnType<typeof this.uploadForm>>): void {
    this.uploadForm.set({ ...this.uploadForm(), ...patch } as any);
  }

  beforeUpload = (file: NzUploadFile): boolean => {
    const raw = file as unknown as File;
    const name = (raw.name ?? '').toLowerCase();
    if (!/\.(mp4|m4v|webm|mov|ogv)$/.test(name)) {
      this.message.error('只支持 mp4 / webm / mov 格式');
      return false;
    }
    this.selectedFile.set(raw);
    if (!this.uploadForm().title) {
      this.patchUpload({ title: name.replace(/\.[^.]+$/, '') });
    }
    return false; // 阻止 ng-zorro 自动上传，交给下面的 submit
  };

  submitUpload(): void {
    const courseId = this.selectedCourseId();
    const file = this.selectedFile();
    if (!courseId || !file) {
      this.message.warning('请选择视频文件');
      return;
    }

    this.uploading.set(true);
    this.uploader
      .upload({
        courseId,
        file,
        title: this.uploadForm().title || undefined,
        chapterId: this.uploadForm().chapterId,
        knowledgeResourceId: this.uploadForm().knowledgeResourceId,
        description: this.uploadForm().description,
        sortOrder: this.videos().length,
      })
      .then(() => {
        this.uploading.set(false);
        this.isUploadOpen.set(false);
        this.message.success('上传成功');
        this.loadList();
      })
      .catch(err => {
        this.uploading.set(false);
        this.message.error(err?.error?.error?.message || '上传失败');
      });
  }

  // ── 打点 ─────────────────────────────────────────────────────

  openEditor(video: CourseVideoDto): void {
    this.service.get(video.id!).subscribe({
      next: full => {
        this.editing.set(full);
        this.editingMarks.set(full.marks ?? []);
      },
      error: () => this.message.error('加载视频失败'),
    });
  }

  addMark(payload: { pointSec: number; label: string }): void {
    const video = this.editing();
    if (!video) return;
    this.service.saveMark({ courseVideoId: video.id!, pointSec: payload.pointSec, label: payload.label }).subscribe({
      next: () => {
        this.message.success('已添加标记');
        this.reloadMarks();
      },
      error: err => this.message.error(err?.error?.error?.message || '添加失败'),
    });
  }

  removeMark(markId: string): void {
    this.service.deleteMark(markId).subscribe({
      next: () => {
        this.message.success('已删除标记');
        this.reloadMarks();
      },
      error: err => this.message.error(err?.error?.error?.message || '删除失败'),
    });
  }

  onDurationDetected(seconds: number): void {
    const video = this.editing() ?? this.videos().find(v => v.durationSeconds === 0);
    if (!video?.id) return;
    this.service.updateDuration(video.id, seconds).subscribe({
      next: () => {
        const editing = this.editing();
        if (editing) this.editing.set({ ...editing, durationSeconds: seconds });
        this.loadList();
      },
      error: () => undefined,
    });
  }

  private reloadMarks(): void {
    const video = this.editing();
    if (!video) return;
    this.service.getMarks(video.id!).subscribe({
      next: rows => this.editingMarks.set(rows ?? []),
      error: () => undefined,
    });
  }

  // ── 排序 / 删除 ───────────────────────────────────────────────

  move(video: CourseVideoDto, delta: number): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    const list = [...this.videos()];
    const index = list.findIndex(x => x.id === video.id);
    const target = index + delta;
    if (index < 0 || target < 0 || target >= list.length) return;

    const [moved] = list.splice(index, 1);
    list.splice(target, 0, moved);

    this.service.reorder(courseId, list.map(x => x.id!)).subscribe({
      next: () => this.loadList(),
      error: err => this.message.error(err?.error?.error?.message || '排序失败'),
    });
  }

  removeVideo(video: CourseVideoDto): void {
    this.modal.confirm({
      nzTitle: '删除视频',
      nzContent: `会同时删除上传的文件与全部时间轴标记，确定删除「${video.title}」吗？`,
      nzOkDanger: true,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.service.delete(video.id!).subscribe({
            next: () => {
              this.message.success('已删除');
              this.loadList();
              resolve();
            },
            error: err => {
              this.message.error(err?.error?.error?.message || '删除失败');
              reject(err);
            },
          });
        }),
    });
  }

  timeText(sec?: number): string {
    const safe = Math.max(0, Math.floor(sec ?? 0));
    return `${Math.floor(safe / 60)
      .toString()
      .padStart(2, '0')}:${(safe % 60).toString().padStart(2, '0')}`;
  }
}
