import { Component, OnInit, inject, signal } from '@angular/core';
import { ConfigStateService, LocalizationService, LocalizationPipe, PermissionDirective, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { IdentityRoleService } from './identity-role.service';
import type { IdentityRoleDto, IdentityRoleCreateDto, IdentityRoleUpdateDto } from './models';
import { TenantPermissionService, PermissionItem } from './tenant-permission.service';
import { FormGroup, FormBuilder, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { CommonModule } from '@angular/common';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzPageHeaderModule } from 'ng-zorro-antd/page-header';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzTreeModule } from 'ng-zorro-antd/tree';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzEmptyModule } from 'ng-zorro-antd/empty';

interface TenantDto {
  id?: string | null;
  name?: string;
}

interface PermissionGroup {
  name: string;
  displayName: string;
  /** 顶层 Default 权限，例如 KnowledgeHub.Resources，对应层级2（无边框背景） */
  parent: PermissionItem | null;
  /** 子权限，对应层级3（有背景，按层级缩进） */
  children: PermissionItem[];
  /** 扁平列表，用于保存/兼容旧逻辑 */
  permissions: PermissionItem[];
}

@Component({
  selector: 'app-identity-roles',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    LocalizationPipe,
    PermissionDirective,
    NzTableModule,
    NzButtonModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzCardModule,
    NzSpaceModule,
    NzPageHeaderModule,
    NzTagModule,
    NzDropDownModule,
    NzIconModule,
    NzSelectModule,
    NzCheckboxModule,
    NzTreeModule,
    NzSpinModule,
    NzDividerModule,
    NzEmptyModule,
  ],
  providers: [],
  templateUrl: './identity-roles.component.html',
  styleUrls: ['./identity-roles.component.scss'],
})
export class IdentityRolesComponent implements OnInit {
  roles = { items: [], totalCount: 0 } as PagedResultDto<IdentityRoleDto>;

  isModalOpen = false;
  isLoading = signal(false);

  form!: FormGroup;
  selectedRole = {} as IdentityRoleDto;
  pageIndex = 1;
  pageSize = 10;

  readonly presetRoleNames = ['admin', 'LeagueAdmin', 'SchoolAdmin', 'Teacher', 'Student', 'EnterpriseUser'] as const;

  tenants: TenantDto[] = [];
  /** 租户筛选：'__global__' 表示全局，其余为租户 Id。去掉“全部”，只能二选一。 */
  readonly globalOptionValue = '__global__';
  tenantFilter: string = this.globalOptionValue;
  private rolesRequestSeq = 0;
  tenantNames: Record<string, string> = {};

  /** 当前登录用户所属租户 ID；null 表示 host 全局管理员。 */
  currentTenantId: string | null = null;
  /** host 全局管理员可为 true；租户管理员为 false，只能查看/管理本租户的角色。 */
  isHostAdmin = false;

  isPermissionModalOpen = false;
  permissionProviderKey = '';
  permissionEntityDisplayName = '';
  permissionTenantId: string | null = null;
  permissionGroups: PermissionGroup[] = [];
  permissionLoading = signal(false);
  permissionSaving = signal(false);

  private readonly roleService = inject(IdentityRoleService);
  private readonly localization = inject(LocalizationService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly restService = inject(RestService);
  private readonly tenantPermissionService = inject(TenantPermissionService);
  private readonly configState = inject(ConfigStateService);

  l(key: string): string {
    return this.localization.instant(key);
  }

  getRoleDisplayName(roleName: string | undefined): string {
    if (!roleName) return '';
    const key = `::RoleName:${roleName}`;
    const translated = this.l(key);
    return (translated && translated !== key) ? translated : roleName;
  }

  getTenantName(tenantId: string | null | undefined): string {
    if (!tenantId) return this.l('::Global');
    return this.tenantNames[tenantId] || tenantId;
  }

  /** 当前筛选对应的租户 Id：全局返回 null。 */
  get selectedTenantId(): string | null {
    return this.tenantFilter === this.globalOptionValue ? null : this.tenantFilter;
  }

  ngOnInit(): void {
    // 识别当前用户租户身份：null 表示 host 全局管理员。
    const cu = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    const tenantId = cu?.['tenantId'];
    this.currentTenantId = (tenantId as string | null | undefined) ?? null;
    this.isHostAdmin = !this.currentTenantId;

    // 租户管理员默认只能查看本租户角色，直接锁定选中。超级管理员默认看全局。
    if (!this.isHostAdmin) {
      this.tenantFilter = this.currentTenantId ?? this.globalOptionValue;
    } else {
      this.tenantFilter = this.globalOptionValue;
    }

    this.loadTenants();
    this.buildForm();
  }

  loadTenants() {
    this.restService.request<any, TenantDto[]>({
      method: 'GET',
      url: '/api/public/tenants'
    }).subscribe((tenants) => {
      // 租户管理员只展示本租户；host 全局管理员可看全部。
      this.tenants = this.isHostAdmin
        ? tenants
        : (tenants || []).filter(t => t.id === this.currentTenantId);
      tenants.forEach(t => {
        if (t.id && t.name) {
          this.tenantNames[t.id] = t.name;
        }
      });
      this.loadRoles();
    });
  }

  loadRoles() {
    // 直接请求 + 序号 guard：避免 ListService 重复 hookToQuery 导致多路并发、后返回覆盖先返回（切换错乱）。
    const seq = ++this.rolesRequestSeq;
    const isGlobal = this.tenantFilter === this.globalOptionValue;
    const tenantId = this.isHostAdmin
      ? (isGlobal ? undefined : this.tenantFilter)
      : this.currentTenantId!;
    const onlyHost = this.isHostAdmin && isGlobal ? true : undefined;

    this.roleService.getList({
      maxResultCount: this.pageSize,
      skipCount: (this.pageIndex - 1) * this.pageSize,
      // 租户管理员：只请求本租户角色。
      tenantId,
      onlyHost,
    }).subscribe((response) => {
      if (seq !== this.rolesRequestSeq) return;
      this.roles = response;
    });
  }

  onTenantFilterChange(value: string | null) {
    const next = value || this.globalOptionValue;
    // 租户管理员：不允许选择其他租户。host 全局管理员不受限。
    if (!this.isHostAdmin && next !== this.currentTenantId) {
      this.tenantFilter = this.currentTenantId ?? this.globalOptionValue;
      return;
    }
    this.tenantFilter = next;
    this.pageIndex = 1;
    this.loadRoles();
  }

  buildForm() {
    // 租户管理员：表单中 tenantId 锁定为当前租户，避免表单被污染后提交跨租户创建。
    const tenantId = this.isHostAdmin
      ? (this.selectedRole.tenantId || null)
      : this.currentTenantId;
    this.form = this.fb.group({
      name: [this.selectedRole.name || '', Validators.required],
      isDefault: [this.selectedRole.isDefault || false],
      isPublic: [this.selectedRole.isPublic !== false],
      tenantId: [tenantId],
    });
  }

  createRole() {
    // 租户管理员：只能在本租户下创建角色。host 全局管理员可自由选择。
    const tenantId = this.isHostAdmin
      ? this.selectedTenantId
      : this.currentTenantId;
    this.selectedRole = { tenantId: tenantId ?? null } as IdentityRoleDto;
    this.buildForm();
    this.isModalOpen = true;
  }

  editRole(role: IdentityRoleDto) {
    this.roleService.get(role.id!).subscribe((r) => {
      this.selectedRole = r;
      this.buildForm();
      this.isModalOpen = true;
    });
  }

  save() {
    if (!this.form || this.form.invalid) {
      return;
    }

    const formValue = { ...this.form.getRawValue() } as IdentityRoleCreateDto | IdentityRoleUpdateDto;
    this.isLoading.set(true);

    if (this.selectedRole.id) {
      this.roleService.update(this.selectedRole.id, formValue as IdentityRoleUpdateDto).subscribe({
        next: () => {
          this.isLoading.set(false);
          this.isModalOpen = false;
          this.loadRoles();
        },
        error: () => {
          this.isLoading.set(false);
        }
      });
    } else {
      this.roleService.create(formValue as IdentityRoleCreateDto).subscribe({
        next: () => {
          this.isLoading.set(false);
          this.isModalOpen = false;
          this.loadRoles();
        },
        error: () => {
          this.isLoading.set(false);
        }
      });
    }
  }

  delete(id: string, roleName: string) {
    const displayName = this.getRoleDisplayName(roleName);
    this.confirmation.warn(this.l('::RoleDeletionConfirmationMessage').replace('{0}', displayName), this.l('::AreYouSure')).subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.roleService.delete(id).subscribe(() => this.loadRoles());
      }
    });
  }

  onPageIndexChange(index: number): void {
    this.pageIndex = index;
    this.loadRoles();
  }

  onPageSizeChange(size: number): void {
    this.pageSize = size;
    this.pageIndex = 1;
    this.loadRoles();
  }

  openPermissions(role: IdentityRoleDto) {
    if (!role.name) {
      console.error('ProviderKey is required');
      return;
    }
    // 租户管理员：只能查看/修改本租户角色的权限。即使列表中因其他原因带入其他租户角色，
    // 这里也以当前租户为准，避免调用跨租户权限 API。
    this.permissionProviderKey = role.name;
    this.permissionEntityDisplayName = this.getRoleDisplayName(role.name);
    this.permissionTenantId = this.isHostAdmin
      ? (role.tenantId || null)
      : this.currentTenantId;
    this.loadPermissions();
    setTimeout(() => {
      this.isPermissionModalOpen = true;
    });
  }

  loadPermissions() {
    this.permissionLoading.set(true);
    this.tenantPermissionService.getForTenant({
      tenantId: this.permissionTenantId,
      providerName: 'R',
      providerKey: this.permissionProviderKey,
    }).subscribe({
      next: (permissions) => {
        this.permissionGroups = this.buildPermissionGroups(permissions || []);
        this.permissionLoading.set(false);
      },
      error: () => {
        this.permissionLoading.set(false);
      }
    });
  }

  private buildPermissionGroups(permissions: any[]): PermissionGroup[] {
    const knowledgeHubPermissions = permissions.filter((p: any) => p.name && p.name.startsWith('KnowledgeHub.'));

    const groups: PermissionGroup[] = [];
    const groupMap = new Map<string, PermissionGroup>();

    knowledgeHubPermissions.forEach((p: any) => {
      // KnowledgeHub.Resources.Create -> Resources 组；
      // KnowledgeHub.Resources（模块父权限本身）-> Resources 组（而非 Other），
      // 之前用 parts.length > 1 判断会把全部 20+ 个父权限都丢进"其他"。
      const parts = p.name.replace('KnowledgeHub.', '').split('.');
      const groupName = parts[0] || 'Other';
      const displayName = this.getGroupDisplayName(groupName);

      if (!groupMap.has(groupName)) {
        const group: PermissionGroup = {
          name: groupName,
          displayName: displayName,
          parent: null,
          children: [],
          permissions: [],
        };
        groupMap.set(groupName, group);
        groups.push(group);
      }

      groupMap.get(groupName)!.permissions.push({
        name: p.name,
        displayName: this.getPermissionDisplayName(p.name),
        providerName: 'R',
        providerKey: this.permissionProviderKey,
        isGranted: p.isGranted,
      });
    });

    // 拆分层级：第二层（Default，无边框背景）与第三层（有背景、按层级缩进）
    // 约定：KnowledgeHub.{Group} 为父权限，其余 KnowledgeHub.{Group}.* 为子权限
    groups.forEach(group => {
      const parentName = `KnowledgeHub.${group.name}`;
      const parentIndex = group.permissions.findIndex(p => p.name === parentName);
      if (parentIndex >= 0) {
        group.parent = group.permissions[parentIndex];
        group.children = group.permissions.filter((_, i) => i !== parentIndex);
      } else {
        group.parent = null;
        group.children = [...group.permissions];
      }
      // 子权限按层级深度 + 名称排序，保证同层级相邻，展开时缩进有规律
      group.children.sort((a, b) => {
        const depthA = a.name.split('.').length;
        const depthB = b.name.split('.').length;
        if (depthA !== depthB) return depthA - depthB;
        return a.name.localeCompare(b.name);
      });
    });

    return groups;
  }

  private getPermissionDisplayName(permissionName: string): string {
    const shortName = permissionName.replace('KnowledgeHub.', '');
    const localizationKey = `::Permission:${shortName}`;
    const localized = this.l(localizationKey);
    if (localized && localized !== localizationKey) {
      return localized;
    }

    const nameMap: Record<string, string> = {
      'Documents': '文档管理',
      'Documents.Create': '创建新文档',
      'Documents.Edit': '编辑文档',
      'Documents.Delete': '删除文档',
      'Resources': '资源管理',
      'Resources.Create': '创建资源',
      'Resources.Edit': '编辑资源',
      'Resources.Delete': '删除资源',
      'Resources.Download': '下载资源',
      'Resources.SchoolAudit': '院校审核',
      'Resources.LeagueAudit': '联盟终审',
      'Resources.ManageCategory': '管理分类',
      'Resources.RequestDelete': '申请删除',
      'Resources.PhysicalDelete': '物理删除',
      'Resources.ViewStatistics': '查看统计',
      'Resources.ViewRecommendation': '查看推荐',
      'Users': '用户管理',
      'Users.Create': '创建新用户',
      'Users.Edit': '编辑用户',
      'Users.Delete': '删除用户',
      'Users.Import': '导入用户',
      'Search': '搜索管理',
      'Search.ManageIndex': '管理索引',
      'Search.ViewStatistics': '查看统计',
      'Courses': '课程管理',
      'Courses.Create': '创建课程',
      'Courses.Edit': '编辑课程',
      'Courses.Delete': '删除课程',
      'Courses.Enroll': '课程报名',
      'Courses.ManageEnrollment': '管理选课',
      'AI': '智能助手',
      'AI.Chat': '智能问答',
      'AI.LessonPlan': '教案生成',
      'AI.CaseAnalysis': '案例分析',
      'AI.CareerGuidance': '职业规划',
      'Alliance': '联盟管理',
      'Alliance.Create': '创建联盟',
      'Alliance.Update': '编辑联盟',
      'Alliance.Delete': '删除联盟',
      'Alliance.ManageMembers': '管理联盟成员',
      'Learning': '学习记录',
      'Learning.ViewStatistics': '查看学习统计',
      'Learning.ExportData': '导出学习数据',
    };
    return nameMap[shortName] || nameMap[permissionName] || shortName.split('.')[shortName.split('.').length - 1];
  }

  private getGroupDisplayName(groupName: string): string {
    const localizationKey = `::Permission:${groupName}`;
    const localized = this.l(localizationKey);
    if (localized && localized !== localizationKey) {
      return localized;
    }

    const nameMap: Record<string, string> = {
      'Documents': '文档管理',
      'Resources': '资源管理',
      'Users': '用户管理',
      'Search': '搜索管理',
      'Courses': '课程管理',
      'AI': '智能助手',
      'TeachingAgents': '教学智能体',
      'Alliance': '联盟管理',
      'Learning': '学习记录',
      'News': '资讯中心',
      'MicroMajors': '微专业',
      'Majors': '专业',
      'Practicum': '实训管理',
      'DoubleHigh': '双高评估支撑',
      'Employment': '就业服务',
      'RecruitmentLive': '招聘直播',
      'TenantInfo': '租户信息',
      'AccountValidity': '账号有效期管理',
      'Branding': '品牌设置',
      'SpecialEducation': '特殊教育',
      'VoiceAssistant': '语音助手',
      'Other': '其他',
    };
    return nameMap[groupName] || groupName;
  }

  savePermissions() {
    this.permissionSaving.set(true);
    const permissions = this.permissionGroups.flatMap(g => g.parent ? [g.parent, ...g.children] : [...g.children]);
    
    this.tenantPermissionService.setForTenant({
      tenantId: this.permissionTenantId,
      permissions: permissions,
    }).subscribe({
      next: () => {
        this.permissionSaving.set(false);
        this.isPermissionModalOpen = false;
      },
      error: () => {
        this.permissionSaving.set(false);
      }
    });
  }

  isGroupAllGranted(group: PermissionGroup): boolean {
    const all = group.parent ? [group.parent, ...group.children] : group.children;
    return all.length > 0 && all.every(p => p.isGranted);
  }

  isGroupIndeterminate(group: PermissionGroup): boolean {
    const all = group.parent ? [group.parent, ...group.children] : group.children;
    const granted = all.filter(p => p.isGranted).length;
    return granted > 0 && granted < all.length;
  }

  toggleGroupPermissions(group: PermissionGroup, granted: boolean) {
    if (group.parent) group.parent.isGranted = granted;
    group.children.forEach(p => p.isGranted = granted);
    // 兼容旧的扁平列表（save 时会扁平化，所以也要同步）
    group.permissions.forEach(p => p.isGranted = granted);
  }

  /** 子权限单独切换时，保持 parent 同步；若全部子权限勾选则 parent 也勾选 */
  onChildToggle(group: PermissionGroup) {
    if (!group.parent) return;
    const allChildrenGranted = group.children.length > 0 && group.children.every(p => p.isGranted);
    // 不强制联动，但保持扁平列表一致
    group.permissions.forEach(p => {
      if (p.name === group.parent!.name) p.isGranted = group.parent!.isGranted;
    });
    // 可选：子全选时自动勾选父（更符合层级语义）
    if (allChildrenGranted && !group.parent.isGranted) {
      // 不自动勾选，保持用户显式控制；如需自动可取消注释下一行
      // group.parent.isGranted = true;
    }
  }

  getPermissionIndent(name: string): number {
    // KnowledgeHub.Resources.Create -> 3 段，缩进 1 级；更深层级递增
    const depth = name.split('.').length;
    // Group 本身为 2 段 (KnowledgeHub.Resources)，子为 3 段及以上
    return Math.max(0, depth - 3);
  }
}
