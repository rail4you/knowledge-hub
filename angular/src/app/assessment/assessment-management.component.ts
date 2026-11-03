import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import {
  CreateDoubleHighProjectFromTemplateDto,
  CreateUpdateDoubleHighIndicatorDto,
  DoubleHighDataSourceType,
  DoubleHighEvidenceDto,
  DoubleHighIndicatorDto,
  DoubleHighIndicatorType,
  DoubleHighProjectDetailDto,
  DoubleHighProjectDto,
  DoubleHighProjectStatus,
  DoubleHighScoringRule,
  DoubleHighService,
  DoubleHighTemplateDto,
} from '../double-high/double-high.service';

/** 单个指标的填报草稿（值 / 说明 / 自评备注） */
interface IndicatorDraft {
  value: number | null;
  textValue: string;
  note: string;
}

/** 项目配置页里可编辑的指标行 */
interface IndicatorRow {
  id?: string;
  indicatorCode: string;
  name: string;
  indicatorType: DoubleHighIndicatorType;
  unit: string;
  dataSourceType: DoubleHighDataSourceType;
  dataSourceName: string;
  weight: number;
  scoringRule: DoubleHighScoringRule;
  targetValue: number | null;
  requiredEvidences: string;
  basis: string;
  sortOrder: number;
}

@Component({
  selector: 'app-assessment-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzDatePickerModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzProgressModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
    NzTooltipModule,
  ],
  templateUrl: './assessment-management.component.html',
  styleUrls: ['./assessment-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssessmentManagementComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly doubleHighService = inject(DoubleHighService);
  private readonly message = inject(NzMessageService);

  readonly statuses = DoubleHighProjectStatus;
  readonly indicatorTypes = DoubleHighIndicatorType;
  readonly scoringRules = DoubleHighScoringRule;
  readonly dataSourceTypes = DoubleHighDataSourceType;

  readonly tabs = ['评估项目', '填报指标', '项目配置', '资料导出'];

  readonly projects = signal<DoubleHighProjectDto[]>([]);
  readonly templates = signal<DoubleHighTemplateDto[]>([]);
  readonly detail = signal<DoubleHighProjectDetailDto | null>(null);
  readonly currentId = signal<string | null>(null);
  readonly activeTab = signal(0);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly uploadingIndicatorId = signal<string | null>(null);

  /** 填报草稿：indicatorId -> 草稿 */
  readonly drafts = signal<Record<string, IndicatorDraft>>({});
  /** 项目配置页的可编辑指标行 */
  configRows = signal<IndicatorRow[]>([]);
  /** 项目配置页的项目基本信息 */
  configForm = {
    title: '',
    assessmentTarget: '',
    endTime: null as Date | null,
    reporterName: '',
  };

  /** 新建项目弹窗 */
  createVisible = false;
  createForm: CreateDoubleHighProjectFromTemplateDto = {
    templateKey: '',
    title: '',
    assessmentTarget: '',
    reporterName: '',
    endTime: undefined,
  };

  readonly indicatorTypeOptions = [
    { value: DoubleHighIndicatorType.Auto, label: '系统取数' },
    { value: DoubleHighIndicatorType.Number, label: '填写数值' },
    { value: DoubleHighIndicatorType.Text, label: '填写说明' },
    { value: DoubleHighIndicatorType.EvidenceOnly, label: '仅佐证' },
  ];

  readonly scoringRuleOptions = [
    { value: DoubleHighScoringRule.Proportional, label: '比例制' },
    { value: DoubleHighScoringRule.Threshold, label: '达标制' },
    { value: DoubleHighScoringRule.Direct, label: '直接计分' },
    { value: DoubleHighScoringRule.None, label: '不计分' },
  ];

  readonly dataSourceOptions = [
    { value: DoubleHighDataSourceType.Manual, label: '手工填报' },
    { value: DoubleHighDataSourceType.ResourceCount, label: '资源数量' },
    { value: DoubleHighDataSourceType.CourseCount, label: '课程数量' },
    { value: DoubleHighDataSourceType.MicroMajorCount, label: '微专业数量' },
    { value: DoubleHighDataSourceType.PracticumProjectCount, label: '实训项目数量' },
    { value: DoubleHighDataSourceType.NewsArticleCount, label: '资讯数量' },
    { value: DoubleHighDataSourceType.MicroMajorEnrollmentCount, label: '微专业报名量' },
    { value: DoubleHighDataSourceType.PracticumEnrollmentCount, label: '实训参与量' },
    { value: DoubleHighDataSourceType.CourseEnrollmentCount, label: '课程选课人次' },
    { value: DoubleHighDataSourceType.CourseCompletionRate, label: '课程学习完成率' },
    { value: DoubleHighDataSourceType.EmploymentRate, label: '就业率' },
  ];

  /** 当前项目（详情） */
  readonly current = computed(() => this.detail());

  /** 填报进度（按已完成指标数 / 指标总数） */
  readonly fillPercent = computed(() => {
    const d = this.detail();
    if (!d || d.indicatorCount === 0) {
      return 0;
    }
    return Math.round((d.completedIndicatorCount * 100) / d.indicatorCount);
  });

  /**
   * 配置页权重合计。
   * 注：权重输入框用 ngModel 双向绑定直接改行对象（不会触发 signal 变更），
   * 所以这里用普通方法实时求和，避免 computed 缓存导致合计不刷新。
   */
  configWeightSum(): number {
    return this.configRows().reduce((sum, row) => sum + (Number(row.weight) || 0), 0);
  }

  ngOnInit(): void {
    this.loadTemplates();
    this.loadProjects();

    // 深链接：/assessment/manage/project/:id[/config|/export]
    const id = this.route.snapshot.paramMap.get('id');
    const url = this.route.snapshot.url.map(s => s.path).join('/');
    if (id) {
      this.currentId.set(id);
      this.activeTab.set(url.includes('/config') ? 2 : url.includes('/export') ? 3 : 1);
      this.loadDetail(id);
    }
  }

  // ── 数据加载 ────────────────────────────────────────────────────────

  loadTemplates(): void {
    this.doubleHighService.getTemplates().subscribe({
      next: list => {
        this.templates.set(list || []);
        if (!this.createForm.templateKey && list && list.length > 0) {
          this.createForm.templateKey = list[0].key;
        }
      },
      error: () => this.message.warning('评估模板加载失败，可稍后重试'),
    });
  }

  loadProjects(): void {
    this.loading.set(true);
    this.doubleHighService.getList({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: result => {
        this.projects.set(result.items || []);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.showApiError(err, '评估项目加载失败');
      },
    });
  }

  loadDetail(id: string): void {
    this.loading.set(true);
    this.doubleHighService.getDetail(id).subscribe({
      next: detail => {
        this.detail.set(detail);
        this.buildDrafts(detail);
        this.buildConfigRows(detail);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.showApiError(err, '评估项目详情加载失败');
      },
    });
  }

  private buildDrafts(detail: DoubleHighProjectDetailDto): void {
    const drafts: Record<string, IndicatorDraft> = {};
    for (const item of detail.indicators) {
      drafts[item.id] = {
        value: item.latestValue?.value ?? null,
        textValue: item.latestValue?.textValue ?? '',
        note: item.latestValue?.note ?? '',
      };
    }
    this.drafts.set(drafts);
  }

  private buildConfigRows(detail: DoubleHighProjectDetailDto): void {
    this.configForm = {
      title: detail.title,
      assessmentTarget: detail.assessmentTarget || '',
      endTime: detail.endTime ? new Date(detail.endTime) : null,
      reporterName: detail.reporterName || '',
    };
    this.configRows.set(
      detail.indicators.map(item => ({
        id: item.id,
        indicatorCode: item.indicatorCode,
        name: item.name,
        indicatorType: item.indicatorType,
        unit: item.unit || '',
        dataSourceType: item.dataSourceType,
        dataSourceName: item.dataSourceName || '',
        weight: item.weight,
        scoringRule: item.scoringRule,
        targetValue: item.targetValue ?? null,
        requiredEvidences: (item.requiredEvidences || []).join('、'),
        basis: item.basis || '',
        sortOrder: item.sortOrder,
      })),
    );
  }

  // ── 项目选择与 Tab 切换 ────────────────────────────────────────────

  onSelectProject(id: string): void {
    this.currentId.set(id);
    this.loadDetail(id);
    // 配置中的项目先带到「项目配置」页签，其余直接进「填报指标」。
    const status = this.projects().find(p => p.id === id)?.status;
    this.gotoTab(status === DoubleHighProjectStatus.Draft ? 2 : 1);
  }

  gotoTab(index: number): void {
    this.activeTab.set(index);

    // 「评估项目」页签回到列表页（不带项目 id）；其余页签带上项目 id 以便深链接。
    if (index === 0) {
      void this.router.navigate(['/assessment/manage']);
      return;
    }

    const id = this.currentId();
    if (!id) {
      void this.router.navigate(['/assessment/manage']);
      return;
    }

    const suffix = index === 2 ? '/config' : index === 3 ? '/export' : '';
    void this.router.navigate([`/assessment/manage/project/${id}${suffix}`]);
  }

  // ── 新建项目 ────────────────────────────────────────────────────────

  /** 当前选中模板的说明（模板下拉里的描述文案） */
  selectedTemplateDescription(): string {
    const key = this.createForm.templateKey;
    if (!key) {
      return '';
    }
    return this.templates().find(t => t.key === key)?.description || '';
  }

  openCreate(): void {
    this.createForm = {
      templateKey: this.templates()[0]?.key || '',
      title: '',
      assessmentTarget: '',
      reporterName: '',
      endTime: undefined,
    };
    this.createVisible = true;
  }

  createProject(): void {
    if (!this.createForm.templateKey) {
      this.message.warning('请选择评估模板');
      return;
    }
    if (!this.createForm.title?.trim()) {
      this.message.warning('请填写评估项目名称');
      return;
    }

    this.saving.set(true);
    const payload: CreateDoubleHighProjectFromTemplateDto = {
      ...this.createForm,
      endTime: this.createForm.endTime ? new Date(this.createForm.endTime).toISOString() : undefined,
    };

    this.doubleHighService.createFromTemplate(payload).subscribe({
      next: detail => {
        this.saving.set(false);
        this.createVisible = false;
        this.message.success('评估项目已创建，请继续确认指标与权重');
        this.loadProjects();
        this.currentId.set(detail.id);
        this.detail.set(detail);
        this.buildDrafts(detail);
        this.buildConfigRows(detail);
        this.activeTab.set(2);
        void this.router.navigate([`/assessment/manage/project/${detail.id}/config`]);
      },
      error: err => {
        this.saving.set(false);
        this.showApiError(err, '评估项目创建失败');
      },
    });
  }

  deleteProject(id: string): void {
    this.doubleHighService.delete(id).subscribe({
      next: () => {
        this.message.success('评估项目已删除');
        this.currentId.set(null);
        this.detail.set(null);
        this.loadProjects();
        this.activeTab.set(0);
        void this.router.navigate(['/assessment/manage']);
      },
      error: err => this.showApiError(err, '删除失败'),
    });
  }

  // ── 项目配置 ────────────────────────────────────────────────────────

  addConfigRow(): void {
    const rows = this.configRows();
    const template = this.templates().find(t => t.key === this.detail()?.templateKey);
    const scope = template?.scopeLabel || this.detail()?.templateName || '评估';
    const next = rows.length + 1;
    rows.push({
      indicatorCode: `I${String(next).padStart(2, '0')}`,
      name: '新指标',
      indicatorType: DoubleHighIndicatorType.Text,
      unit: '',
      dataSourceType: DoubleHighDataSourceType.Manual,
      dataSourceName: '手工填报',
      weight: 0,
      scoringRule: DoubleHighScoringRule.None,
      targetValue: null,
      requiredEvidences: '',
      basis: '校本',
      sortOrder: next,
    });
    this.configRows.set([...rows]);
  }

  removeConfigRow(index: number): void {
    const rows = [...this.configRows()];
    rows.splice(index, 1);
    this.configRows.set(rows);
  }

  moveConfigRow(index: number, up: boolean): void {
    const rows = [...this.configRows()];
    const target = up ? index - 1 : index + 1;
    if (target < 0 || target >= rows.length) {
      return;
    }
    const [row] = rows.splice(index, 1);
    rows.splice(target, 0, row);
    rows.forEach((r, i) => (r.sortOrder = i + 1));
    this.configRows.set(rows);
  }

  saveConfig(): void {
    const detail = this.detail();
    if (!detail) {
      return;
    }

    const rows = this.configRows();
    const sum = rows.reduce((acc, row) => acc + (Number(row.weight) || 0), 0);
    if (sum - 1 > 0.000001) {
      this.message.warning(`指标权重之和不能超过 1，当前为 ${sum.toFixed(2)}`);
      return;
    }
    if (rows.some(row => !row.name?.trim())) {
      this.message.warning('请填写每个指标的名称');
      return;
    }
    if (rows.some(row => !row.indicatorCode?.trim())) {
      this.message.warning('请填写每个指标的编号');
      return;
    }
    const duplicated = rows
      .map(r => (r.indicatorCode || '').trim().toLowerCase())
      .filter((code, index, arr) => code && arr.indexOf(code) !== index);
    if (duplicated.length > 0) {
      this.message.warning(`指标编号重复：${duplicated[0]}`);
      return;
    }

    const indicators: CreateUpdateDoubleHighIndicatorDto[] = rows.map((row, index) => ({
      categoryName: detail.templateName || '评估',
      indicatorCode: (row.indicatorCode || '').trim(),
      name: (row.name || '').trim(),
      unit: row.unit?.trim() || undefined,
      dataSourceType: row.indicatorType === DoubleHighIndicatorType.Auto ? row.dataSourceType : DoubleHighDataSourceType.Manual,
      targetValue: row.targetValue ?? undefined,
      weight: Number(row.weight) || 0,
      sortOrder: index + 1,
      indicatorType: row.indicatorType,
      scoringRule: row.scoringRule,
      requiredEvidences: (row.requiredEvidences || '')
        .split(/[、,，]/)
        .map(x => x.trim())
        .filter(x => !!x),
      basis: row.basis?.trim() || undefined,
      dataSourceName: row.dataSourceName?.trim() || undefined,
    }));

    this.saving.set(true);
    this.doubleHighService
      .update(detail.id, {
        title: this.configForm.title?.trim() || detail.title,
        batchCode: detail.batchCode,
        description: detail.description,
        status: detail.status,
        startTime: detail.startTime,
        endTime: this.configForm.endTime ? new Date(this.configForm.endTime).toISOString() : detail.endTime,
        templateKey: detail.templateKey,
        assessmentTarget: this.configForm.assessmentTarget?.trim(),
        reporterName: this.configForm.reporterName?.trim(),
        indicators,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.message.success('项目配置已保存');
          this.loadProjects();
          this.loadDetail(detail.id);
        },
        error: err => {
          this.saving.set(false);
          this.showApiError(err, '项目配置保存失败');
        },
      });
  }

  startFilling(): void {
    const detail = this.detail();
    if (!detail) {
      return;
    }
    this.doubleHighService.startFilling(detail.id).subscribe({
      next: () => {
        this.message.success('已开始填报，指标配置已锁定');
        this.loadProjects();
        this.loadDetail(detail.id);
        this.activeTab.set(1);
        void this.router.navigate([`/assessment/manage/project/${detail.id}`]);
      },
      error: err => this.showApiError(err, '开始填报失败'),
    });
  }

  // ── 填报 ────────────────────────────────────────────────────────────

  getDraft(indicatorId: string): IndicatorDraft {
    return (
      this.drafts()[indicatorId] ?? {
        value: null,
        textValue: '',
        note: '',
      }
    );
  }

  updateDraft(indicatorId: string, patch: Partial<IndicatorDraft>): void {
    const drafts = { ...this.drafts() };
    drafts[indicatorId] = { ...this.getDraft(indicatorId), ...patch };
    this.drafts.set(drafts);
  }

  saveReporting(item: DoubleHighIndicatorDto): void {
    const draft = this.getDraft(item.id);
    // 系统取数指标：数值由平台带出，只提交自评说明（后端会拒绝手工改值）。
    const isAuto = item.indicatorType === DoubleHighIndicatorType.Auto;
    this.doubleHighService
      .saveReporting({
        indicatorId: item.id,
        value: isAuto ? undefined : draft.value ?? undefined,
        textValue: isAuto ? undefined : draft.textValue || undefined,
        note: draft.note || undefined,
      })
      .subscribe({
        next: () => {
          this.message.success(`${item.name} 已保存`);
          const id = this.currentId();
          if (id) {
            this.loadDetail(id);
          }
        },
        error: err => this.showApiError(err, `${item.name} 保存失败`),
      });
  }

  submitProject(): void {
    const detail = this.detail();
    if (!detail) {
      return;
    }
    this.doubleHighService.submit(detail.id).subscribe({
      next: () => {
        this.message.success('填报已提交，内容已锁定');
        this.loadProjects();
        this.loadDetail(detail.id);
      },
      error: err => this.showApiError(err, '提交填报失败'),
    });
  }

  onEvidenceSelected(input: HTMLInputElement, indicator: DoubleHighIndicatorDto, evidenceName: string): void {
    const file = input.files && input.files.length > 0 ? input.files[0] : null;
    input.value = '';
    if (!file) {
      return;
    }

    const detail = this.detail();
    if (!detail) {
      return;
    }

    this.uploadingIndicatorId.set(indicator.id);
    this.doubleHighService.addEvidenceFile(detail.id, indicator.id, evidenceName, file).subscribe({
      next: () => {
        this.uploadingIndicatorId.set(null);
        this.message.success('佐证材料上传成功');
        this.loadDetail(detail.id);
      },
      error: err => {
        this.uploadingIndicatorId.set(null);
        this.showApiError(err, '佐证材料上传失败');
      },
    });
  }

  deleteEvidence(evidence: DoubleHighEvidenceDto): void {
    this.doubleHighService.deleteEvidence(evidence.id).subscribe({
      next: () => {
        this.message.success('佐证材料已删除');
        const id = this.currentId();
        if (id) {
          this.loadDetail(id);
        }
      },
      error: err => this.showApiError(err, '佐证材料删除失败'),
    });
  }

  evidencesOf(indicatorId: string): DoubleHighEvidenceDto[] {
    return (this.detail()?.evidences || []).filter(e => e.indicatorId === indicatorId);
  }

  evidencesOfSlot(indicatorId: string, evidenceName: string): DoubleHighEvidenceDto[] {
    return this.evidencesOf(indicatorId).filter(e => (e.evidenceName || '') === evidenceName);
  }

  // ── 导出 ────────────────────────────────────────────────────────────

  exportPackage(): void {
    const detail = this.detail();
    if (!detail) {
      this.message.warning('请先选择评估项目');
      return;
    }
    this.doubleHighService.exportPackage(detail.id).subscribe({
      next: blob => this.downloadBlob(blob, `评估资料包_${detail.title}.zip`),
      error: err => this.showApiError(err, '资料包导出失败'),
    });
  }

  exportAllPackage(): void {
    this.doubleHighService.exportAllPackage().subscribe({
      next: blob => this.downloadBlob(blob, `评估资料包_全部项目.zip`),
      error: err => this.showApiError(err, '资料包导出失败'),
    });
  }

  exportReport(): void {
    const detail = this.detail();
    if (!detail) {
      this.message.warning('请先选择评估项目');
      return;
    }
    this.doubleHighService.exportReport(detail.id).subscribe({
      next: blob => this.downloadBlob(blob, `${detail.batchCode}_双高评估报表.xlsx`),
      error: err => this.showApiError(err, '报表导出失败'),
    });
  }

  downloadEvidenceFile(evidence: DoubleHighEvidenceDto): void {
    this.doubleHighService.downloadEvidenceFile(evidence.id).subscribe({
      next: blob => this.downloadBlob(blob, evidence.fileName || evidence.title),
      error: err => this.showApiError(err, '佐证文件下载失败'),
    });
  }

  private downloadBlob(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
    window.URL.revokeObjectURL(url);
  }

  // ── 展示辅助 ────────────────────────────────────────────────────────

  getStageLabel(status: DoubleHighProjectStatus): string {
    const labels: Record<number, string> = {
      [DoubleHighProjectStatus.Draft]: '① 配置中',
      [DoubleHighProjectStatus.Active]: '② 填报中',
      [DoubleHighProjectStatus.Submitted]: '③ 已提交',
      [DoubleHighProjectStatus.Closed]: '④ 已关闭',
    };
    return labels[status] || '未知';
  }

  getStageTagClass(status: DoubleHighProjectStatus): string {
    const classes: Record<number, string> = {
      [DoubleHighProjectStatus.Draft]: 'wn',
      [DoubleHighProjectStatus.Active]: 'blue',
      [DoubleHighProjectStatus.Submitted]: 'ok',
      [DoubleHighProjectStatus.Closed]: 'gray',
    };
    return classes[status] || 'gray';
  }

  getIndicatorTypeLabel(type: DoubleHighIndicatorType): string {
    return this.indicatorTypeOptions.find(x => x.value === type)?.label || '填写数值';
  }

  getScoringRuleLabel(rule: DoubleHighScoringRule): string {
    return this.scoringRuleOptions.find(x => x.value === rule)?.label || '比例制';
  }

  getDataSourceLabel(type: DoubleHighDataSourceType): string {
    return this.dataSourceOptions.find(x => x.value === type)?.label || '手工填报';
  }

  indicatorCode(index: number): string {
    return 'I' + String(index + 1).padStart(2, '0');
  }

  formatSize(size?: number): string {
    if (!size) {
      return '-';
    }
    if (size < 1024) {
      return `${size} B`;
    }
    if (size < 1024 * 1024) {
      return `${(size / 1024).toFixed(1)} KB`;
    }
    return `${(size / 1024 / 1024).toFixed(1)} MB`;
  }

  formatDate(value?: string): string {
    if (!value) {
      return '-';
    }
    const date = new Date(value);
    if (isNaN(date.getTime())) {
      return value;
    }
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  }

  /** 资料包结构预览 */
  packagePreview(): { path: string; note: string }[] {
    const detail = this.detail();
    if (!detail) {
      return [];
    }

    const rows: { path: string; note: string }[] = [
      { path: `${detail.title}/00_项目说明.md`, note: '文档' },
      { path: `${detail.title}/01_指标体系.csv`, note: '文档' },
      { path: `${detail.title}/02_填报内容.csv`, note: '文档' },
      { path: `${detail.title}/03_佐证材料清单.csv`, note: '文档' },
      { path: `${detail.title}/04_评分汇总.csv`, note: '文档' },
    ];

    detail.indicators.forEach((item, index) => {
      this.evidencesOf(item.id).forEach(evidence => {
        const name = evidence.evidenceName || evidence.title;
        rows.push({
          path: `${detail.title}/佐证材料/${this.indicatorCode(index)}_${item.name}/${name}/${evidence.fileName || evidence.title}`,
          note: `佐证 · ${this.formatSize(evidence.fileSize)}`,
        });
      });
    });

    return rows;
  }

  private showApiError(err: any, fallback: string): void {
    const detail = err?.error?.error?.message || err?.error?.message || err?.message || '未知错误';
    console.error('[Assessment]', fallback, err);
    this.message.error(`${fallback}：${detail}`);
  }
}
