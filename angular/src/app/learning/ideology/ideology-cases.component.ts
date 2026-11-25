import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PermissionService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { ChapterService } from '../../proxy/courses/chapter.service';
import type { ChapterDto } from '../../proxy/courses/dtos/models';
import { CourseService } from '../../proxy/courses/course.service';
import { CourseContextService } from '../course-context.service';
import {
  CreateUpdateIdeologyCase,
  IDEOLOGY_SOURCE_LABEL,
  IDEOLOGY_STATUS_LABEL,
  IDEOLOGY_USAGE_LABEL,
  IdeologyCaseContent,
  IdeologyCaseDto,
  IdeologyCaseStatus,
  IdeologyElementDto,
  IdeologyService,
  IdeologySourceType,
  IdeologyUsageMethod,
} from './ideology.service';

interface CourseOption {
  id: string;
  title?: string;
}

/**
 * 思政案例库（插件式模块的主页面）。
 *
 * <p>案例**强绑课程**（可再绑章节），不做审核：老师保存即发布，也可存草稿。
 * 案例正文（`contentJson.brief`）是核心内容 —— 列表可「预览」，编辑时给足空间。</p>
 *
 * <p>出处（`sourceText`）**选填**：选定章节后自动填成章节名，老师可改可清空。</p>
 */
@Component({
  selector: 'app-ideology-cases',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzTableModule,
    NzButtonModule,
    NzModalModule,
    NzDrawerModule,
    NzInputModule,
    NzSelectModule,
    NzDatePickerModule,
    NzSwitchModule,
    NzTagModule,
    NzIconModule,
    NzSpaceModule,
    NzPopconfirmModule,
    NzTooltipModule,
    NzEmptyModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ideology-cases.component.html',
  styleUrls: ['./ideology-pages.scss'],
})
export class IdeologyCasesComponent implements OnInit {
  private readonly service = inject(IdeologyService);
  private readonly courseService = inject(CourseService);
  private readonly chapterService = inject(ChapterService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly permission = inject(PermissionService);

  readonly statusLabel = IDEOLOGY_STATUS_LABEL;
  readonly sourceLabel = IDEOLOGY_SOURCE_LABEL;
  readonly usageLabel = IDEOLOGY_USAGE_LABEL;
  readonly statuses = [IdeologyCaseStatus.Draft, IdeologyCaseStatus.Published, IdeologyCaseStatus.Archived];
  readonly sources = Object.values(IdeologySourceType).filter(v => typeof v === 'number') as number[];
  readonly usages = Object.values(IdeologyUsageMethod).filter(v => typeof v === 'number') as number[];

  readonly courses = signal<CourseOption[]>([]);
  readonly courseId = signal<string | null>(null);
  readonly chapters = signal<ChapterDto[]>([]);
  readonly elements = signal<IdeologyElementDto[]>([]);
  readonly rows = signal<IdeologyCaseDto[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);

  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  readonly filterChapterId = signal<string | null>(null);
  readonly filterElementId = signal<string | null>(null);
  readonly filterStatus = signal<IdeologyCaseStatus | null>(null);
  readonly keyword = signal('');

  readonly canEdit = computed(() => this.permission.getGrantedPolicy('KnowledgeHub.CourseIdeology.Edit'));
  readonly canGenerate = computed(() => this.permission.getGrantedPolicy('KnowledgeHub.CourseIdeology.AiGenerate'));

  // ── 案例编辑弹窗 ──
  readonly editorVisible = signal(false);
  readonly saving = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly form = signal({
    title: '',
    chapterId: null as string | null,
    brief: '',
    sourceType: IdeologySourceType.Textbook as IdeologySourceType,
    sourceText: '',
    sourceDate: null as Date | null,
    usageMethod: IdeologyUsageMethod.Import as IdeologyUsageMethod,
    elementIds: [] as string[],
    publish: true,
    aiGenerated: false,
    aiMaterial: null as string | null,
  });

  // ── 预览抽屉 ──
  readonly previewVisible = signal(false);
  readonly previewItem = signal<IdeologyCaseDto | null>(null);

  // ── AI 生成弹窗 ──
  readonly aiVisible = signal(false);
  readonly aiGenerating = signal(false);
  readonly aiForm = signal({
    chapterId: null as string | null,
    elementIds: [] as string[],
    material: '',
    usageMethod: IdeologyUsageMethod.Import as IdeologyUsageMethod,
    extraPrompt: '',
  });
  readonly aiWarnings = signal<string[]>([]);

  ngOnInit(): void {
    const fromQuery = this.route.snapshot.queryParamMap.get('courseId');
    const initial = fromQuery || this.courseContext.currentCourseId();
    this.loadCourses(initial);
  }

  private loadCourses(initialCourseId: string | null): void {
    this.courseService.getMyCourses({ maxResultCount: 200, skipCount: 0 }).subscribe({
      next: list => {
        this.courses.set((list.items || []).map(c => ({ id: c.id!, title: c.title })));
        const target =
          initialCourseId && this.courses().some(c => c.id === initialCourseId)
            ? initialCourseId
            : this.courses()[0]?.id ?? null;
        if (target) {
          this.selectCourse(target);
        }
      },
      error: () => this.courses.set([]),
    });
  }

  selectCourse(courseId: string): void {
    this.courseId.set(courseId);
    this.courseContext.setCourse(courseId);
    this.filterChapterId.set(null);
    this.filterElementId.set(null);
    this.pageIndex.set(1);

    this.chapterService.getChaptersByCourse(courseId).subscribe({
      next: list => this.chapters.set(list || []),
      error: () => this.chapters.set([]),
    });

    this.service.getElements(true).subscribe({
      next: list => this.elements.set(list || []),
      error: () => this.elements.set([]),
    });

    this.reload();
  }

  onCourseChange(courseId: string): void {
    this.selectCourse(courseId);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { courseId },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  reload(): void {
    const courseId = this.courseId();
    if (!courseId) {
      this.rows.set([]);
      this.total.set(0);
      return;
    }

    this.loading.set(true);
    this.service
      .getCases({
        courseId,
        chapterId: this.filterChapterId(),
        elementId: this.filterElementId(),
        status: this.filterStatus(),
        filter: this.keyword(),
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
      })
      .subscribe({
        next: (result: PagedResultDto<IdeologyCaseDto>) => {
          this.rows.set(result.items || []);
          this.total.set(result.totalCount || 0);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('读取思政案例失败');
        },
      });
  }

  onFilterChange(): void {
    this.pageIndex.set(1);
    this.reload();
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
    this.reload();
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageIndex.set(1);
    this.reload();
  }

  // ── 案例编辑 ────────────────────────────────────────────

  openCreate(): void {
    this.editingId.set(null);
    this.form.set({
      title: '',
      chapterId: this.filterChapterId(),
      brief: '',
      sourceType: IdeologySourceType.Textbook,
      sourceText: this.chapterSourceText(this.filterChapterId()),
      sourceDate: null,
      usageMethod: IdeologyUsageMethod.Import,
      elementIds: this.filterElementId() ? [this.filterElementId()!] : [],
      publish: true,
      aiGenerated: false,
      aiMaterial: null,
    });
    this.editorVisible.set(true);
  }

  openEdit(item: IdeologyCaseDto): void {
    const content = IdeologyService.parseContent(item.contentJson);
    this.editingId.set(item.id);
    this.form.set({
      title: item.title,
      chapterId: item.chapterId ?? null,
      brief: content.brief ?? '',
      sourceType: item.sourceType,
      sourceText: item.sourceText ?? '',
      sourceDate: item.sourceDate ? new Date(item.sourceDate) : null,
      usageMethod: item.usageMethod,
      elementIds: [...(item.elementIds || [])],
      publish: item.status === IdeologyCaseStatus.Published,
      aiGenerated: item.aiGenerated,
      aiMaterial: item.aiMaterial ?? null,
    });
    this.editorVisible.set(true);
  }

  patchForm(patch: Record<string, unknown>): void {
    this.form.set({ ...this.form(), ...patch } as any);
  }

  /**
   * 选章节时自动带出「出处」：老师最常给的出处就是教材章节名。
   * 只在不覆盖老师手写内容的前提下填（当前出处为空或仍是上一次自动带入的章节名）。
   */
  onFormChapterChange(chapterId: string | null): void {
    const previousChapterTitle = this.chapterSourceText(this.form().chapterId);
    const currentSource = (this.form().sourceText || '').trim();
    const shouldAutoFill = !currentSource || currentSource === previousChapterTitle;

    this.patchForm({
      chapterId,
      sourceText: shouldAutoFill ? this.chapterSourceText(chapterId) : this.form().sourceText,
    });
  }

  private chapterSourceText(chapterId: string | null): string {
    if (!chapterId) return '';
    return this.chapters().find(c => c.id === chapterId)?.title ?? '';
  }

  save(): void {
    const courseId = this.courseId();
    if (!courseId) return;

    const f = this.form();
    if (!f.title.trim()) {
      this.message.warning('请填写案例标题');
      return;
    }
    if (!f.brief.trim()) {
      this.message.warning('请填写案例文本（这是案例的核心内容）');
      return;
    }

    const content: IdeologyCaseContent = { brief: f.brief };
    const chapter = this.chapters().find(c => c.id === f.chapterId);
    const body: CreateUpdateIdeologyCase = {
      courseId,
      chapterId: f.chapterId,
      chapterTitle: chapter?.title ?? null,
      title: f.title.trim(),
      contentJson: IdeologyService.stringifyContent(content),
      sourceType: f.sourceType,
      // 出处选填：留空时后端会用章节名兜底。
      sourceText: (f.sourceText || '').trim(),
      sourceDate: f.sourceDate ? f.sourceDate.toISOString() : null,
      usageMethod: f.usageMethod,
      elementIds: f.elementIds,
      aiGenerated: f.aiGenerated,
      aiMaterial: f.aiMaterial,
      publish: f.publish,
    };

    this.saving.set(true);
    const id = this.editingId();
    const request = id ? this.service.updateCase(id, body) : this.service.createCase(body);
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.editorVisible.set(false);
        this.message.success(id ? '已保存' : '已创建');
        this.reload();
      },
      error: (err: any) => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '保存失败');
      },
    });
  }

  publish(item: IdeologyCaseDto): void {
    this.service.publishCase(item.id).subscribe({
      next: () => {
        this.message.success('已发布');
        this.reload();
      },
      error: () => this.message.error('发布失败'),
    });
  }

  archive(item: IdeologyCaseDto): void {
    this.service.archiveCase(item.id).subscribe({
      next: () => {
        this.message.success('已下架');
        this.reload();
      },
      error: () => this.message.error('下架失败'),
    });
  }

  remove(item: IdeologyCaseDto): void {
    this.service.deleteCase(item.id).subscribe({
      next: () => {
        this.message.success('已删除');
        this.reload();
      },
      error: () => this.message.error('删除失败'),
    });
  }

  // ── 预览 ────────────────────────────────────────────────

  openPreview(item: IdeologyCaseDto): void {
    this.previewItem.set(item);
    this.previewVisible.set(true);
  }

  // ── AI 生成 ─────────────────────────────────────────────

  openAi(): void {
    this.aiWarnings.set([]);
    this.aiForm.set({
      chapterId: this.filterChapterId(),
      elementIds: this.filterElementId() ? [this.filterElementId()!] : [],
      material: '',
      usageMethod: IdeologyUsageMethod.Import,
      extraPrompt: '',
    });
    this.aiVisible.set(true);
  }

  patchAiForm(patch: Record<string, unknown>): void {
    this.aiForm.set({ ...this.aiForm(), ...patch } as any);
  }

  generateDraft(): void {
    const courseId = this.courseId();
    if (!courseId) return;
    const f = this.aiForm();
    if (f.material.trim().length < 20) {
      this.message.warning('请粘贴素材要点（不少于 20 字），AI 不会凭空编造事实');
      return;
    }

    this.aiGenerating.set(true);
    this.service
      .generateDraft({
        courseId,
        chapterId: f.chapterId,
        elementIds: f.elementIds,
        material: f.material.trim(),
        usageMethod: f.usageMethod,
        extraPrompt: f.extraPrompt?.trim() || null,
      })
      .subscribe({
        next: draft => {
          this.aiGenerating.set(false);
          this.aiVisible.set(false);
          this.aiWarnings.set(draft.warnings || []);

          const content = IdeologyService.parseContent(draft.contentJson);
          this.editingId.set(null);
          this.form.set({
            title: draft.title || '',
            chapterId: f.chapterId,
            brief: content.brief ?? '',
            sourceType: IdeologySourceType.AiGenerated,
            sourceText: draft.sourceText || this.chapterSourceText(f.chapterId),
            sourceDate: null,
            usageMethod: f.usageMethod,
            elementIds: [...f.elementIds],
            // AI 产物默认存草稿：必须人工核验后再发布。
            publish: false,
            aiGenerated: true,
            aiMaterial: f.material.trim().slice(0, 2000),
          });
          this.editorVisible.set(true);
          this.message.info('AI 草稿已生成，请核验事实与出处后再发布');
        },
        error: (err: any) => {
          this.aiGenerating.set(false);
          this.message.error(err?.error?.error?.message || 'AI 生成失败');
        },
      });
  }

  // ── 展示辅助 ────────────────────────────────────────────

  statusColor(status: IdeologyCaseStatus): string {
    switch (status) {
      case IdeologyCaseStatus.Published:
        return 'green';
      case IdeologyCaseStatus.Draft:
        return 'orange';
      default:
        return 'default';
    }
  }

  chapterPath(item: IdeologyCaseDto): string {
    if (!item.chapterId) return '课程通用';
    const chapter = this.chapters().find(c => c.id === item.chapterId);
    return chapter?.title ?? item.chapterTitle ?? '章节已删除';
  }

  /** 列表里的正文摘要（案例文本前 80 字）。 */
  textPreview(item: IdeologyCaseDto): string {
    return IdeologyService.caseText(item.contentJson ?? null, 80);
  }

  previewText(item: IdeologyCaseDto | null): string {
    return item ? IdeologyService.caseText(item.contentJson ?? null, 4000) : '';
  }
}
