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
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ResourceLibraryService } from '../../proxy/resource-libraries/resource-library.service';
import { ResourceLibraryVisibility } from '../../proxy/resource-libraries/resource-library-visibility.enum';
import type {
  AddResourceLibraryMemberDto,
  CreateResourceLibraryDto,
  ResourceLibraryDto,
  ResourceLibraryMemberDto,
  UpdateResourceLibraryDto,
  ResourceLibraryTeacherDto,
} from '../../proxy/resource-libraries/dtos/models';
import { MajorService } from '../../proxy/majors/major.service';
import type { MajorDto } from '../../proxy/majors/dtos/models';
import { PortalService } from '../../proxy/portal/portal.service';
import type { TenantResourceSummaryDto } from '../../proxy/portal/models';
import { OssUploadService } from '../../shared/oss-upload.service';

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
  private readonly portalService = inject(PortalService);
  private readonly message = inject(NzMessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly permissionService = inject(PermissionService);
  private readonly configState = inject(ConfigStateService);

  readonly items = signal<ResourceLibraryDto[]>([]);
  readonly majors = signal<MajorDto[]>([]);
  readonly tenants = signal<TenantResourceSummaryDto[]>([]);
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
  teacherModalVisible = false;
  teacherLibrary: ResourceLibraryDto | null = null;
  teachers: ResourceLibraryTeacherDto[] = [];
  assignableTeachers: ResourceLibraryTeacherDto[] = [];
  teacherKeyword = '';
  teacherLoading = false;
  assignableLoading = false;
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

  readonly statusLabels: Record<number, string> = { 0: '草稿', 1: '已发布', 2: '已归档' };

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

    this.portalService.getPublicTenantList().subscribe({
      next: (list) => this.tenants.set(list || []),
      error: () => this.tenants.set([]),
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
    };
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
    this.modalVisible = true;
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
    };
    this.modalVisible = true;
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

    this.libraryService.create(this.form as CreateResourceLibraryDto).subscribe({
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
    this.teacherLoading = true;
    this.assignableLoading = true;
    this.teachers = [];
    this.assignableTeachers = [];

    // 两个请求并行：可分配列表由后端按 libraryId 标记 IsAssigned，前端本地剔除，
    // 不再串行等待「已分配」先返回（弹窗打开速度提升一倍）。
    this.libraryService.getTeachers(libraryId).subscribe({
      next: (list) => {
        this.teachers = list || [];
        this.teacherLoading = false;
      },
      error: () => {
        this.teachers = [];
        this.teacherLoading = false;
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
    this.assignableLoading = true;
    const targetLibraryId = libraryId || (this.teacherLibrary?.id as string | undefined);
    const filter = (keyword ?? this.teacherKeyword) || undefined;

    this.libraryService
      .getAssignableTeachers({ filter, libraryId: targetLibraryId })
      .pipe(finalize(() => (this.assignableLoading = false)))
      .subscribe({
        next: (list) => {
          // 后端已按「显式分配 ∪ 主建专业默认教师」标记 IsAssigned，
          // 这里只按它过滤，避免与并行的「已分配」请求竞态导致重复出现同一位教师。
          this.assignableTeachers = (list || []).filter((t) => !t.isAssigned);
        },
        error: () => {
          this.assignableTeachers = [];
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

  /** 可选合作学校：排除主建学校与已在库内的成员 */
  candidateTenants(library: ResourceLibraryDto): TenantResourceSummaryDto[] {
    const existing = new Set((library.members || []).map(m => m.memberTenantId));
    return this.tenants().filter(t => t.id && t.id !== library.ownerTenantId && !existing.has(t.id));
  }
}
