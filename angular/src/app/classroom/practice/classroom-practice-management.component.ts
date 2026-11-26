import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
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
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ClassroomPracticeService } from '../../proxy/classroom/classroom-practice.service';
import type {
  PracticeSessionDto,
  CreatePracticeSessionInput,
  PracticeStatsDto,
} from '../../proxy/classroom/dtos/models';
import { ClassroomPracticeStatus } from '../../proxy/classroom/enums/classroom-practice-status.enum';
import type { CourseDto, ChapterDto } from '../../proxy/courses/dtos/models';
import type { KnowledgeResourceDto } from '../../proxy/courses/dtos/models';
import { ChapterService } from '../../proxy/courses/chapter.service';
import { KnowledgeResourceService } from '../../proxy/courses/knowledge-resource.service';
import { MyCoursesService } from '../../learning/shared/my-courses.service';
import { CourseContextService } from '../../learning/course-context.service';
import {
  DEFAULT_PRACTICE_QUESTION_COUNT,
  PRACTICE_STATUS_LABEL,
  SHORT_URL_PREFIX,
  STATUS_TAG_COLOR,
  buildShortUrl,
} from '../classroom-labels';

/**
 * 课堂练习管理：选范围 → 快照抽题 → 短链下发 → 实时统计（提交率 / 逐题正确率 / 未提交名单）。
 * 抽到的题在场次内固定，全班做的是同一套题 —— 这是看统计的前提。
 */
@Component({
  selector: 'app-classroom-practice-management',
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
    NzInputNumberModule,
    NzDrawerModule,
    NzProgressModule,
  ],
  templateUrl: './classroom-practice-management.component.html',
  styleUrls: ['./classroom-practice-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClassroomPracticeManagementComponent implements OnInit {
  private readonly myCourses = inject(MyCoursesService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(ClassroomPracticeService);
  private readonly chapterService = inject(ChapterService);
  private readonly knowledgeService = inject(KnowledgeResourceService);
  private readonly message = inject(NzMessageService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly sessions = signal<PracticeSessionDto[]>([]);
  readonly loading = signal(false);

  readonly chapters = signal<ChapterDto[]>([]);
  readonly knowledgeOptions = signal<{ id: string; label: string }[]>([]);
  readonly selectedKpIds = signal<string[]>([]);

  readonly isCreateOpen = signal(false);
  readonly saving = signal(false);
  readonly form = signal<CreatePracticeSessionInput>(this.emptyForm());

  readonly stats = signal<PracticeStatsDto | null>(null);
  readonly statsSession = signal<PracticeSessionDto | null>(null);
  readonly statsLoading = signal(false);

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
      this.sessions.set([]);
      this.chapters.set([]);
      this.knowledgeOptions.set([]);
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
    this.service.getList(courseId, 0, 100).subscribe({
      next: res => {
        this.sessions.set(res.items ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.sessions.set([]);
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

  /** 知识点下拉：拉树后扁平化（带缩进），抽题可按知识点收窄。 */
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

  openCreate(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    this.selectedKpIds.set([]);
    this.form.set({ ...this.emptyForm(), courseId });
    this.isCreateOpen.set(true);
  }

  patchForm(patch: Partial<CreatePracticeSessionInput>): void {
    this.form.set({ ...this.form(), ...patch });
  }

  save(): void {
    const model = this.form();
    if (!model.title?.trim()) {
      this.message.warning('请填写练习标题');
      return;
    }
    const input: CreatePracticeSessionInput = {
      ...model,
      knowledgeResourceIds: this.selectedKpIds().length ? this.selectedKpIds() : null,
    };

    this.saving.set(true);
    this.service.create(input).subscribe({
      next: () => {
        this.saving.set(false);
        this.isCreateOpen.set(false);
        this.message.success('已发起课堂练习');
        this.loadList();
      },
      error: err => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '发起失败');
      },
    });
  }

  closeSession(session: PracticeSessionDto): void {
    this.service.close(session.id!).subscribe({
      next: () => {
        this.message.success('已结束');
        this.loadList();
      },
      error: err => this.message.error(err?.error?.error?.message || '操作失败'),
    });
  }

  removeSession(session: PracticeSessionDto): void {
    if (!confirm(`删除「${session.title}」及其全部作答记录？`)) return;
    this.service.delete(session.id!).subscribe({
      next: () => {
        this.message.success('已删除');
        this.loadList();
      },
      error: err => this.message.error(err?.error?.error?.message || '删除失败'),
    });
  }

  openStats(session: PracticeSessionDto): void {
    this.statsSession.set(session);
    this.stats.set(null);
    this.statsLoading.set(true);
    this.service.getStats(session.id!).subscribe({
      next: data => {
        this.stats.set(data);
        this.statsLoading.set(false);
      },
      error: () => this.statsLoading.set(false),
    });
  }

  refreshStats(): void {
    const session = this.statsSession();
    if (session) this.openStats(session);
  }

  copyLink(session: PracticeSessionDto): void {
    const url = buildShortUrl(SHORT_URL_PREFIX.practice, session.token ?? '');
    navigator.clipboard?.writeText(url).then(
      () => this.message.success('链接已复制'),
      () => this.message.warning('复制失败，请手动选择文本'),
    );
  }

  shortUrl(session: PracticeSessionDto): string {
    return buildShortUrl(SHORT_URL_PREFIX.practice, session.token ?? '');
  }

  statusLabel(session: PracticeSessionDto): string {
    return PRACTICE_STATUS_LABEL[(session.status ?? 0) as ClassroomPracticeStatus] ?? '—';
  }

  statusColor(session: PracticeSessionDto): string {
    return STATUS_TAG_COLOR[this.statusLabel(session)] ?? 'default';
  }

  percent(value?: number | null): number {
    return Math.round((value ?? 0) * 100);
  }

  private emptyForm(): CreatePracticeSessionInput {
    return {
      courseId: '',
      title: '',
      chapterId: null,
      knowledgeResourceIds: null,
      questionCount: DEFAULT_PRACTICE_QUESTION_COUNT,
    };
  }
}
