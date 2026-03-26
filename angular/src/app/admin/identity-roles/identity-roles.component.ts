import { Component, OnInit, inject, signal } from '@angular/core';
import { ListService, LocalizationService, LocalizationPipe, PermissionDirective, RestService } from '@abp/ng.core';
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
  providers: [ListService],
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
  
  tenants: TenantDto[] = [];
  selectedTenantId: string | null = null;
  tenantNames: Record<string, string> = {};

  isPermissionModalOpen = false;
  permissionProviderKey = '';
  permissionEntityDisplayName = '';
  permissionTenantId: string | null = null;
  permissionGroups: PermissionGroup[] = [];
  permissionLoading = signal(false);
  permissionSaving = signal(false);

  private readonly list = inject(ListService);
  private readonly roleService = inject(IdentityRoleService);
  private readonly localization = inject(LocalizationService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly restService = inject(RestService);
  private readonly tenantPermissionService = inject(TenantPermissionService);

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

  ngOnInit(): void {
    this.loadTenants();
    this.buildForm();
  }

  loadTenants() {
    this.restService.request<any, TenantDto[]>({
      method: 'GET',
      url: '/api/public/tenants'
    }).subscribe((tenants) => {
      this.tenants = tenants;
      tenants.forEach(t => {
        if (t.id && t.name) {
          this.tenantNames[t.id] = t.name;
        }
      });
      this.loadRoles();
    });
  }

  loadRoles() {
    const roleStreamCreator = (query: any) =>
      this.roleService.getList({
        ...query,
        maxResultCount: this.pageSize,
        skipCount: (this.pageIndex - 1) * this.pageSize,
        tenantId: this.selectedTenantId || undefined,
      });

    this.list.hookToQuery(roleStreamCreator).subscribe((response) => {
      this.roles = response;
    });
  }

  onTenantFilterChange(tenantId: string | null) {
    this.selectedTenantId = tenantId;
    this.pageIndex = 1;
    this.loadRoles();
  }

  buildForm() {
    this.form = this.fb.group({
      name: [this.selectedRole.name || '', Validators.required],
      isDefault: [this.selectedRole.isDefault || false],
      isPublic: [this.selectedRole.isPublic !== false],
      tenantId: [this.selectedRole.tenantId || null],
    });
  }

  createRole() {
    this.selectedRole = { tenantId: this.selectedTenantId } as IdentityRoleDto;
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

    const formValue = { ...this.form.value } as IdentityRoleCreateDto | IdentityRoleUpdateDto;
    this.isLoading.set(true);

    if (this.selectedRole.id) {
      this.roleService.update(this.selectedRole.id, formValue as IdentityRoleUpdateDto).subscribe({
        next: () => {
          this.isLoading.set(false);
          this.isModalOpen = false;
          this.list.get();
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
          this.list.get();
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
        this.roleService.delete(id).subscribe(() => this.list.get());
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
    this.permissionProviderKey = role.name;
    this.permissionEntityDisplayName = this.getRoleDisplayName(role.name);
    this.permissionTenantId = role.tenantId || null;
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
      const parts = p.name.replace('KnowledgeHub.', '').split('.');
      const groupName = parts.length > 1 ? parts[0] : 'Other';
      const displayName = this.getGroupDisplayName(groupName);
      
      if (!groupMap.has(groupName)) {
        const group: PermissionGroup = {
          name: groupName,
          displayName: displayName,
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
    
    return groups;
  }

  private getPermissionDisplayName(permissionName: string): string {
    const shortName = permissionName.replace('KnowledgeHub.', '');
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
      'Resources.PhysicalDelete': '物理删除',
      'Resources.ViewStatistics': '查看统计',
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
      'AI': '智能助手',
      'AI.Chat': '智能问答',
      'AI.LessonPlan': '教案生成',
      'AI.CaseAnalysis': '案例分析',
      'AI.CareerGuidance': '职业规划',
    };
    return nameMap[shortName] || nameMap[permissionName] || shortName.split('.')[shortName.split('.').length - 1];
  }

  private getGroupDisplayName(groupName: string): string {
    const nameMap: Record<string, string> = {
      'Documents': '文档管理',
      'Resources': '资源管理',
      'Users': '用户管理',
      'Search': '搜索管理',
      'Courses': '课程管理',
      'AI': '智能助手',
      'Other': '其他',
    };
    return nameMap[groupName] || groupName;
  }

  savePermissions() {
    this.permissionSaving.set(true);
    const permissions = this.permissionGroups.flatMap(g => g.permissions);
    
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
    return group.permissions.length > 0 && group.permissions.every(p => p.isGranted);
  }

  isGroupIndeterminate(group: PermissionGroup): boolean {
    const granted = group.permissions.filter(p => p.isGranted).length;
    return granted > 0 && granted < group.permissions.length;
  }

  toggleGroupPermissions(group: PermissionGroup, granted: boolean) {
    group.permissions.forEach(p => p.isGranted = granted);
  }
}
