import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { debounceTime, distinctUntilChanged, finalize } from 'rxjs/operators';
import { Subject } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ConfigStateService, PermissionService } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalService } from 'ng-zorro-antd/modal';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ResourceLibraryService } from '../../proxy/resource-libraries/resource-library.service';
import { ResourceLibraryVisibility } from '../../proxy/resource-libraries/resource-library-visibility.enum';
import { ResourceLibraryMemberRole } from '../../proxy/resource-libraries/resource-library-member-role.enum';
import type {
  AddResourceLibraryMemberDto,
  CreateResourceLibraryDto,
  ResourceLibraryDto,
  ResourceLibraryMemberDto,
  ResourceLibraryPartnerTenantDto,
  UpdateResourceLibraryDto,
  ResourceLibraryTeacherDto,
} from '../../proxy/resource-libraries/dtos/models';
import { MajorService } from '../../proxy/majors/major.service';
import type { MajorDto } from '../../proxy/majors/dtos/models';
import { OssUploadService } from '../../shared/oss-upload.service';
import { BuildTaskService } from '../../proxy/resource-libraries/build-task.service';
import type { LibraryTemplateDto } from '../../proxy/resource-libraries/dtos/models';

/**
 * 代理里的 ResourceLibraryDto 还没有模板字段（后端新增 TemplateId / TemplateVersion，
 * 代理未重新生成），这里用本地扩展类型承接，避免手动改动 proxy 文件。
 */
type ResourceLibraryRow = ResourceLibraryDto & {
  templateId?: string | null;
  templateVersion?: number | null;
};

/**
 * 资源库管理（管理端，多校共建）。
 *
 * 需求「资源库可以多校共建，体现主建学校（专业），和合作学校」：
 * - 库由本校（主建学校）创建并绑定主建专业；
 * - 合作学校通过成员表加入，加入后即可浏览库内资源/课程；
 * - 只有主建学校可以改库、管成员、发布 / 归档。
 */
@Component({
  selector: 'app-resource-library-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzCheckboxModule,
    NzEmptyModule,
    NzFormModule,
    NzInputModule,
    NzModalModule,
    NzSelectModule,
    NzSpaceModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './resource-library-management.component.html',
  styleUrls: ['./resource-library-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceLibraryManagementComponent implements OnInit {
  private readonly libraryService = inject(ResourceLibraryService);
  private readonly majorService = inject(MajorService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly buildTaskService = inject(BuildTaskService);
  private readonly route = inject(ActivatedRoute);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly permissionService = inject(PermissionService);
  private readonly configState = inject(ConfigStateService);

  readonly items = signal<ResourceLibraryRow[]>([]);
  readonly majors = signal<MajorDto[]>([]);
  readonly loading = signal<boolean>(false);
  /** 封面图上传中（OSS 直传） */
  readonly coverUploading = signal<boolean>(false);

  // ── 写权限（教师角色只有 Default + Build：可浏览 / 建设内容，不能建库 / 改库 / 删库 / 管共建） ──
  readonly canCreate = signal<boolean>(false);
  readonly canEdit = signal<boolean>(false);
  readonly canDelete = signal<boolean>(false);
  readonly canManageMembers = signal<boolean>(false);

  /** 只读模式：无任何库级写权限时，页面仅可浏览 */
  readonly readOnly = computed(
    () =>
      !this.canCreate() &&
      !this.canEdit() &&
      !this.canDelete() &&
      !this.canManageMembers(),
  );

  /** 建库 / 改库弹窗 */
  modalVisible = false;
  editingId: string | null = null;
  form: CreateResourceLibraryDto & UpdateResourceLibraryDto = this.emptyForm();

  // ── 分配教师弹窗 ──
  // 注意：本组件是 OnPush，弹窗里的数据/加载态必须用 signal，
  // 否则异步回调里赋值不会触发变更检测，界面会一直停在“正在加载…”。
  teacherModalVisible = false;
  teacherLibrary: ResourceLibraryDto | null = null;
  readonly teachers = signal<ResourceLibraryTeacherDto[]>([]);
  readonly assignableTeachers = signal<ResourceLibraryTeacherDto[]>([]);
  teacherKeyword = '';
  readonly teacherLoading = signal(false);
  readonly assignableLoading = signal(false);
  /** 教师搜索防抖：避免每敲一个字就打一次接口（弹窗卡顿的体感主因之一） */
  private readonly teacherKeyword$ = new Subject<string>();
  /** 当前租户 Id：用于区分「本校教师」与共建库里的「他校教师」 */
  readonly currentTenantId =
    (this.configState.getOne('currentUser') as { tenantId?: string } | undefined)?.tenantId ?? null;
  canAssignTeachers = false;

  /** 成员弹窗 */
  memberModalVisible = false;
  memberLibrary: ResourceLibraryDto | null = null;
  memberTenantId: string | null = null;

  /**
   * 资源库状态文案（V2.0 增加审核态）。
   * 3 待院校审核 / 4 待联盟终审 / 5 已驳回 / 6 变更审核中 / 7 删除审核中
   */
  readonly statusLabels: Record<number, string> = {
    0: '草稿',
    1: '已发布',
    2: '已归档',
    3: '待院校审核',
    4: '待联盟终审',
    5: '已驳回',
    6: '变更审核中',
    7: '删除审核中',
  };

  statusColor(status?: number): string {
    switch (status) {
      case 1:
        return 'green';
      case 2:
        return 'default';
      case 5:
        return 'red';
      case 3:
      case 4:
      case 6:
      case 7:
        return 'processing';
      default:
        return 'orange';
    }
  }

  /** 审核中（变更/删除）：界面提示「仍按旧配置对外服务」。 */
  isReviewing(item: ResourceLibraryDto): boolean {
    return item.status === 3 || item.status === 4 || item.status === 6 || item.status === 7;
  }

  /** 教学资源库可见性：0 私有 / 1 公开 / 2 院校共建 */
  readonly visibilityLabels: Record<number, string> = { 0: '私有', 1: '公开', 2: '院校共建' };

  /** PRD 形态名（校级私有库 / 校级共享库 / 校际共建库）。 */
  readonly formLabels: Record<number, string> = { 0: '校级私有库', 1: '校级共享库', 2: '校际共建库' };

  /** 形态说明（新建 / 编辑弹窗提示）。 */
  readonly formHints: Record<number, string> = {
    0: '校级私有库：前台所有人可见可浏览，仅课程老师导入班级的学员可以学习；不允许参建单位；不对外开放自学。',
    1: '校级共享库：对外开放学习（其他学员需手机号验证 + 实名认证）；不允许参建单位；库内课程仅限校内共享。',
    2: '校际共建库：主建单位 + 参建单位共建；库内课程可共享给参建单位；同样对外开放自学。',
  };

  ngOnInit(): void {
    // 权限决定 UI 只读：教师无 Create/Edit/Delete/ManageMembers（后端接口同样有 [Authorize] 兜底）
    this.canCreate.set(this.permissionService.getGrantedPolicy('KnowledgeHub.ResourceLibraries.Create'));
    this.canEdit.set(this.permissionService.getGrantedPolicy('KnowledgeHub.ResourceLibraries.Edit'));
    this.canDelete.set(this.permissionService.getGrantedPolicy('KnowledgeHub.ResourceLibraries.Delete'));
    this.canManageMembers.set(
      this.permissionService.getGrantedPolicy('KnowledgeHub.ResourceLibraries.ManageMembers'),
    );
    this.canAssignTeachers = this.permissionService.getGrantedPolicy(
      'KnowledgeHub.ResourceLibraries.AssignTeachers',
    );

    this.teacherKeyword$
      .pipe(debounceTime(300), distinctUntilChanged())
      .subscribe((keyword) => this.loadAssignableTeachers(undefined, keyword));

    // focus 入口要等列表加载完才能定位到具体库，所以先记下来，reload 成功后再执行
    this.pendingFocus = this.route.snapshot.queryParamMap.get('focus');
    this.reload();

    this.majorService.getList({ maxResultCount: 200, skipCount: 0 }).subscribe({
      next: (res) => this.majors.set(res.items || []),
      error: () => this.majors.set([]),
    });
  }

  /** 从门户「共建共享资源库中心」带 `?focus=` 进来的待处理入口 */
  private pendingFocus: string | null = null;

  /**
   * 支持从门户「共建共享资源库中心」带 `?focus=` 进来，直接落到对应入口：
   * - `create`  → 打开「新建资源库」弹窗（建库向导）
   * - `members` → 打开第一个可管理的库的「共建学校」弹窗
   */
  private applyFocusFromQuery(): void {
    const focus = this.pendingFocus;
    if (!focus) {
      return;
    }
    this.pendingFocus = null;
    if (focus === 'create') {
      this.openCreate();
      return;
    }
    if (focus === 'members') {
      const first = this.items().find(x => x.isOwner) || this.items()[0];
      if (first) {
        this.openMembers(first);
      }
    }
  }

  /** 已发布的资源库模板（建库向导用；V2.0 P3）。 */
  readonly libraryTemplates = signal<LibraryTemplateDto[]>([]);

  /** 模板列表加载失败的原因（如「共建任务功能未对本校开通」）。 */
  readonly templateLoadError = signal('');

  /** 新建时选择的模板（编辑时不改模板）。 */
  selectedTemplateId: string | null = null;

  /** 当前选中的模板对象。 */
  readonly selectedTemplate = computed(
    () => this.libraryTemplates().find(t => t.id === this.selectedTemplateId) ?? null,
  );

  /**
   * 模板配置（ConfigJson）摘要：把 JSON 里的栏目 / 审核阶段等翻译成人能看懂的说明。
   *
   * <p>模板不会自动填写建库表单，它只把「模板版本 + 配置」快照冻结到新建的库上，
   * 供后续按模板下发栏目 / 交付项；所以这里把模板实际携带的内容列出来，
   * 避免用户以为“选了模板表单就会变”。</p>
   */
  readonly selectedTemplateSummary = computed(() => {
    const tpl = this.selectedTemplate();
    if (!tpl) return null;
    const parsed = this.parseTemplateConfig(tpl.configJson);
    const items: Array<{ label: string; value: string }> = [];
    if (parsed.modules.length) {
      items.push({ label: '启用栏目', value: parsed.modules.map(m => this.moduleLabel(m)).join('、') });
    }
    if (parsed.reviewStages.length) {
      items.push({ label: '审核阶段', value: parsed.reviewStages.map(s => this.stageLabel(s)).join(' → ') });
    }
    if (parsed.requiredFields.length) {
      items.push({ label: '必填字段', value: parsed.requiredFields.join('、') });
    }
    if (parsed.defaultPolicy) {
      items.push({ label: '默认共享策略', value: parsed.defaultPolicy });
    }
    if (!items.length) {
      items.push({ label: '模板内容', value: '未配置栏目等参数（仅记录模板版本）' });
    }
    return { name: tpl.name, version: tpl.version, description: tpl.description, items };
  });

  private parseTemplateConfig(configJson: string | null | undefined): {
    modules: string[];
    reviewStages: string[];
    requiredFields: string[];
    defaultPolicy: string | null;
  } {
    const empty = { modules: [], reviewStages: [], requiredFields: [], defaultPolicy: null };
    if (!configJson) return empty;
    try {
      const parsed = JSON.parse(configJson);
      if (!parsed || typeof parsed !== 'object') return empty;
      const arr = (v: unknown): string[] =>
        Array.isArray(v) ? v.filter(x => typeof x === 'string') : [];
      return {
        modules: arr(parsed.modules),
        reviewStages: arr(parsed.reviewStages),
        requiredFields: arr(parsed.requiredFields),
        defaultPolicy: typeof parsed.defaultPolicy === 'string' ? parsed.defaultPolicy : null,
      };
    } catch {
      return empty;
    }
  }

  private moduleLabel(m: string): string {
    const map: Record<string, string> = {
      course: '课程',
      practice: '实训',
      resource: '资源',
      exam: '试卷',
    };
    return map[m] ?? m;
  }

  private stageLabel(s: string): string {
    const map: Record<string, string> = {
      SCHOOL: '院校初审',
      LIBRARY: '联盟终审',
      LEAGUE: '联盟终审',
    };
    return map[s] ?? s;
  }

  private loadLibraryTemplates(): void {
    this.templateLoadError.set('');
    this.buildTaskService.getTemplates().subscribe({
      next: list => this.libraryTemplates.set(list ?? []),
      error: (err: any) => {
        this.libraryTemplates.set([]);
        // 模板列表接口受「共建任务」功能开关控制，未开通时会直接报错；
        // 这里把真实原因透出来，避免页面显示成「暂无模板」让人误以为没配置。
        this.templateLoadError.set(
          err?.error?.error?.message || err?.error?.message || '模板列表加载失败',
        );
      },
    });
  }

  emptyForm(): CreateResourceLibraryDto & UpdateResourceLibraryDto {
    return {
      name: '',
      description: '',
      coverUrl: '',
      primaryMajorId: null,
      principal: '',
      isPublic: true,
      sortOrder: 0,
      visibility: ResourceLibraryVisibility.Private,
      openToExternalLearners: false,
      partnerTenantIds: [],
    };
  }

  /**
   * 可选的参建学校（排除主建学校自身）。
   *
   * <para>主建学校在建库时自动以「主建」角色入表，不需要也不能在这里重复选。
   * 数据源是专用的候选学校接口（跨租户返回全部学校），不用门户的公开租户列表 ——
   * 后者在学校域名下只返回本校。</para>
   */
  readonly partnerCandidates = signal<ResourceLibraryPartnerTenantDto[]>([]);

  /** 拉取候选参建学校（建库 / 改库弹窗用）。 */
  loadPartnerCandidates(): void {
    this.libraryService.getCandidatePartnerTenants().subscribe({
      next: list => this.partnerCandidates.set(list || []),
      error: () => this.partnerCandidates.set([]),
    });
  }

  reload(): void {
    this.loading.set(true);
    this.libraryService.getList({}).subscribe({
      next: (list) => {
        this.items.set(list || []);
        this.loading.set(false);
        this.applyFocusFromQuery();
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载资源库失败');
      },
    });
  }

  openCreate(): void {
    this.editingId = null;
    this.form = this.emptyForm();
    this.selectedTemplateId = null;
    this.loadLibraryTemplates();
    this.modalVisible = true;
    this.loadPartnerCandidates();
  }

  openEdit(item: ResourceLibraryDto): void {    this.editingId = item.id;
    this.form = {
      name: item.name || '',
      description: item.description || '',
      coverUrl: item.coverUrl || '',
      primaryMajorId: item.primaryMajorId || null,
      principal: item.principal || '',
      isPublic: item.isPublic,
      sortOrder: item.sortOrder,
      visibility: item.visibility ?? ResourceLibraryVisibility.Private,
      openToExternalLearners: item.openToExternalLearners ?? false,
      // 已有共建名单回填（role 0 = 主建学校，不属于「关联学校」）
      partnerTenantIds: (item.members || [])
        .filter(m => m.role !== ResourceLibraryMemberRole.Owner)
        .map(m => m.memberTenantId)
        .filter((id): id is string => !!id),
    };
    this.modalVisible = true;
    this.loadPartnerCandidates();
  }

  // ── 封面图：直接上传到 OSS，不让用户手填 URL ──
  onCoverSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // 允许重复选同一文件
    if (!file) {
      return;
    }
    if (!file.type.startsWith('image/')) {
      this.message.error('只能上传图片文件');
      return;
    }
    if (file.size / 1024 / 1024 > 5) {
      this.message.error('图片大小不能超过 5MB');
      return;
    }

    this.coverUploading.set(true);
    this.ossUploadService.uploadImage(file).subscribe({
      next: (res) => {
        this.coverUploading.set(false);
        this.form.coverUrl = res.url;
        this.message.success('封面图上传成功');
      },
      error: () => {
        this.coverUploading.set(false);
        this.message.error('封面图上传失败，请重试');
      },
    });
  }

  removeCover(): void {
    this.form.coverUrl = '';
  }

  /** 切换库形态：跟随形态默认值重置「对外开放自学」（校级私有库强制关闭）。 */
  onVisibilityChange(visibility: number): void {
    this.form.visibility = visibility;
    this.form.openToExternalLearners = visibility !== 0;
  }

  save(): void {
    if (!this.form.name?.trim()) {
      this.message.warning('请填写资源库名称');
      return;
    }

    // 校际共建库必须至少有一所参建学校（后端 CreateAsync/UpdateAsync 同样强制校验）
    if (
      (this.form.visibility ?? 0) === ResourceLibraryVisibility.CoBuilt &&
      !(this.form.partnerTenantIds || []).length
    ) {
      this.message.warning('校际共建库请至少选择一所关联学校');
      return;
    }

    if (this.editingId) {
      this.libraryService.update(this.editingId, this.form as UpdateResourceLibraryDto).subscribe({
        next: () => {
          this.modalVisible = false;
          this.message.success('已保存');
          this.reload();
        },
        error: (err) => this.message.error(err?.error?.error?.message || '保存失败'),
      });
      return;
    }

    const payload = { ...(this.form as CreateResourceLibraryDto) };
    if (!this.editingId && this.selectedTemplateId) {
      payload.templateId = this.selectedTemplateId;
    }
    this.libraryService.create(payload).subscribe({
      next: () => {
        this.modalVisible = false;
        this.message.success('资源库已创建（草稿），加入合作学校后可发布');
        this.reload();
      },
      error: (err) => this.message.error(err?.error?.error?.message || '创建失败'),
    });
  }

  remove(item: ResourceLibraryDto): void {
    this.libraryService.delete(item.id).subscribe({
      next: () => {
        this.message.success('已删除');
        this.reload();
      },
      error: (err) => this.message.error(err?.error?.error?.message || '删除失败'),
    });
  }

  /** 发布 / 归档 */
  /** 提交建库 / 变更 / 删除审核（后端按开关自动生成审核单）。 */
  submitForReview(item: ResourceLibraryDto): void {
    this.modal.confirm({
      nzTitle: '提交审核',
      nzContent: `将提交「${item.name}」的建库/变更内容给院校管理员审核；院校共建库还需联盟终审。审核期间库仍按当前配置对外服务。`,
      nzOnOk: () => {
        this.libraryService
          .update(item.id!, { name: item.name, description: item.description ?? undefined } as any)
          .subscribe({
            next: () => {
              this.message.success('已提交审核');
              this.reload();
            },
            error: err => this.message.error(err?.error?.error?.message ?? '提交失败'),
          });
      },
    });
  }

  /** 申请删除：V2.0 走审核后归档，不硬删。 */
  requestDelete(item: ResourceLibraryDto): void {
    this.modal.confirm({
      nzTitle: '申请删除（归档）',
      nzContent: `将提交「${item.name}」的删除申请。审核通过后资源库转为「已归档」，内容解绑但保留可追溯，不会物理删除。`,
      nzOkDanger: true,
      nzOnOk: () => {
        this.libraryService.delete(item.id!).subscribe({
          next: () => {
            this.message.success('已提交删除审核');
            this.reload();
          },
          error: err => this.message.error(err?.error?.error?.message ?? '提交失败'),
        });
      },
    });
  }

  changeStatus(item: ResourceLibraryDto, status: number): void {
    this.libraryService.update(item.id, { status } as UpdateResourceLibraryDto).subscribe({
      next: () => {
        this.message.success(status === 1 ? '已发布' : '已归档');
        this.reload();
      },
      error: (err) => this.message.error(err?.error?.error?.message || '操作失败'),
    });
  }

  // ── 分配教师 ──────────────────────────────────────────────

  openTeachers(item: ResourceLibraryDto): void {
    this.teacherLibrary = item;
    this.teacherKeyword = '';
    this.teacherModalVisible = true;
    this.loadTeachers();
  }

  /** 已分配教师（显式分配）+ 可分配教师（本校 Teacher 角色） */
  loadTeachers(): void {
    if (!this.teacherLibrary) {
      return;
    }

    const libraryId = this.teacherLibrary.id as string;
    this.teacherLoading.set(true);
    this.assignableLoading.set(true);
    this.teachers.set([]);
    this.assignableTeachers.set([]);

    // 两个请求并行：可分配列表由后端按 libraryId 标记 IsAssigned，前端本地剔除，
    // 不再串行等待「已分配」先返回（弹窗打开速度提升一倍）。
    this.libraryService.getTeachers(libraryId).subscribe({
      next: (list) => {
        this.teachers.set(list || []);
        this.teacherLoading.set(false);
      },
      error: () => {
        this.teachers.set([]);
        this.teacherLoading.set(false);
      },
    });

    this.loadAssignableTeachers(libraryId);
  }

  /** 他校教师（共建库里由对方学校维护，本校只读展示） */
  isOtherSchoolTeacher(teacher: ResourceLibraryTeacherDto): boolean {
    return !!teacher.tenantId && !!this.currentTenantId && teacher.tenantId !== this.currentTenantId;
  }

  /** 只读教师：主建专业默认教师（非显式分配）或他校教师，都不能在本校取消分配 */
  isReadonlyTeacher(teacher: ResourceLibraryTeacherDto): boolean {
    return !!teacher.isImplicit || this.isOtherSchoolTeacher(teacher);
  }

  /** 可分配教师：本校「教师」角色用户，剔除已分配的（后端已标 IsAssigned，无需等已分配列表） */
  private loadAssignableTeachers(libraryId?: string, keyword?: string): void {
    this.assignableLoading.set(true);
    const targetLibraryId = libraryId || (this.teacherLibrary?.id as string | undefined);
    const filter = (keyword ?? this.teacherKeyword) || undefined;

    this.libraryService
      .getAssignableTeachers({ filter, libraryId: targetLibraryId })
      .pipe(finalize(() => this.assignableLoading.set(false)))
      .subscribe({
        next: (list) => {
          // 后端已按「显式分配 ∪ 主建专业默认教师」标记 IsAssigned，
          // 这里只按它过滤，避免与并行的「已分配」请求竞态导致重复出现同一位教师。
          this.assignableTeachers.set((list || []).filter((t) => !t.isAssigned));
        },
        error: () => {
          this.assignableTeachers.set([]);
        },
      });
  }

  onTeacherKeywordChange(keyword: string): void {
    this.teacherKeyword = keyword;
    // 防抖后再请求（只刷新可分配列表，已分配列表与关键词无关）
    this.teacherKeyword$.next(keyword || '');
  }

  assignTeacher(userId: string): void {
    if (!this.teacherLibrary) {
      return;
    }

    this.libraryService
      .assignTeacher(this.teacherLibrary.id as string, { userId })
      .subscribe({
        next: () => {
          this.message.success('已分配');
          this.loadTeachers();
          this.reload();
        },
        error: (err) => this.message.error(err?.error?.error?.message || '分配失败'),
      });
  }

  removeTeacher(userId: string): void {
    if (!this.teacherLibrary) {
      return;
    }

    this.libraryService.removeTeacher(this.teacherLibrary.id as string, userId).subscribe({
      next: () => {
        this.message.success('已取消分配');
        this.loadTeachers();
        this.reload();
      },
      error: (err) => this.message.error(err?.error?.error?.message || '取消失败'),
    });
  }

  openMembers(item: ResourceLibraryDto): void {
    this.memberLibrary = item;
    this.memberTenantId = null;
    this.memberModalVisible = true;
    this.loadPartnerCandidates();
  }

  addMember(): void {
    if (!this.memberLibrary || !this.memberTenantId) {
      this.message.warning('请选择合作学校');
      return;
    }

    const input: AddResourceLibraryMemberDto = { memberTenantId: this.memberTenantId };
    this.libraryService.addMember(this.memberLibrary.id, input).subscribe({
      next: () => {
        this.message.success('已加入共建');
        this.memberTenantId = null;
        this.reload();
        this.refreshMembers();
      },
      error: (err) => this.message.error(err?.error?.error?.message || '加入失败'),
    });
  }

  removeMember(member: ResourceLibraryMemberDto): void {
    if (!this.memberLibrary) {
      return;
    }

    this.libraryService.removeMember(this.memberLibrary.id, member.memberTenantId).subscribe({
      next: () => {
        this.message.success('已移除');
        this.reload();
        this.refreshMembers();
      },
      error: (err) => this.message.error(err?.error?.error?.message || '移除失败'),
    });
  }

  /** 成员变更后刷新弹窗内的成员列表 */
  private refreshMembers(): void {
    if (!this.memberLibrary) {
      return;
    }

    const id = this.memberLibrary.id;
    this.libraryService.get(id).subscribe({
      next: (fresh) => {
        this.memberLibrary = fresh;
        this.items.update(list => list.map(x => (x.id === id ? fresh : x)));
      },
      error: () => undefined,
    });
  }

  /**
   * 「共建学校」弹窗里的候选学校：排除主建学校与已在名单里的学校。
   *
   * <para>与建库弹窗共用同一个候选来源（跨租户），避免学校域名下拿不到别的学校。</para>
   */
  candidateTenants(library: ResourceLibraryDto): ResourceLibraryPartnerTenantDto[] {
    const existing = new Set((library.members || []).map(m => m.memberTenantId));
    return this.partnerCandidates().filter(t => !!t.id && t.id !== library.ownerTenantId && !existing.has(t.id));
  }
}
