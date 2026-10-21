import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { catchError, firstValueFrom, Observable, Subscription, switchMap, of } from 'rxjs';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';

import { ConfigStateService } from '@abp/ng.core';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { MasteryBarComponent } from '../../learning-analytics/components/mastery-bar/mastery-bar.component';
import { AnalysisKpiComponent } from '../../learning-analytics/components/analysis-kpi/analysis-kpi.component';
import { LearningAnalyticsService } from '../../learning-analytics/services/learning-analytics.service';

import {
  AnalysisReportFile,
  BatchStudentRow,
  ClassWeaknessDto,
  CourseLinkConfig,
  CourseLinkItem,
  CourseStudentDto,
  MasteryLevel,
  OcrPipelineStep,
  PaperAnalysisOverview,
  PaperBatchItem,
  PaperBatchStatus,
  PaperGeneratorConfig,
  PaperScriptRow,
  PaperTemplate,
  PaperUploadImage,
  PaperUploadPayload,
  PaperUploadStatus,
  PrerequisiteMapping,
  RecommendationEvaluation,
  RecommendationWeights,
  StudentReportPreview,
  TeachingInsightDto,
} from '../../learning-analytics/models/learning-analytics.models';

/** 页面顶部分区：试卷分析（含试卷图片生成 + 上传）/ 分析报告 / 课程联动 */
type TeacherTab = 'paper' | 'report' | 'linkage';

/** 教师端「智学分析」：纸质试卷分析 / 分析报告 / 课程联动设置 */
@Component({
  selector: 'app-learning-analysis',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzEmptyModule,
    MasteryBarComponent,
    AnalysisKpiComponent,
  ],
  templateUrl: './learning-analysis.component.html',
  styleUrls: ['./learning-analysis.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LearningAnalysisComponent implements OnInit, OnDestroy {
  private readonly service = inject(LearningAnalyticsService);
  private readonly courseService = inject(CourseService);
  private readonly configState = inject(ConfigStateService);

  readonly PaperBatchStatus = PaperBatchStatus;
  readonly PaperUploadStatus = PaperUploadStatus;

  readonly tabs: { key: TeacherTab; label: string; icon: string }[] = [
    { key: 'paper', label: '试卷分析', icon: 'file-done' },
    { key: 'report', label: '分析报告', icon: 'bar-chart' },
    { key: 'linkage', label: '课程联动设置', icon: 'deployment-unit' },
  ];

  readonly activeTab = signal<TeacherTab>('paper');

  /**
   * 「前置知识点映射」暂不展示（UI 先行下线，功能与后端保留）。
   * 改为 true 即可恢复。
   */
  readonly showPrerequisiteMapping = false;

  // ── 试卷 ──
  readonly paperLoading = signal(false);
  readonly paper = signal<PaperAnalysisOverview | null>(null);
  readonly batches = computed(() => this.paper()?.batches ?? []);
  readonly selectedBatch = signal<PaperBatchItem | null>(null);
  readonly ocrSteps = signal<OcrPipelineStep[]>([]);
  readonly runningOcr = signal(false);
  readonly uploading = signal(false);
  readonly dragActive = signal(false);
  readonly uploadImages = signal<PaperUploadImage[]>([]);
  readonly ocrHint = signal('');

  // 上传表单
  courses = signal<CourseDto[]>([]);
  /** 只看我教的课程（按 Course.TeacherId = 当前用户过滤；为空时自动回退全部） */
  onlyMyCourses = false;
  readonly myCoursesEmpty = signal(false);
  uploadCourseId = '';
  uploadTitle = '';
  uploadDate = new Date().toISOString().slice(0, 10);
  /** 一次只上传一份学生试卷：单个文件 */
  pendingFile: File | null = null;
  readonly showUploadForm = signal(false);
  /** 该课程的选课学生（上传时必须指定归属学生） */
  readonly students = signal<CourseStudentDto[]>([]);
  readonly studentsLoading = signal(false);
  uploadStudentId = '';
  /** 当前批次已上传试卷的学生 */
  readonly batchStudents = signal<BatchStudentRow[]>([]);
  /** 正在把 PDF 渲染成图片 */
  readonly rendering = signal(false);
  readonly renderHint = signal('');

  // 判定线
  thresholdWeakPercent = 60;
  thresholdOkPercent = 75;
  readonly thresholdSaving = signal(false);
  readonly thresholdNotice = signal('');

  // 校对
  readonly scriptsLoading = signal(false);
  readonly scripts = signal<PaperScriptRow[]>([]);
  readonly editingScript = signal<PaperScriptRow | null>(null);
  readonly confirmPanelOpen = signal(false);

  /** 删除中标记：`script:{id}` / `batch:{id}`，用于按钮禁用与文案 */
  readonly deleting = signal('');
  readonly deleteNotice = signal('');

  // ── 试卷生成器（生成可 OCR 扫描的试卷样张）──
  /** 展开「生成试卷」面板 */
  readonly paperGenOpen = signal(false);
  readonly paperGenLoading = signal(false);
  readonly paperGenConfig = signal<PaperGeneratorConfig | null>(null);
  readonly paperTemplate = signal<PaperTemplate | null>(null);
  readonly paperPngBlob = signal<Blob | null>(null);
  readonly paperGenNotice = signal('');

  /** 生成参数（普通字段，配合 ngModel 双向绑定） */
  genTitle = '';
  genQuestionCount = 6;
  genFullScore = 5;
  genRandomScore = true;
  genIncludeAnswer = true;
  genClassName = '';
  genExamDate = new Date().toISOString().slice(0, 10);
  /** 随机种子（空 = 随机；同种子出同一份卷） */
  genSeed: number | null = null;
  /** 考生：默认取上传表单里选中的学生 */
  genStudentId = '';

  // ── 班级薄弱点 / 教学成果 ──
  readonly analysisLoading = signal(false);
  readonly classWeakness = signal<ClassWeaknessDto | null>(null);
  readonly teachingInsight = signal<TeachingInsightDto | null>(null);
  readonly pushing = signal(false);
  readonly pushedNotice = signal('');
  /** 报告导出 / 预览 */
  readonly exporting = signal('');
  readonly preview = signal<StudentReportPreview | null>(null);
  readonly previewLoading = signal(false);
  previewStudentId = '';
  readonly downloadNotice = signal('');
  /** 归档文件列表 */
  readonly reportFiles = signal<AnalysisReportFile[]>([]);
  readonly archiving = signal('');
  /** 推荐效果评估 */
  readonly evaluation = signal<RecommendationEvaluation | null>(null);
  readonly evaluating = signal(false);
  /** 当前生效权重 */
  readonly recWeights = signal<RecommendationWeights | null>(null);
  readonly applyingWeights = signal(false);
  readonly weightNotice = signal('');
  /** 是否已启用 OSS 直传（上传时按需探测） */
  readonly ossUploadEnabled = signal(false);
  /** OCR 能力（图片是否可用、PDF 是否可直传），首次上传时探测并缓存 */
  private ocrCapabilities: { image: boolean; pdfDirect: boolean; pdfModel?: string; note?: string | null } | null = null;

  // ── 课程联动 ──
  readonly linkageLoading = signal(false);
  readonly courseLink = signal<CourseLinkConfig | null>(null);
  linkageCourseId = '';
  linkPredecessorId = '';
  linkSuccessorId = '';

  // ── AI 任务 ──
  readonly aiBusy = signal('');
  readonly aiMessage = signal('');

  private pollSub?: Subscription;

  readonly targetLine = computed(() => this.classWeakness()?.targetLine ?? 0.75);

  /**
   * 是否可以提交上传。
   * 注意：这里是方法而不是 computed —— 课程 / 批次名 / 学生 / 文件都是普通字段
   * （配合 ngModel 双向绑定），computed 只跟踪 signal，普通字段变化不会让它失效，
   * 会导致「上传并识别」按钮始终处于 disabled。
   */
  canUpload(): boolean {
    return (
      !this.rendering() &&
      !this.uploading() &&
      !!this.uploadCourseId &&
      !!this.uploadTitle.trim() &&
      !!this.uploadStudentId &&
      !!this.pendingFile
    );
  }

  /** 待校对试卷数 */
  unconfirmedCount(): number {
    return this.scripts().filter(s => !s.confirmed).length;
  }

  /** 识别失败的图片数 */
  failedImageCount(): number {
    return this.uploadImages().filter(i => i.status === PaperUploadStatus.Failed).length;
  }

  /** 已处理（完成 + 失败）的图片数 */
  readonly uploadImagesProcessed = computed(
    () => this.uploadImages().filter(i => i.status === PaperUploadStatus.Done || i.status === PaperUploadStatus.Failed).length
  );

  ngOnInit(): void {
    this.loadCourses();
    this.loadPaperOverview();
    this.loadRecommendationWeights();
    this.restorePaperGenConfig();
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  /** 当前登录用户 Id（用于「我的课程」过滤） */
  private currentUserId(): string | undefined {
    const user = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    return (user?.['id'] as string) || undefined;
  }

  onMyCoursesToggle(value: boolean): void {
    this.onlyMyCourses = value;
    this.loadCourses();
  }

  private loadCourses(): void {
    // 教师端需要看到本院校内的课程（不限于自己创建），优先 getList，失败或为空时回退到已发布课程；
    // 勾选「只看我教的课程」时按 teacherId 过滤，若结果为空则自动回退全部课程并提示
    const teacherId = this.onlyMyCourses ? this.currentUserId() : undefined;
    this.courseService
      .getList({ skipCount: 0, maxResultCount: 200, teacherId } as never)
      .pipe(
        catchError(() => this.courseService.getPublished({ skipCount: 0, maxResultCount: 200 } as never))
      )
      .subscribe({
        next: res => {
          let items = res?.items ?? [];
          if (this.onlyMyCourses && items.length === 0) {
            // 课程未维护授课教师时，回退到全部课程，避免教师看不到任何数据
            this.myCoursesEmpty.set(true);
            this.onlyMyCourses = false;
            this.loadCourses();
            return;
          }

          this.myCoursesEmpty.set(false);
          this.courses.set(items);
          if (items.length && !this.uploadCourseId) {
            this.uploadCourseId = items[0].id;
            this.syncTitleWithCourse(items[0].id);
            this.loadStudents(items[0].id);
          }
          if (items.length && !this.linkageCourseId) {
            this.linkageCourseId = items[0].id;
            this.loadCourseLink(items[0].id);
          }
        },
        error: () => this.courses.set([]),
      });
  }

  loadPaperOverview(): void {
    this.paperLoading.set(true);
    this.service.getPaperOverview().subscribe({
      next: res => {
        this.paper.set(res);
        this.paperLoading.set(false);
        const first = res.batches?.[0];
        if (first && !this.selectedBatch()) {
          this.selectedBatch.set(first);
          this.loadReportFiles(first.id);
        }
      },
      error: () => this.paperLoading.set(false),
    });
  }

  setTab(key: TeacherTab): void {
    this.activeTab.set(key);
    // 分析报告：需要时加载当前批次的分析结果（班级薄弱点 + 教学成果）
    if (key === 'report' && !this.classWeakness() && !this.teachingInsight()) {
      this.loadAnalysisForSelectedBatch();
    }
    if (key === 'linkage' && !this.courseLink() && this.linkageCourseId) {
      this.loadCourseLink(this.linkageCourseId);
    }
  }

  // ══════════════ 上传 + OCR ══════════════

  toggleUploadForm(): void {
    this.showUploadForm.update(v => !v);
    this.ocrSteps.set([]);
    this.ocrHint.set('');
  }

  onDragOver(ev: DragEvent): void {
    ev.preventDefault();
    this.dragActive.set(true);
  }

  onDragLeave(ev: DragEvent): void {
    ev.preventDefault();
    this.dragActive.set(false);
  }

  onDrop(ev: DragEvent): void {
    ev.preventDefault();
    this.dragActive.set(false);
    const files = Array.from(ev.dataTransfer?.files ?? []);
    // 一次一份试卷：只取第一个文件
    if (files.length) {
      this.selectFile(files[0]);
    }
  }

  onFilesSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    if (files.length) {
      this.selectFile(files[0]);
    }
    input.value = '';
  }

  /** 选择一份试卷文件（一次只处理一份学生试卷） */
  selectFile(file: File): void {
    const allowed = ['image/jpeg', 'image/png', 'image/webp', 'image/bmp', 'application/pdf'];
    if (!(allowed.includes(file.type) || /\.(jpe?g|png|webp|bmp|pdf)$/i.test(file.name))) {
      this.ocrHint.set('不支持的文件类型（仅支持 JPG / PNG / WEBP / BMP / PDF）');
      return;
    }
    this.pendingFile = file;
    this.ocrHint.set('');
  }

  isPdf(file: File | null | undefined): boolean {
    if (!file) return false;
    return file.type === 'application/pdf' || /\.pdf$/i.test(file.name);
  }

  removePendingFile(): void {
    this.pendingFile = null;
  }

  /** 切换关联课程 → 重新加载该课程的选课学生 */
  onUploadCourseChange(courseId: string): void {
    this.uploadCourseId = courseId;
    // 试卷批次名称随课程动态变化：{课程名} 期末试卷
    this.syncTitleWithCourse(courseId);
    this.loadStudents(courseId);
  }

  /** 批次名称 / 生成器标题跟随所选课程（教师可再手动改） */
  private syncTitleWithCourse(courseId: string): void {
    const course = this.courses().find(c => c.id === courseId);
    if (!course) return;
    this.uploadTitle = `${course.title} 期末试卷`;
    // 生成器标题优先用服务端配置的模板（支持 {course} 占位）
    const template = this.paperGenConfig()?.titleTemplate;
    this.genTitle = template
      ? template.replace('{course}', course.title)
      : `${course.title} 期末试卷`;
  }

  /** 加载课程的选课学生（教师上传纸质试卷时选择归属学生） */
  loadStudents(courseId: string): void {
    this.students.set([]);
    this.uploadStudentId = '';
    this.batchStudents.set([]);
    if (!courseId) {
      return;
    }

    this.studentsLoading.set(true);
    this.service.getCourseStudents(courseId).subscribe({
      next: rows => {
        this.students.set(rows ?? []);
        this.studentsLoading.set(false);
      },
      error: err => {
        this.students.set([]);
        this.studentsLoading.set(false);
        this.ocrHint.set(this.errorText(err) || '选课学生加载失败');
      },
    });
  }

  /** 当前批次已上传试卷的学生（按学生聚合） */
  loadBatchStudents(batchId: string): void {
    this.service.getPaperUploadImages(batchId).subscribe({
      next: images => {
        const rows = new Map<string, BatchStudentRow>();
        for (const img of images ?? []) {
          const key = img.studentId || img.studentName || 'unknown';
          const current = rows.get(key);
          const failed = img.status === PaperUploadStatus.Failed;
          const done = img.status === PaperUploadStatus.Done;
          rows.set(key, {
            studentId: img.studentId ?? '',
            studentName: img.studentName || '未指定学生',
            studentNumber: img.studentNumber || '',
            fileCount: (current?.fileCount ?? 0) + 1,
            status: failed ? PaperUploadStatus.Failed : done ? PaperUploadStatus.Done : PaperUploadStatus.Processing,
            errorMessage: img.errorMessage ?? current?.errorMessage ?? null,
          });
        }
        this.batchStudents.set([...rows.values()]);
      },
      error: () => this.batchStudents.set([]),
    });
  }

  /** 上传一份学生试卷 → 触发 OCR → 轮询进度 */
  startUpload(): void {
    if (!this.canUpload()) return;

    const course = this.courses().find(c => c.id === this.uploadCourseId);
    const student = this.students().find(s => s.studentId === this.uploadStudentId);
    if (!student) {
      this.ocrHint.set('请先选择这份试卷所属的学生');
      return;
    }

    this.uploading.set(true);
    this.ocrSteps.set([]);
    this.ocrHint.set('');
    this.uploadImages.set([]);

    this.buildUploadPayloads()
      .then(payloads => {
        if (!payloads.length) {
          throw new Error('试卷文件读取失败');
        }
        return firstValueFrom(
          this.service.uploadPaperScript({
            courseId: this.uploadCourseId,
            courseName: course?.title ?? '',
            title: this.uploadTitle.trim(),
            examDate: this.uploadDate,
            studentId: student.studentId,
            studentName: student.studentName,
            studentNumber: student.studentNumber,
            files: payloads,
          })
        );
      })
      .then(res => {
        this.uploading.set(false);
        this.pendingFile = null;
        this.runningOcr.set(true);
        this.pollBatch(res.batchId);
      })
      .catch(err => {
        this.uploading.set(false);
        this.runningOcr.set(false);
        this.ocrHint.set(this.errorText(err) || '上传失败，请检查网络或后端服务');
      });
  }

  /** 解析后端错误信息（ABP 返回 {error:{message}}） */
  private errorText(err: unknown): string {
    const anyErr = err as { error?: { error?: { message?: string }; message?: string }; message?: string };
    return anyErr?.error?.error?.message || anyErr?.error?.message || anyErr?.message || '';
  }

  /**
   * 构造上传载荷：优先把图片/整份 PDF 直传 OSS（只传 URL，请求体小、OCR 时模型按 URL 取图）；
   * OSS 未配置或上传失败时自动回退 Base64；PDF 回退时由浏览器按页渲染成图片。
   */
  private async buildUploadPayloads(): Promise<PaperUploadPayload[]> {
    const file = this.pendingFile;
    if (!file) return [];

    if (!this.ocrCapabilities) {
      try {
        this.ocrCapabilities = await firstValueFrom(this.service.getOcrCapabilities());
      } catch {
        this.ocrCapabilities = { image: false, pdfDirect: false };
      }
    }

    // PDF：优先整份走 OSS 直传（qwen3.5-ocr 直接解析 PDF，无需拆页）
    if (this.isPdf(file)) {
      if (this.ocrCapabilities?.pdfDirect) {
        try {
          const res = await firstValueFrom(this.service.uploadFileToOss(file, file.name));
          if (res?.url) {
            this.ossUploadEnabled.set(true);
            return [{ fileName: file.name, contentType: 'application/pdf', url: res.url, pageIndex: 1 }];
          }
        } catch {
          // OSS 不可用 → 继续走浏览器拆页
        }
      }

      const payloads = await this.pdfToPayloads(file);
      this.ossUploadEnabled.set(payloads.some(p => !!p.url));
      return payloads;
    }

    // 图片：优先 OSS 直传，失败自动回退 Base64
    const item = await this.toPayload(file, file.name, file.type || 'image/jpeg', 1);
    this.ossUploadEnabled.set(item.usedOss);
    return [item.payload];
  }

  /** 单张图片 → OSS URL 或 Base64（OSS 失败自动回退） */
  private async toPayload(
    blob: Blob,
    fileName: string,
    contentType: string,
    pageIndex: number,
    mergeGroup?: string
  ): Promise<{ payload: PaperUploadPayload; usedOss: boolean }> {
    try {
      const res = await firstValueFrom(this.service.uploadImageToOss(blob, fileName));
      if (res?.url) {
        return {
          payload: { fileName, contentType, url: res.url, pageIndex, mergeGroup },
          usedOss: true,
        };
      }
    } catch {
      // OSS 未配置 / 无权限 / 网络异常 → 回退 Base64
    }

    const base64 = await this.blobToBase64(blob);
    return {
      payload: { fileName, contentType, base64, pageIndex, mergeGroup },
      usedOss: false,
    };
  }

  private blobToBase64(blob: Blob): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        const result = String(reader.result ?? '');
        const comma = result.indexOf(',');
        resolve(comma >= 0 ? result.slice(comma + 1) : result);
      };
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(blob);
    });
  }

  /**
   * 把 PDF 逐页渲染成 JPEG 后上传（浏览器端用 pdfjs，避免服务端引入 PDF 渲染原生依赖）。
   * 整份 PDF = 一份学生试卷，因此所有页面共用同一个 mergeGroup，后端会合并为一份试卷。
   */
  private async pdfToPayloads(file: File): Promise<PaperUploadPayload[]> {
    this.rendering.set(true);
    this.renderHint.set(`正在解析 PDF《${file.name}》…`);
    try {
      const pdfjsLib = await import('pdfjs-dist');
      pdfjsLib.GlobalWorkerOptions.workerSrc = 'assets/pdfjs/pdf.worker.min.mjs';

      const data = new Uint8Array(await file.arrayBuffer());
      const doc = await pdfjsLib.getDocument({
        data,
        cMapUrl: 'assets/pdfjs/cmaps/',
        cMapPacked: true,
      }).promise;

      const result: PaperUploadPayload[] = [];
      for (let p = 1; p <= doc.numPages; p++) {
        this.renderHint.set(`正在渲染《${file.name}》第 ${p}/${doc.numPages} 页…`);
        const page = await doc.getPage(p);
        const viewport = page.getViewport({ scale: 2 });
        const canvas = document.createElement('canvas');
        canvas.width = Math.floor(viewport.width);
        canvas.height = Math.floor(viewport.height);
        await page
          .render({ canvasContext: canvas.getContext('2d')!, viewport } as never)
          .promise;

        const blob = await this.canvasToBlob(canvas);
        const pageName = `${file.name}#p${p}`;
        const item = await this.toPayload(blob, pageName, 'image/jpeg', p, file.name);
        result.push(item.payload);
        page.cleanup();
      }

      await doc.destroy();
      return result;
    } finally {
      this.rendering.set(false);
      this.renderHint.set('');
    }
  }

  private canvasToBlob(canvas: HTMLCanvasElement): Promise<Blob> {
    return new Promise((resolve, reject) => {
      canvas.toBlob(
        blob => (blob ? resolve(blob) : reject(new Error('canvas toBlob failed'))),
        'image/jpeg',
        0.9
      );
    });
  }

  /** 轮询 OCR 进度：批次进入「待校对 / 已确认 / 已推送」或所有图片处理完成后停止 */
  private pollBatch(batchId: string): void {
    this.pollSub?.unsubscribe();
    this.ocrSteps.set([
      { key: 'upload', label: '① 上传试卷', status: 'done' },
      { key: 'ocr', label: '② OCR识别每题得分', status: 'running' },
      { key: 'match', label: '③ 题目匹配习题库知识点', status: 'pending' },
      { key: 'report', label: '④ 生成报告并推送', status: 'pending' },
    ]);

    let ticks = 0;
    const maxTicks = 120; // ~5 分钟（2.5s × 120）

    this.pollSub = new Subscription();
    const timer = setInterval(() => {
      ticks++;
      this.service.getPaperBatch(batchId).subscribe({
        next: batch => {
          if (batch) this.selectedBatch.set(batch);
          this.service.getPaperUploadImages(batchId).subscribe(imgs => this.uploadImages.set(imgs));

          const done = batch && batch.status !== PaperBatchStatus.OcrRunning;
          if ((done && (!batch || batch.uploadedCount === 0 || batch.processedCount + batch.failedCount >= batch.uploadedCount)) || ticks >= maxTicks) {
            clearInterval(timer);
            this.finishOcr(batchId, batch, ticks >= maxTicks);
          }
        },
        error: () => {
          if (ticks >= maxTicks) {
            clearInterval(timer);
            this.finishOcr(batchId, null, true);
          }
        },
      });
    }, 2500);

    this.pollSub.add(() => clearInterval(timer));
  }

  private finishOcr(batchId: string, batch: PaperBatchItem | null, timedOut: boolean): void {
    this.runningOcr.set(false);
    this.ocrSteps.set([
      { key: 'upload', label: '① 上传试卷', status: 'done' },
      { key: 'ocr', label: '② OCR识别每题得分', status: timedOut ? 'pending' : 'done' },
      { key: 'match', label: '③ 题目匹配习题库知识点', status: timedOut ? 'pending' : 'done' },
      { key: 'report', label: '④ 生成报告并推送', status: 'pending' },
    ]);

    this.service.getPaperUploadImages(batchId).subscribe(imgs => {
      this.uploadImages.set(imgs);
      const failed = imgs.filter(i => i.status === PaperUploadStatus.Failed).length;
      if (failed > 0) {
        this.ocrHint.set(`${failed} 张图片识别失败，可点「重试失败图片」或检查图片清晰度`);
      } else if (timedOut) {
        this.ocrHint.set('识别耗时较长，已停止等待；可稍后刷新批次状态查看结果');
      } else if (batch) {
        // 识别完成后不再展示逐步骤标签与逐文件明细，只给一行结果
        const names = imgs
          .filter(i => i.status === PaperUploadStatus.Done)
          .map(i => i.studentName || i.fileName)
          .filter(x => !!x);
        const detail = names.length
          ? `（${names.slice(0, 2).join('、')}${names.length > 2 ? ' 等' : ''}）`
          : '';
        this.ocrHint.set(
          `识别完成：${detail}${batch.scriptCount} 份试卷，${batch.needsConfirmCount} 份待校对`
        );
      }
    });

    this.loadPaperOverview();
    this.loadBatchStudents(batchId);
    this.openConfirmForBatchId(batchId);
  }

  /** 重试失败图片（后端会把失败图片重置为待处理并重新入队） */
  retryFailedOcr(): void {
    const batch = this.selectedBatch();
    if (!batch) return;
    this.runningOcr.set(true);
    this.service.runPaperOcr(batch.id).subscribe({
      next: () => this.pollBatch(batch.id),
      error: () => this.runningOcr.set(false),
    });
  }

  // ══════════════ 校对 ══════════════

  openConfirm(batch: PaperBatchItem): void {
    this.selectedBatch.set(batch);
    this.openConfirmForBatchId(batch.id);
  }

  private openConfirmForBatchId(batchId: string): void {
    this.confirmPanelOpen.set(true);
    this.editingScript.set(null);
    this.scriptsLoading.set(true);
    this.service.getPaperScripts(batchId).subscribe({
      next: rows => {
        this.scripts.set(rows);
        this.scriptsLoading.set(false);
      },
      error: () => this.scriptsLoading.set(false),
    });
  }

  closeConfirm(): void {
    this.confirmPanelOpen.set(false);
    this.editingScript.set(null);
  }

  selectScript(script: PaperScriptRow): void {
    this.editingScript.set(script);
  }

  lowConfidenceCount(): number {
    const rows = this.editingScript()?.items ?? [];
    return rows.filter(r => r.ocrConfidence < 0.75 || r.matchStatus !== 'matched').length;
  }

  confirmScript(script: PaperScriptRow): void {
    this.service.confirmPaperScript(script.id, this.scripts()).subscribe({
      next: () => {
        script.confirmed = true;
        this.scripts.set([...this.scripts()]);
        this.editingScript.set({ ...script });
        this.loadPaperOverview();
      },
      error: () => void 0,
    });
  }

  /** 一键确认本批次所有试卷（仅建议在人工抽查后使用） */
  confirmAll(): void {
    const rows = this.scripts().filter(s => !s.confirmed);
    if (!rows.length) return;
    let remaining = rows.length;
    rows.forEach(row => {
      this.service.confirmPaperScript(row.id, this.scripts()).subscribe({
        next: () => {
          row.confirmed = true;
          if (--remaining === 0) {
            this.scripts.set([...this.scripts()]);
            this.loadPaperOverview();
          }
        },
        error: () => {
          remaining--;
        },
      });
    });
  }

  // ══════════════ 删除分析结果 ══════════════

  /** 删除单份试卷的分析结果（识别明细 + 题目得分 + 作答记录 + 来源图片） */
  removeScript(script: PaperScriptRow): void {
    if (this.deleting()) return;
    const confirmed = confirm(
      `删除「${script.studentName}」的分析结果？\n` +
        `将同时删除该份试卷的识别明细、题目得分${script.confirmed ? '与已入库的作答记录' : ''}及来源图片。`
    );
    if (!confirmed) return;

    this.deleting.set(`script:${script.id}`);
    this.deleteNotice.set('');
    this.service.deletePaperScript(script.id).subscribe({
      next: () => {
        this.deleting.set('');
        this.scripts.set(this.scripts().filter(s => s.id !== script.id));
        if (this.editingScript()?.id === script.id) {
          this.editingScript.set(null);
        }
        this.deleteNotice.set(`已删除「${script.studentName}」的分析结果`);
        this.refreshAfterDelete();
      },
      error: err => {
        this.deleting.set('');
        this.deleteNotice.set(this.errorText(err) || '删除失败，请稍后重试');
      },
    });
  }

  /** 删除某个试卷批次的全部分析结果（所有试卷 + 成绩 + 报告 + 归档 + 源图片） */
  removeBatch(batch: PaperBatchItem): void {
    if (this.deleting()) return;
    const confirmed = confirm(
      `删除试卷批次「${batch.title}」的全部分析结果？\n` +
        `将删除该批次下 ${batch.scriptCount} 份试卷的识别结果、成绩、已推送报告与归档文件，且不可恢复。`
    );
    if (!confirmed) return;

    this.deleting.set(`batch:${batch.id}`);
    this.deleteNotice.set('');
    this.service.deletePaperBatch(batch.id).subscribe({
      next: () => {
        this.deleting.set('');
        this.deleteNotice.set(`已删除「${batch.title}」的全部分析结果`);
        if (this.selectedBatch()?.id === batch.id) {
          this.selectedBatch.set(null);
          this.scripts.set([]);
          this.editingScript.set(null);
          this.confirmPanelOpen.set(false);
          this.classWeakness.set(null);
          this.teachingInsight.set(null);
          this.evaluation.set(null);
          this.preview.set(null);
          this.reportFiles.set([]);
          this.batchStudents.set([]);
          this.uploadImages.set([]);
        }
        this.refreshAfterDelete();
      },
      error: err => {
        this.deleting.set('');
        this.deleteNotice.set(this.errorText(err) || '删除失败，请稍后重试');
      },
    });
  }

  /** 删除后刷新：批次概览 + 批次计数 + 当前批次分析结果 */
  private refreshAfterDelete(): void {
    this.loadPaperOverview();
    const batchId = this.selectedBatch()?.id;
    if (batchId) {
      this.loadBatchStudents(batchId);
      this.loadReportFiles(batchId);
      if (this.activeTab() === 'report') {
        this.classWeakness.set(null);
        this.teachingInsight.set(null);
        this.loadAnalysisForSelectedBatch();
      }
    }
  }

  // ══════════════ 班级薄弱点 / 教学成果 ══════════════

  private loadAnalysisForSelectedBatch(): void {
    const batch = this.selectedBatch() ?? this.batches()[0];
    if (!batch) return;
    this.selectedBatch.set(batch);
    this.analysisLoading.set(true);

    this.service.getClassWeakness(batch.id).subscribe({
      next: res => {
        this.classWeakness.set(res);
        this.loadThreshold(res.courseId);
      },
      error: () => this.classWeakness.set(null),
    });
    this.service.getTeachingInsight(batch.id).subscribe({
      next: res => {
        this.teachingInsight.set(res);
        this.analysisLoading.set(false);
      },
      error: () => this.analysisLoading.set(false),
    });
  }

  onBatchChange(batchId: string): void {
    const batch = this.batches().find(b => b.id === batchId) ?? null;
    this.selectedBatch.set(batch);
    this.classWeakness.set(null);
    this.teachingInsight.set(null);
    this.evaluation.set(null);
    this.loadReportFiles(batchId);
    if (this.activeTab() === 'report') this.loadAnalysisForSelectedBatch();
  }

  pushReports(): void {
    const batch = this.selectedBatch();
    if (!batch) return;
    this.pushing.set(true);
    this.service.pushPaperReports(batch.id).subscribe({
      next: res => {
        this.pushing.set(false);
        this.pushedNotice.set(`已向 ${res.pushedCount} 名学生推送个人薄弱点报告`);
        this.classWeakness.update(c =>
          c ? { ...c, pushedCount: c.totalStudents, focusStudents: c.focusStudents.map(s => ({ ...s, pushed: true })) } : c
        );
      },
      error: () => this.pushing.set(false),
    });
  }

  // ══════════════ 试卷生成器（生成可 OCR 扫描的试卷样张）══════════════

  /** 展开 / 收起生成面板（首次展开时拉取后端默认配置） */
  togglePaperGenerator(): void {
    const open = !this.paperGenOpen();
    this.paperGenOpen.set(open);
    if (open && !this.paperGenConfig()) {
      this.loadPaperGeneratorConfig();
    }
  }

  /** 读取后端生效配置（题目数量 / 分值 / 班级 / 标题模板） */
  loadPaperGeneratorConfig(): void {
    this.service.getPaperGeneratorConfig().subscribe({
      next: cfg => {
        this.paperGenConfig.set(cfg);
        this.applyPaperGenDefaults(cfg);
      },
      error: () => this.paperGenNotice.set('读取试卷生成器配置失败'),
    });
  }

  /** 把后端配置 / 本地记忆的值填进表单（不覆盖用户已改过的值） */
  private applyPaperGenDefaults(cfg: PaperGeneratorConfig): void {
    this.genQuestionCount = this.genQuestionCount || cfg.defaultQuestionCount;
    this.genFullScore = this.genFullScore || cfg.fullScorePerQuestion;
    this.genClassName = this.genClassName || cfg.className;
    if (!this.genTitle) {
      this.genTitle = cfg.titleTemplate.replace('{course}', this.courses().find(c => c.id === this.uploadCourseId)?.title ?? '');
    }
  }

  /** 生成试卷（题目随机抽、得分随机） */
  generatePaper(): void {
    if (!this.uploadCourseId) {
      this.paperGenNotice.set('请先选择课程');
      return;
    }

    const course = this.courses().find(c => c.id === this.uploadCourseId);
    const student = this.students().find(s => s.studentId === (this.genStudentId || this.uploadStudentId));

    this.paperGenLoading.set(true);
    this.paperGenNotice.set('');
    this.paperTemplate.set(null);
    this.paperPngBlob.set(null);

    this.service
      .generatePaperTemplate({
        courseId: this.uploadCourseId,
        title: this.genTitle?.trim() || undefined,
        studentId: student?.studentId,
        studentName: student?.studentName,
        studentNumber: student?.studentNumber,
        className: this.genClassName?.trim() || undefined,
        examDate: this.genExamDate ? new Date(this.genExamDate).toISOString() : undefined,
        questionCount: this.genQuestionCount,
        fullScorePerQuestion: this.genFullScore,
        randomScore: this.genRandomScore,
        includeAnswer: this.genIncludeAnswer,
        seed: this.genSeed ?? undefined,
      })
      .subscribe({
        next: async template => {
          this.paperGenLoading.set(false);
          this.paperTemplate.set(template);
          this.paperGenNotice.set(
            `已生成《${template.title}》：${template.questionCount} 道题，满分 ${template.totalFullScore} 分，合计得分 ${template.totalScore} 分`
          );
          this.savePaperGenConfig();

          try {
            this.paperPngBlob.set(await this.htmlToPng(template.html));
          } catch {
            this.paperGenNotice.set('试卷已生成，但图片渲染失败，可先下载 HTML 再自行截图');
          }
        },
        error: err => {
          this.paperGenLoading.set(false);
          this.paperGenNotice.set(this.errorText(err) || '生成失败，请稍后重试');
        },
      });
  }

  /** 下载生成的试卷图片（PNG） */
  downloadPaperPng(): void {
    const blob = this.paperPngBlob();
    const template = this.paperTemplate();
    if (!blob || !template) return;
    this.saveBlob(blob, `${template.title}.png`);
  }

  /** 下载生成的试卷源文件（HTML，可直接打印成 PDF） */
  downloadPaperHtml(): void {
    const template = this.paperTemplate();
    if (!template) return;
    this.saveBlob(new Blob([template.html], { type: 'text/html;charset=utf-8' }), `${template.title}.html`);
  }

  /** 把生成的试卷图片作为待上传文件，直接走「上传并识别」 */
  usePaperForUpload(): void {
    const blob = this.paperPngBlob();
    const template = this.paperTemplate();
    if (!blob || !template) return;

    const file = new File([blob], `${template.title}.png`, { type: 'image/png' });
    this.pendingFile = file;
    this.ocrHint.set('');
    this.paperGenNotice.set('试卷图片已填入上传区，点「上传并识别」即可走 OCR 识别');
  }

  /**
   * HTML → PNG：把 A4 排版的试卷塞进 SVG foreignObject，再用 canvas 栅格化。
   * 纯前端完成，不依赖任何额外的截图库。
   */
  private async htmlToPng(html: string): Promise<Blob> {
    const width = 794; // A4 @96dpi
    const scale = 2;    // 2 倍图，OCR 识别更稳
    const style = /<style[^>]*>([\s\S]*?)<\/style>/i.exec(html)?.[1] ?? '';
    const body = /<body[^>]*>([\s\S]*?)<\/body>/i.exec(html)?.[1] ?? html;

    // 先在隐藏容器里量一次高度（与最终渲染同宽 → 换行一致）
    const probe = document.createElement('div');
    probe.style.cssText = `position:fixed;left:-10000px;top:0;width:${width}px;background:#fff;`;
    probe.innerHTML = `<style>${style}</style>${body}`;
    document.body.appendChild(probe);
    const height = Math.max(probe.scrollHeight, 600) + 20;
    probe.remove();

    const svg =
      `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">` +
      `<foreignObject width="100%" height="100%">` +
      `<div xmlns="http://www.w3.org/1999/xhtml" style="width:${width}px;background:#fff">` +
      `<style>${style}</style>${body}</div>` +
      `</foreignObject></svg>`;

    const img = new Image();
    await new Promise<void>((resolve, reject) => {
      img.onload = () => resolve();
      img.onerror = () => reject(new Error('render failed'));
      img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
    });

    const canvas = document.createElement('canvas');
    canvas.width = width * scale;
    canvas.height = height * scale;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('canvas unavailable');
    ctx.fillStyle = '#fff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);

    return new Promise<Blob>((resolve, reject) =>
      canvas.toBlob(blob => (blob ? resolve(blob) : reject(new Error('toBlob failed'))), 'image/png')
    );
  }

  private saveBlob(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName.replace(/[\\/:*?"<>|]/g, '_');
    a.click();
    window.URL.revokeObjectURL(url);
  }

  /** 记住生成参数（下次打开直接沿用） */
  private savePaperGenConfig(): void {
    try {
      localStorage.setItem(
        'kh:paper-generator',
        JSON.stringify({
          questionCount: this.genQuestionCount,
          fullScore: this.genFullScore,
          randomScore: this.genRandomScore,
          includeAnswer: this.genIncludeAnswer,
          className: this.genClassName,
          seed: this.genSeed,
        })
      );
    } catch {
      // 隐私模式等场景可能禁止写入，忽略
    }
  }

  private restorePaperGenConfig(): void {
    try {
      const raw = localStorage.getItem('kh:paper-generator');
      if (!raw) return;
      const cfg = JSON.parse(raw) as {
        questionCount?: number;
        fullScore?: number;
        randomScore?: boolean;
        includeAnswer?: boolean;
        className?: string;
        seed?: number | null;
      };
      if (cfg.questionCount) this.genQuestionCount = cfg.questionCount;
      if (cfg.fullScore) this.genFullScore = cfg.fullScore;
      if (cfg.randomScore !== undefined) this.genRandomScore = cfg.randomScore;
      if (cfg.includeAnswer !== undefined) this.genIncludeAnswer = cfg.includeAnswer;
      if (cfg.className) this.genClassName = cfg.className;
      this.genSeed = cfg.seed ?? null;
    } catch {
      // 忽略损坏的本地配置
    }
  }

  // ══════════════ 报告导出 / 预览 ══════════════

  /** 导出 Word 文字报告（class：班级学情+教学诊断；students：学生薄弱点汇总） */
  exportDocx(type: 'class' | 'students'): void {
    const batch = this.selectedBatch();
    if (!batch) return;
    const suffix = type === 'students' ? '学生薄弱点汇总' : '班级学情报告';
    this.download(
      `docx-${type}`,
      this.service.exportDocxReport(batch.id, type),
      `${batch.courseName}_${batch.title}_${suffix}.docx`
    );
  }

  exportStudentReports(): void {
    const batch = this.selectedBatch();
    if (!batch) return;
    this.download(
      'students-excel',
      this.service.exportStudentReports(batch.id),
      `${batch.courseName}_${batch.title}_学生薄弱点明细.xlsx`
    );
  }

  private download(kind: string, request: Observable<Blob>, fileName: string): void {
    this.exporting.set(kind);
    this.downloadNotice.set('');
    request.subscribe({
      next: blob => {
        this.exporting.set('');
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName.replace(/[\\/:*?"<>|]/g, '_');
        a.click();
        window.URL.revokeObjectURL(url);
        this.downloadNotice.set(`已导出：${fileName}`);
      },
      error: () => {
        this.exporting.set('');
        this.downloadNotice.set('导出失败，请稍后重试');
      },
    });
  }

  // ══════════════ 报告留档 ══════════════

  loadReportFiles(batchId?: string): void {
    const id = batchId ?? this.selectedBatch()?.id;
    if (!id) return;
    this.service.getReportFiles(id).subscribe({
      next: files => this.reportFiles.set(files ?? []),
      error: () => this.reportFiles.set([]),
    });
  }

  archiveReport(type: 'class' | 'students' | 'excel'): void {
    const batch = this.selectedBatch();
    if (!batch) return;
    this.archiving.set(type);
    this.downloadNotice.set('');
    this.service.archiveReport(batch.id, type).subscribe({
      next: file => {
        this.archiving.set('');
        this.downloadNotice.set(`已归档：${file.fileName}（${Math.round(file.sizeBytes / 1024)} KB）`);
        this.loadReportFiles(batch.id);
      },
      error: () => {
        this.archiving.set('');
        this.downloadNotice.set('归档失败，请稍后重试');
      },
    });
  }

  downloadArchived(file: AnalysisReportFile): void {
    this.download('archived', this.service.downloadReportFile(file.id), file.fileName);
    setTimeout(() => this.loadReportFiles(file.batchId), 400);
  }

  removeArchived(file: AnalysisReportFile): void {
    if (!confirm(`删除归档文件「${file.fileName}」？`)) return;
    this.service.deleteReportFile(file.id).subscribe({
      next: () => this.loadReportFiles(file.batchId),
      error: () => this.downloadNotice.set('删除失败'),
    });
  }

  // ══════════════ 推荐效果评估 ══════════════

  loadRecommendationWeights(): void {
    this.service.getRecommendationWeights().subscribe({
      next: w => this.recWeights.set(w),
      error: () => this.recWeights.set(null),
    });
  }

  /** 应用评估给出的最优权重（写入 Setting，立即生效） */
  applyBestWeights(): void {
    const courseId = this.classWeakness()?.courseId ?? this.selectedBatch()?.courseId;
    this.applyingWeights.set(true);
    this.weightNotice.set('');
    this.service.applyBestRecommendationWeights(courseId, 5).subscribe({
      next: w => {
        this.applyingWeights.set(false);
        this.recWeights.set(w);
        this.weightNotice.set(`已应用：${w.label}`);
        // 重新评估一次，让「当前配置」行反映新权重
        this.runEvaluation();
      },
      error: err => {
        this.applyingWeights.set(false);
        this.weightNotice.set(err?.error?.error?.message ?? '应用失败（样本不足？）');
      },
    });
  }

  runEvaluation(): void {
    const courseId = this.classWeakness()?.courseId ?? this.selectedBatch()?.courseId;
    this.evaluating.set(true);
    this.service.evaluateRecommendation(courseId, 5).subscribe({
      next: res => {
        this.evaluating.set(false);
        this.evaluation.set(res);
      },
      error: () => {
        this.evaluating.set(false);
        this.downloadNotice.set('推荐评估失败');
      },
    });
  }

  /** 预览推送给学生的报告内容 */
  openPreview(): void {
    const batch = this.selectedBatch();
    if (!batch) return;
    this.previewLoading.set(true);
    this.service.getStudentReportPreview(batch.id, this.previewStudentId || undefined).subscribe({
      next: res => {
        this.previewLoading.set(false);
        this.preview.set(res);
        this.previewStudentId = res.studentId ?? '';
      },
      error: () => {
        this.previewLoading.set(false);
        this.downloadNotice.set('预览失败：该批次还没有匹配到学生的试卷');
      },
    });
  }

  closePreview(): void {
    this.preview.set(null);
  }

  previewOther(studentId: string): void {
    this.previewStudentId = studentId;
    this.openPreview();
  }

  // ══════════════ 课程联动 ══════════════

  loadCourseLink(courseId: string): void {
    this.linkageCourseId = courseId;
    this.linkageLoading.set(true);
    this.service.getCourseLink(courseId).subscribe({
      next: res => {
        this.courseLink.set(res);
        this.linkageLoading.set(false);
        if (!this.linkPredecessorId && res.links.length) {
          this.linkPredecessorId = res.links[0].predecessorCourseId;
          this.linkSuccessorId = res.links[0].successorCourseId;
        }
      },
      error: () => this.linkageLoading.set(false),
    });
  }

  private loadThreshold(courseId: string): void {
    if (!courseId) return;
    this.thresholdNotice.set('');
    this.service.getThreshold(courseId).subscribe({
      next: t => {
        this.thresholdWeakPercent = Math.round(t.weakLine * 100);
        this.thresholdOkPercent = Math.round(t.okLine * 100);
      },
    });
  }

  saveThreshold(): void {
    const courseId = this.classWeakness()?.courseId ?? this.linkageCourseId;
    if (!courseId) return;

    const weak = Number(this.thresholdWeakPercent) / 100;
    const ok = Number(this.thresholdOkPercent) / 100;
    if (!(weak >= 0 && ok <= 1 && weak < ok)) {
      this.thresholdNotice.set('判定线不合法：需满足 0 ≤ 薄弱线 < 达标线 ≤ 100%');
      return;
    }

    this.thresholdSaving.set(true);
    this.service.setThreshold({ courseId, weakLine: weak, okLine: ok }).subscribe({
      next: () => {
        this.thresholdSaving.set(false);
        this.thresholdNotice.set('判定线已保存，重新加载分析结果生效');
        const batch = this.selectedBatch();
        if (batch) {
          this.classWeakness.set(null);
          this.teachingInsight.set(null);
          this.loadAnalysisForSelectedBatch();
        }
      },
      error: err => {
        this.thresholdSaving.set(false);
        this.thresholdNotice.set(err?.error?.error?.message ?? '判定线保存失败');
      },
    });
  }

  onLinkageCourseChange(courseId: string): void {
    this.courseLink.set(null);
    this.loadCourseLink(courseId);
  }

  addCourseLink(): void {
    if (!this.linkPredecessorId || !this.linkSuccessorId) {
      this.aiMessage.set('请选择前置课程与后续课程');
      return;
    }
    this.service
      .saveCourseLink({
        predecessorCourseId: this.linkPredecessorId,
        successorCourseId: this.linkSuccessorId,
      })
      .subscribe({
        next: () => {
          this.aiMessage.set('课程前后关系已保存');
          this.loadCourseLink(this.linkageCourseId);
        },
        error: err => this.aiMessage.set(err?.error?.error?.message ?? '保存失败'),
      });
  }

  removeCourseLink(link: CourseLinkItem): void {
    this.service.deleteCourseLink(link.id).subscribe({
      next: () => this.loadCourseLink(this.linkageCourseId),
      error: () => this.loadCourseLink(this.linkageCourseId),
    });
  }

  addMapping(): void {
    const config = this.courseLink();
    if (!config) return;
    this.service
      .savePrerequisite({
        courseId: config.courseId,
        successorChapterTitle: undefined,
        dependencyNames: [],
        sourceCourseTitle: '',
        condition: '掌握率 < 60%',
        source: 'Manual',
        status: 0,
      })
      .subscribe({
        next: created => {
          this.courseLink.update(c => (c ? { ...c, mappings: [...c.mappings, created] } : c));
        },
        error: () => void 0,
      });
  }

  removeMapping(m: PrerequisiteMapping): void {
    this.service.deletePrerequisite(m.id).subscribe({
      next: () =>
        this.courseLink.update(c => (c ? { ...c, mappings: c.mappings.filter(x => x.id !== m.id) } : c)),
      error: () =>
        this.courseLink.update(c => (c ? { ...c, mappings: c.mappings.filter(x => x.id !== m.id) } : c)),
    });
  }

  // ══════════════ AI 任务 ══════════════

  extractKnowledgePoints(overwrite: boolean): void {
    if (!this.linkageCourseId) return;
    this.aiBusy.set('extract');
    this.aiMessage.set('');
    this.service.extractKnowledgePoints(this.linkageCourseId, overwrite).subscribe({
      next: res => {
        this.aiBusy.set('');
        this.aiMessage.set(`${res.message}（共 ${res.exerciseCount} 道习题）`);
      },
      error: err => {
        this.aiBusy.set('');
        this.aiMessage.set(err?.error?.error?.message ?? '知识点抽取提交失败');
      },
    });
  }

  minePrerequisites(): void {
    this.aiBusy.set('mine');
    this.aiMessage.set('');
    this.service.minePrerequisites(this.linkageCourseId || undefined).subscribe({
      next: res => {
        this.aiBusy.set('');
        this.aiMessage.set(res.message);
        this.loadCourseLink(this.linkageCourseId);
      },
      error: () => {
        this.aiBusy.set('');
        this.aiMessage.set('先修挖掘失败');
      },
    });
  }

  pushPreCourseReviews(): void {
    this.aiBusy.set('push');
    this.aiMessage.set('');
    this.service.pushPreCourseReviews().subscribe({
      next: res => {
        this.aiBusy.set('');
        this.aiMessage.set(
          res.pushedCount > 0
            ? `已向 ${res.pushedCount} 名学生推送衔接复习包`
            : '当前没有需要推送的学生（需已配置开课日期且处于提前周数窗口内）'
        );
      },
      error: () => {
        this.aiBusy.set('');
        this.aiMessage.set('衔接复习包推送失败');
      },
    });
  }

  trainModel(): void {
    this.aiBusy.set('train');
    this.aiMessage.set('');
    this.service.trainMasteryModel().subscribe({
      next: res => {
        this.aiBusy.set('');
        this.aiMessage.set(
          `${res.message}（样本 ${res.sampleCount}，学生 ${res.studentCount}，知识点 ${res.knowledgePointCount}，估计器 ${res.estimator}）`
        );
      },
      error: () => {
        this.aiBusy.set('');
        this.aiMessage.set('模型训练失败');
      },
    });
  }

  // ══════════════ 展示辅助 ══════════════

  levelOf(mastery: number): MasteryLevel {
    return mastery < 0.6 ? MasteryLevel.Weak : mastery < 0.75 ? MasteryLevel.Fair : MasteryLevel.Ok;
  }

  statusLabel(status: PaperBatchStatus): string {
    switch (status) {
      case PaperBatchStatus.Draft:
        return '待识别';
      case PaperBatchStatus.OcrRunning:
        return '识别中';
      case PaperBatchStatus.NeedConfirm:
        return '待校对';
      case PaperBatchStatus.Confirmed:
        return '已确认';
      default:
        return '已推送';
    }
  }

  statusClass(status: PaperBatchStatus): string {
    switch (status) {
      case PaperBatchStatus.NeedConfirm:
        return 'is-warn';
      case PaperBatchStatus.Pushed:
      case PaperBatchStatus.Confirmed:
        return 'is-ok';
      default:
        return 'is-muted';
    }
  }

  matchLabel(status: string): string {
    return status === 'matched' ? '已匹配' : status === 'low' ? '待复核' : '未匹配';
  }

  uploadStatusClass(status: PaperUploadStatus): string {
    switch (status) {
      case PaperUploadStatus.Done:
        return 'is-ok';
      case PaperUploadStatus.Failed:
        return 'is-warn';
      default:
        return 'is-muted';
    }
  }

  uploadStatusLabel(status: PaperUploadStatus): string {
    switch (status) {
      case PaperUploadStatus.Pending:
        return '待识别';
      case PaperUploadStatus.Processing:
        return '识别中';
      case PaperUploadStatus.Done:
        return '已完成';
      default:
        return '失败';
    }
  }

  percent(v: number): number {
    return Math.round((v ?? 0) * 100);
  }

  /** 比率格式化（保留 1 位小数，用于评估指标） */
  rate(v: number | undefined | null): string {
    if (v === undefined || v === null || Number.isNaN(v)) return '—';
    return `${(v * 100).toFixed(1)}%`;
  }

  diagnosisClass(level: string): string {
    return level === 'good' ? 'is-good' : level === 'polarized' ? 'is-warn' : 'is-gap';
  }
}
