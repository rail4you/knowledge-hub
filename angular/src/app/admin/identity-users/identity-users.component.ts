import { Component, OnInit, inject, signal } from '@angular/core';
import {
  ConfigStateService,
  LocalizationService,
  LocalizationPipe,
  PermissionDirective,
  RestService
} from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { TenantUserService } from '../../proxy/application/identity/tenant-user.service';
import { FormGroup, FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { PermissionManagementComponent } from '@abp/ng.permission-management';
import { finalize, catchError } from 'rxjs/operators';
import { throwError } from 'rxjs';
import { CommonModule } from '@angular/common';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { FormsModule } from '@angular/forms';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzPageHeaderModule } from 'ng-zorro-antd/page-header';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import type { UserImportResultDto } from '../../proxy/users/models';
import { MajorService } from '../../proxy/majors/major.service';
import type { MajorLookupDto } from '../../proxy/majors/dtos/models';

interface TenantDto {
  id?: string | null;
  name?: string;
}

interface RoleDto {
  id: string;
  name: string;
  isDefault?: boolean;
  isPublic?: boolean;
}

interface IdentityUserDto {
  id?: string;
  tenantId?: string;
  userName?: string;
  normalizedUserName?: string;
  name?: string;
  email?: string;
  normalizedEmail?: string;
  emailConfirmed?: boolean;
  phoneNumber?: string;
  phoneNumberConfirmed?: boolean;
  isActive?: boolean;
  twoFactorEnabled?: boolean;
  lockoutEnabled?: boolean;
  accessFailedCount?: number;
  lockoutEnd?: string;
  extraProperties?: Record<string, any>;
  className?: string;
  companyName?: string;
  course?: string;
  department?: string;
  employeeNumber?: string;
  grade?: string;
  industry?: string;
  major?: string;
  majorId?: string;
  majorName?: string;
  managementScope?: string;
  partnerSchool?: string;
  position?: string;
  remark?: string;
  roleType?: number;
  schoolId?: string;
  studentNumber?: string;
  title?: string;
  unifiedSocialCreditCode?: string;
  roleNames?: string[];
  [key: string]: any;
}

@Component({
  selector: 'app-identity-users',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
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
    NzDividerModule,
    NzGridModule,
    NzAlertModule,
    NzUploadModule,
    PermissionManagementComponent,
    FormsModule,
  ],
  providers: [],
  templateUrl: './identity-users.component.html',
  styleUrls: ['./identity-users.component.scss'],
})
export class IdentityUsersComponent implements OnInit {
  users = { items: [], totalCount: 0 } as PagedResultDto<IdentityUserDto>;
  isModalOpen = false;
  isLoading = signal(false);
  form!: FormGroup;
  selectedUser = {} as IdentityUserDto;
  pageIndex = 1;
  pageSize = 10;

  /** 租户筛选：'__global__' 表示全局，其余为租户 Id。去掉“全部”，只能二选一。 */
  readonly globalOptionValue = '__global__';
  tenantFilter: string = this.globalOptionValue;
  private usersRequestSeq = 0;
  tenants: TenantDto[] = [];
  tenantNames: Record<string, string> = {};

  /** 当前登录用户所属租户 ID；null 表示 host 全局管理员。 */
  currentTenantId: string | null = null;
  /** host 全局管理员可为 true；租户管理员为 false，只能管理本租户的用户。 */
  isHostAdmin = false;
  
  roles: RoleDto[] = [];
  selectedUserRole: string | null = null;
  userRolesMap: Record<string, string[]> = {};
  
  isPermissionModalOpen = false;
  permissionProviderKey = '';
  formError = '';

  /** Excel 批量导入 */
  importModalOpen = false;
  importing = false;
  downloadingTemplate = false;
  importFileList: NzUploadFile[] = [];
  importResult: UserImportResultDto | null = null;

  /** 各角色类型的必填字段说明（与后端 UserImportAppService.RequiredFieldsMapping 保持一致）。 */
  readonly importRequiredFields: { role: string; fields: string }[] = [
    { role: '联盟管理员', fields: '角色类型、姓名、登录账号、初始密码、手机号、工号' },
    { role: '院校管理员', fields: '角色类型、姓名、登录账号、初始密码、手机号、所属院校、工号' },
    { role: '教师', fields: '角色类型、姓名、登录账号、初始密码、手机号、所属院校、工号、所属院系/部门、所教专业' },
    { role: '学生', fields: '角色类型、姓名、登录账号、初始密码、手机号、所属院校、专业、学号、年级、班级' },
    { role: '企业用户', fields: '角色类型、姓名、登录账号、初始密码、手机号、邮箱、企业名称、统一社会信用代码、职位/岗位' },
  ];

  private readonly restService = inject(RestService);
  private readonly localization = inject(LocalizationService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly tenantUserService = inject(TenantUserService);
  private readonly majorService = inject(MajorService);
  private readonly message = inject(NzMessageService);
  private readonly configState = inject(ConfigStateService);

  readonly majors = signal<MajorLookupDto[]>([]);

  l(key: string): string {
    return this.localization.instant(key);
  }

  getRoleDisplayName(roleName: string | undefined): string {
    if (!roleName) return '';
    const displayNameKey = `::RoleName:${roleName}`;
    const displayName = this.l(displayNameKey);
    if (displayName && displayName !== displayNameKey) {
      return displayName;
    }
    return roleName;
  }

  ngOnInit(): void {
    const cu = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    const tenantId = cu?.['tenantId'];
    this.currentTenantId = (tenantId as string | null | undefined) ?? null;
    this.isHostAdmin = !this.currentTenantId;

    if (!this.isHostAdmin) {
      this.tenantFilter = this.currentTenantId ?? this.globalOptionValue;
    } else {
      // 超级管理员默认看全局，下拉只有“全局 + 各租户”，无“全部”。
      this.tenantFilter = this.globalOptionValue;
    }

    this.loadTenants();
    this.buildForm();
    this.majorService.getLookupList().subscribe({
      next: (list) => this.majors.set(list || []),
    });
  }

  loadTenants() {
    this.restService.request<any, TenantDto[]>({
      method: 'GET',
      url: '/api/public/tenants'
    }).subscribe((tenants) => {
      // /api/public/tenants 首项是 Id=null 的“全局”伪租户，下拉已有独立全局选项，此处过滤掉，避免重复显示。
      const realTenants = (tenants || []).filter(t => t.id != null);
      this.tenants = this.isHostAdmin
        ? realTenants
        : realTenants.filter(t => t.id === this.currentTenantId);
      (tenants || []).forEach(t => {
        if (t.id && t.name) {
          this.tenantNames[t.id] = t.name;
        }
      });
      this.loadUsers();
    });
  }

  loadUsers() {
    // 直接请求 + 序号 guard：避免 ListService 重复 hookToQuery 导致多路并发、后返回覆盖先返回（切换错乱）。
    const seq = ++this.usersRequestSeq;
    const isGlobal = this.tenantFilter === this.globalOptionValue;
    const effectiveTenantId = this.isHostAdmin
      ? (isGlobal ? undefined : this.tenantFilter)
      : this.currentTenantId;
    const onlyHost = this.isHostAdmin && isGlobal ? true : undefined;

    this.restService.request<any, PagedResultDto<IdentityUserDto>>({
      method: 'GET',
      url: '/api/app/tenant-user',
      params: {
        maxResultCount: this.pageSize,
        skipCount: (this.pageIndex - 1) * this.pageSize,
        tenantId: effectiveTenantId || undefined,
        onlyHost,
      },
    }).subscribe((response) => {
      if (seq !== this.usersRequestSeq) return;
      this.users = response;
      this.loadUsersRoles();
    });
  }

  loadUsersRoles() {
    if (!this.users.items?.length) return;
    
    this.users.items.forEach((user) => {
      if (user.id) {
        this.restService.request<any, string[]>({
          method: 'GET',
          url: `/api/app/tenant-user/roles-for-user/${user.id}`,
        }).subscribe((roles) => {
          this.userRolesMap[user.id!] = roles || [];
        });
      }
    });
  }

  getUserRoles(userId: string | undefined): string[] {
    if (!userId) return [];
    return this.userRolesMap[userId] || [];
  }

  getTenantName(tenantId: string | undefined | null): string {
    if (!tenantId) return '全局';
    if (this.tenantNames[tenantId]) return this.tenantNames[tenantId];
    const tenant = this.tenants.find(t => t.id === tenantId);
    return tenant?.name || tenantId;
  }

  /** 当前筛选对应的租户 Id：全局返回 null。 */
  get selectedTenantId(): string | null {
    return this.tenantFilter === this.globalOptionValue ? null : this.tenantFilter;
  }

  buildForm() {
    const filterTenantId = this.selectedTenantId;
    const tenantIdForForm = this.isHostAdmin
      ? (this.selectedUser.tenantId || filterTenantId || null)
      : this.currentTenantId;
    this.form = this.fb.group({
      userName: [this.selectedUser.userName || '', Validators.required],
      email: [this.selectedUser.email || '', [Validators.required, Validators.email]],
      password: [''],
      name: [this.selectedUser.name || '', Validators.required],
      phoneNumber: [this.selectedUser.phoneNumber || ''],
      isActive: [this.selectedUser.isActive ?? true],
      tenantId: [tenantIdForForm],
      roleName: [this.selectedUserRole, Validators.required],
      studentNumber: [this.selectedUser.studentNumber || ''],
      employeeNumber: [this.selectedUser.employeeNumber || ''],
      grade: [this.selectedUser.grade || ''],
      className: [this.selectedUser.className || ''],
      major: [this.selectedUser.major || ''],
      majorId: [this.selectedUser.majorId || null],
      department: [this.selectedUser.department || ''],
      position: [this.selectedUser.position || ''],
      title: [this.selectedUser.title || ''],
      companyName: [this.selectedUser.companyName || ''],
      course: [this.selectedUser.course || ''],
      industry: [this.selectedUser.industry || ''],
      managementScope: [this.selectedUser.managementScope || ''],
      partnerSchool: [this.selectedUser.partnerSchool || ''],
      schoolId: [this.selectedUser.schoolId || ''],
      remark: [this.selectedUser.remark || ''],
      unifiedSocialCreditCode: [this.selectedUser.unifiedSocialCreditCode || ''],
    });

    this.form.valueChanges.subscribe(() => {
      this.formError = '';
    });

    this.form.get('tenantId')?.valueChanges.subscribe((tenantId: string | null) => {
      this.loadRolesForTenant(tenantId);
    });
  }

  loadRolesForTenant(tenantId: string | null = this.form.get('tenantId')?.value ?? null) {
    const effectiveTenantId = this.isHostAdmin ? tenantId : this.currentTenantId;
    this.restService.request<any, { items: RoleDto[] }>({
      method: 'GET',
      url: '/api/app/tenant-role',
      params: {
        maxResultCount: 1000,
        tenantId: effectiveTenantId || undefined,
        onlyHost: this.isHostAdmin && !effectiveTenantId ? true : undefined,
      }
    }).subscribe((response) => {
      const items = response.items || [];
      // 联盟管理员是全局角色，建租户用户时不提供该选项。
      // 目标为某租户（租户管理员，或 host 指定租户建用户）时过滤掉 LeagueAdmin；
      // host 建全局用户（tenantId 为空）时保留。
      this.roles = effectiveTenantId
        ? items.filter(role => role.name !== 'LeagueAdmin')
        : items;
      const availableRoleNames = new Set(this.roles.map(role => role.name));
      const selectedRoleName = this.form.get('roleName')?.value as string | null;
      if (selectedRoleName && !availableRoleNames.has(selectedRoleName)) {
        this.selectedUserRole = null;
        this.form.patchValue({ roleName: null }, { emitEvent: false });
      }
    });
  }

  createUser() {
    this.selectedUser = {} as IdentityUserDto;
    this.selectedUserRole = null;
    if (!this.isHostAdmin) {
      // 确保新建用户的 tenantId 锁定为当前租户
      this.selectedUser.tenantId = this.currentTenantId ?? undefined;
    } else {
      // host 按当前筛选预填：选中某租户时新建用户默认归属该租户，选中全局则为全局用户。不改动列表筛选。
      this.selectedUser.tenantId = this.selectedTenantId ?? undefined;
    }
    this.buildForm();
    this.loadRolesForTenant(this.isHostAdmin ? this.selectedUser.tenantId ?? null : this.currentTenantId);
    this.isModalOpen = true;
  }

  editUser(user: IdentityUserDto) {
    this.selectedUser = user;
    // 不改动列表筛选 tenantFilter，只按用户自身租户加载角色。
    this.buildForm();
    
    this.restService.request<any, string[]>({
      method: 'GET',
      url: `/api/app/tenant-user/roles-for-user/${user.id}`,
    }).subscribe((roles) => {
      this.selectedUserRole = roles?.[0] || null;
      this.form.patchValue({ roleName: this.selectedUserRole }, { emitEvent: false });
      this.loadRolesForTenant(this.isHostAdmin ? (user.tenantId || null) : this.currentTenantId);
      this.isModalOpen = true;
    });
  }

  save() {
    if (!this.form || this.form.invalid) {
      this.form?.markAllAsTouched();
      Object.values(this.form?.controls ?? {}).forEach(c => c.updateValueAndValidity());
      return;
    }

    const { tenantId: formTenantId, roleName, password, email, ...formValue } = this.form.value;
    const tenantId = this.isHostAdmin ? formTenantId : this.currentTenantId;

    // 联盟管理员是全局角色，租户内不允许分配，前端先拦截避免无效请求。
    if (tenantId && roleName === 'LeagueAdmin') {
      this.formError = '租户内不允许分配“联盟管理员”角色，联盟管理员为全局账号。';
      return;
    }

    this.isLoading.set(true);

    const handleError = (err: any) => {
      this.isLoading.set(false);
      this.formError = err?.error?.error?.message || err?.message || this.l('::SaveFailed');
      return throwError(() => err);
    };

    if (this.selectedUser.id) {
      const payload: any = { 
        ...formValue, 
        email,
        surname: formValue.surname || '-',
        roleNames: roleName ? [roleName] : []
      };
      
      if (password) {
        payload.password = password;
      }
      
      this.restService.request<any, IdentityUserDto>({
        method: 'PUT',
        url: `/api/app/tenant-user/${this.selectedUser.id}`,
        body: payload,
      }).pipe(
        catchError(handleError)
      ).subscribe({
        next: () => {
          this.isLoading.set(false);
          this.isModalOpen = false;
          this.formError = '';
          this.loadUsers();
        }
      });
    } else {
      const payload: any = { 
        ...formValue, 
        emailAddress: email,
        surname: '-',
        roleNames: roleName ? [roleName] : []
      };
      
      if (tenantId) {
        payload.tenantId = tenantId;
      }
      if (password) {
        payload.password = password;
      }
      
      this.restService.request<any, IdentityUserDto>({
        method: 'POST',
        url: '/api/app/tenant-user/user-for-tenant',
        body: payload,
      }).pipe(
        catchError(handleError)
      ).subscribe({
        next: () => {
          this.isLoading.set(false);
          this.isModalOpen = false;
          this.formError = '';
          this.loadUsers();
        }
      });
    }
  }

  delete(id: string, userName: string) {
    this.confirmation.warn(
      this.l('::UserDeletionConfirmationMessage').replace('{0}', userName),
      this.l('::AreYouSure')
    ).subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.restService.request<any, void>({
          method: 'DELETE',
          url: `/api/app/tenant-user/${id}`,
        }).subscribe(() => this.loadUsers());
      }
    });
  }

  onTenantFilterChange(value: string | null): void {
    const next = value || this.globalOptionValue;
    if (!this.isHostAdmin && next !== this.currentTenantId) {
      // 租户管理员锁定本租户，不允许切换。
      this.tenantFilter = this.currentTenantId ?? this.globalOptionValue;
      return;
    }
    this.tenantFilter = next;
    this.pageIndex = 1;
    this.loadUsers();
  }

  onPageIndexChange(index: number): void {
    this.pageIndex = index;
    this.loadUsers();
  }

  onPageSizeChange(size: number): void {
    this.pageSize = size;
    this.pageIndex = 1;
    this.loadUsers();
  }

  openPermissions(user: IdentityUserDto) {
    this.permissionProviderKey = user.id ?? '';
    setTimeout(() => {
      this.isPermissionModalOpen = true;
    });
  }

  // ===== Excel 批量导入 =====
  openImportModal(): void {
    this.importModalOpen = true;
    this.importFileList = [];
    this.importResult = null;
  }

  closeImportModal(): void {
    if (this.importing) return;
    this.importModalOpen = false;
    this.importFileList = [];
    this.importResult = null;
  }

  beforeImportUpload = (file: NzUploadFile): boolean => {
    const name = (file.name || '').toLowerCase();
    if (!name.endsWith('.xlsx') && !name.endsWith('.xls')) {
      this.message.warning('仅支持 Excel 文件（.xlsx / .xls）');
      return false;
    }
    this.importFileList = [file];
    return false;
  };

  downloadImportTemplate(): void {
    this.downloadingTemplate = true;
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      url: '/api/app/user-import/import-template',
    }).pipe(finalize(() => (this.downloadingTemplate = false))).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `用户导入模板_${this.todayStr()}.xlsx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      },
      error: err => this.message.error(this.extractErrorMessage(err, '模板下载失败')),
    });
  }

  importXlsx(): void {
    const file = this.importFileList[0];
    if (!file) {
      this.message.warning('请先选择要导入的 Excel 文件');
      return;
    }
    this.importing = true;
    this.importResult = null;
    const reader = new FileReader();
    reader.onload = () => {
      // 后端只接受 Base64 字符串（System.Text.Json 无法把数字数组转成 byte[]）。
      const dataUrl = reader.result as string;
      const base64 = dataUrl.split(',')[1] || '';
      this.restService.request<any, UserImportResultDto>({
        method: 'POST',
        url: '/api/app/user-import/import',
        body: { fileBase64: base64, fileName: file.name },
      }).subscribe({
        next: result => {
          this.importing = false;
          const ok = result.successCount ?? 0;
          const fail = result.failCount ?? 0;
          if (fail > 0) {
            this.importResult = result;
            this.message.warning(`导入完成：成功 ${ok} 条，失败 ${fail} 条，详见下方明细`);
          } else {
            this.importModalOpen = false;
            this.importFileList = [];
            this.importResult = null;
            this.message.success(`导入完成：成功 ${ok} 条`);
          }
          this.loadUsers();
        },
        error: err => {
          this.importing = false;
          this.importResult = null;
          this.message.error(this.extractErrorMessage(err, '导入失败'));
        },
      });
    };
    reader.onerror = () => {
      this.importing = false;
      this.message.error('读取文件失败，请重试');
    };
    reader.readAsDataURL(file as any);
  }

  private todayStr(): string {
    const d = new Date();
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}`;
  }

  private extractErrorMessage(err: any, fallback: string): string {
    return err?.error?.error?.message || err?.error?.message || err?.message || fallback;
  }
}
