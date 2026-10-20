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
  ClassWeaknessDto,
  CourseLinkConfig,
  CourseLinkItem,
  MasteryLevel,
  OcrPipelineStep,
  PaperAnalysisOverview,
  PaperBatchItem,
  PaperBatchStatus,
  PaperScriptRow,
  PaperUploadImage,
  PaperUploadPayload,
  PaperUploadStatus,
  PrerequisiteMapping,
  RecommendationEvaluation,
  RecommendationWeights,
  StudentReportPreview,
  TeachingInsightDto,
} from '../../learning-analytics/models/learning-analytics.models';

type TeacherSection = 'paper' | 'class' | 'teaching' | 'linkage';

/** 教师端「智学分析」：纸质试卷分析 / 班级薄弱点 / 教学成果分析 / 课程联动设置 */
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

  readonly sections: { key: TeacherSection; label: string; icon: string }[] = [
    { key: 'paper', label: '纸质试卷分析', icon: 'cloud-upload' },
    { key: 'class', label: '班级薄弱点', icon: 'bar-chart' },
    { key: 'teaching', label: '教学成果分析', icon: 'line-chart' },
    { key: 'linkage', label: '课程联动设置', icon: 'deployment-unit' },
  ];

  readonly activeSection = signal<TeacherSection>('paper');
  readonly usingSampleData = this.service.usingSampleData;

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
  pendingFiles: File[] = [];
  readonly showUploadForm = signal(false);
  /** 使用 Mock OCR 演示（不上传真实图片，按班级学生生成） */
  demoMode = false;
  /** PDF 多页合并为一份试卷（默认关闭：每页一份学生试卷） */
  mergePdfPages = false;
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
  /** OCR 能力（PDF 是否可直传），首次上传时探测并缓存 */
  private ocrCapabilities: { pdfDirect: boolean; pdfModel?: string } | null = null;

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

  readonly canUpload = computed(
    () =>
      !this.rendering() &&
      !!this.uploadCourseId &&
      !!this.uploadTitle.trim() &&
      (this.demoMode || this.pendingFiles.length > 0)
  );

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
            this.uploadTitle = `${items[0].title} 期末试卷`;
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

  setSection(key: TeacherSection): void {
    this.activeSection.set(key);
    if ((key === 'class' || key === 'teaching') && !this.classWeakness()) {
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
    this.appendFiles(Array.from(ev.dataTransfer?.files ?? []));
  }

  onFilesSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    this.appendFiles(Array.from(input.files ?? []));
    input.value = '';
  }

  private appendFiles(files: File[]): void {
    const allowed = ['image/jpeg', 'image/png', 'image/webp', 'image/bmp', 'application/pdf'];
    const accepted = files.filter(
      f => allowed.includes(f.type) || /\.(jpe?g|png|webp|bmp|pdf)$/i.test(f.name)
    );
    if (accepted.length !== files.length) {
      this.ocrHint.set('已忽略不支持的文件（仅支持 JPG / PNG / WEBP / BMP / PDF）');
    }
    this.pendingFiles = [...this.pendingFiles, ...accepted];
  }

  hasPdfSelected(): boolean {
    return this.pendingFiles.some(f => this.isPdf(f));
  }

  isPdf(file: File): boolean {
    return file.type === 'application/pdf' || /\.pdf$/i.test(file.name);
  }

  removePendingFile(index: number): void {
    this.pendingFiles = this.pendingFiles.filter((_, i) => i !== index);
  }

  clearPendingFiles(): void {
    this.pendingFiles = [];
  }

  /** 上传（真实图片 OCR）→ 触发识别 → 轮询进度 */
  startUpload(): void {
    if (!this.canUpload()) return;

    const course = this.courses().find(c => c.id === this.uploadCourseId);
    this.uploading.set(true);
    this.ocrSteps.set([]);
    this.ocrHint.set('');
    this.uploadImages.set([]);

    this.service
      .createPaperBatch({
        courseId: this.uploadCourseId,
        courseName: course?.title ?? '',
        title: this.uploadTitle.trim(),
        examDate: this.uploadDate,
        fileNames: this.demoMode ? [] : this.pendingFiles.map(f => f.name),
      })
      .pipe(
        switchMap(batch =>
          this.uploadPayloads()
            .then(payloads =>
              payloads.length
                ? this.service.uploadPaperImages(batch.id, payloads).pipe(catchError(() => of(void 0)))
                : of(void 0)
            )
            .then(() => batch)
        )
      )
      .pipe(
        switchMap(batch =>
          this.service.runPaperOcr(batch.id).pipe(catchError(() => of([]))).pipe(() => of(batch))
        )
      )
      .subscribe({
        next: batch => {
          this.uploading.set(false);
          this.pendingFiles = [];
          this.showUploadForm.set(false);
          this.runningOcr.set(true);
          this.selectedBatch.set(batch);
          this.pollBatch(batch.id);
        },
        error: () => {
          this.uploading.set(false);
          this.runningOcr.set(false);
          this.ocrHint.set('上传失败，请检查网络或后端服务');
        },
      });
  }

  /**
   * 构造上传载荷：优先把图片直传 OSS（只传 URL，请求体小、OCR 时模型按 URL 取图）；
   * OSS 未配置或上传失败时自动回退 Base64。
   */
  private async uploadPayloads(): Promise<PaperUploadPayload[]> {
    if (this.demoMode) return [];

    const payloads: PaperUploadPayload[] = [];
    let useOss = true;

    if (!this.ocrCapabilities) {
      try {
        this.ocrCapabilities = await firstValueFrom(this.service.getOcrCapabilities());
      } catch {
        this.ocrCapabilities = { pdfDirect: false };
      }
    }

    for (const file of this.pendingFiles) {
      if (this.isPdf(file)) {
        // PDF 直传：整份上传 OSS，交给 qwen3.5-ocr 的 Responses API 解析（需后端开启 PdfDirectEnabled）
        if (this.ocrCapabilities?.pdfDirect && useOss) {
          try {
            const res = await firstValueFrom(this.service.uploadFileToOss(file, file.name));
            if (res?.url) {
              payloads.push({
                fileName: file.name,
                contentType: 'application/pdf',
                url: res.url,
                pageIndex: 1,
              });
              continue;
            }
          } catch {
            // OSS 不可用 → 继续走浏览器拆页
          }
        }

        payloads.push(...(await this.pdfToPayloads(file, this.mergePdfPages, () => useOss, v => (useOss = v))));
      } else {
        const item = await this.toPayload(file, file.name, file.type || 'image/jpeg', 1, undefined, useOss);
        if (item.usedOss === false) useOss = false;
        payloads.push(item.payload);
      }
    }

    this.ossUploadEnabled.set(useOss && payloads.some(p => !!p.url));
    return payloads;
  }

  /** 单张图片 → OSS URL 或 Base64（OSS 失败自动回退） */
  private async toPayload(
    blob: Blob,
    fileName: string,
    contentType: string,
    pageIndex: number,
    mergeGroup: string | undefined,
    tryOss: boolean
  ): Promise<{ payload: PaperUploadPayload; usedOss: boolean }> {
    if (tryOss) {
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
   * merge=true 时同一份 PDF 的页面共用 mergeGroup，后端会合并为一份试卷。
   */
  private async pdfToPayloads(
    file: File,
    merge: boolean,
    getUseOss: () => boolean,
    setUseOss: (v: boolean) => void
  ): Promise<PaperUploadPayload[]> {
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
        const item = await this.toPayload(
          blob,
          pageName,
          'image/jpeg',
          p,
          merge ? file.name : undefined,
          getUseOss()
        );
        if (item.usedOss === false) setUseOss(false);
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

  private readBase64(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        const result = String(reader.result ?? '');
        const comma = result.indexOf(',');
        resolve(comma >= 0 ? result.slice(comma + 1) : result);
      };
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(file);
    });
  }

  /** 轮询 OCR 进度：批次进入「待校对 / 已确认 / 已推送」或所有图片处理完成后停止 */
  private pollBatch(batchId: string): void {
    this.pollSub?.unsubscribe();
    this.ocrSteps.set([
      { key: 'upload', label: '① 上传试卷', status: 'done' },
      { key: 'ocr', label: '② OCR识别学号与得分', status: 'running' },
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
      { key: 'ocr', label: '② OCR识别学号与得分', status: timedOut ? 'pending' : 'done' },
      { key: 'match', label: '③ 题目匹配习题库知识点', status: timedOut ? 'pending' : 'done' },
      { key: 'report', label: '④ 生成报告并推送', status: 'pending' },
    ]);

    this.service.getPaperUploadImages(batchId).subscribe(imgs => {
      this.uploadImages.set(imgs);
      const failed = imgs.filter(i => i.status === PaperUploadStatus.Failed).length;
      const mock = imgs.some(i => i.isMockOcr);
      if (failed > 0) {
        this.ocrHint.set(`${failed} 张图片识别失败，可点「重试失败图片」或检查图片清晰度`);
      } else if (mock) {
        this.ocrHint.set('当前使用 Mock OCR（未配置 Qwen Key），结果为示例数据，请人工校对');
      } else if (timedOut) {
        this.ocrHint.set('识别耗时较长，已停止等待；可稍后刷新批次状态查看结果');
      } else if (batch) {
        this.ocrHint.set(`识别完成：${batch.scriptCount} 份试卷，${batch.needsConfirmCount} 份待校对`);
      }
    });

    this.loadPaperOverview();
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
    if (this.activeSection() !== 'paper') this.loadAnalysisForSelectedBatch();
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
