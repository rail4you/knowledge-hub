import { Component, OnInit, inject, signal } from '@angular/core';
import { 
  ListService, 
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
    PermissionManagementComponent,
    FormsModule,
  ],
  providers: [ListService],
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
  
  tenants: TenantDto[] = [];
  selectedTenantId: string | null = null;
  
  roles: RoleDto[] = [];
  selectedUserRole: string | null = null;
  userRolesMap: Record<string, string[]> = {};
  
  isPermissionModalOpen = false;
  permissionProviderKey = '';
  formError = '';

  private readonly list = inject(ListService);
  private readonly restService = inject(RestService);
  private readonly localization = inject(LocalizationService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly tenantUserService = inject(TenantUserService);
  private readonly majorService = inject(MajorService);

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
      this.tenants = tenants;
      this.loadUsers();
    });
  }

  loadUsers() {
    const userStreamCreator = (query: any) =>
      this.restService.request<any, PagedResultDto<IdentityUserDto>>({
        method: 'GET',
        url: '/api/app/tenant-user',
        params: {
          ...query,
          maxResultCount: this.pageSize,
          skipCount: (this.pageIndex - 1) * this.pageSize,
          tenantId: this.selectedTenantId || undefined,
        },
      });

    this.list.hookToQuery(userStreamCreator).subscribe((response) => {
      this.users = response;
      this.loadUsersRoles();
    });
    this.list.get();
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
    if (!tenantId) return 'Host';
    const tenant = this.tenants.find(t => t.id === tenantId);
    return tenant?.name || tenantId;
  }

  buildForm() {
    this.form = this.fb.group({
      userName: [this.selectedUser.userName || '', Validators.required],
      email: [this.selectedUser.email || '', [Validators.required, Validators.email]],
      password: [''],
      name: [this.selectedUser.name || ''],
      phoneNumber: [this.selectedUser.phoneNumber || ''],
      isActive: [this.selectedUser.isActive ?? true],
      tenantId: [this.selectedTenantId || null],
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
    this.restService.request<any, { items: RoleDto[] }>({
      method: 'GET',
      url: '/api/app/tenant-role',
      params: { 
        maxResultCount: 1000,
        tenantId: tenantId || undefined
      }
    }).subscribe((response) => {
      this.roles = response.items || [];
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
    this.selectedTenantId = null;
    this.selectedUserRole = null;
    this.buildForm();
    this.loadRolesForTenant(this.selectedTenantId);
    this.isModalOpen = true;
  }

  editUser(user: IdentityUserDto) {
    this.selectedUser = user;
    this.selectedTenantId = user.tenantId || null;
    this.buildForm();
    
    this.restService.request<any, string[]>({
      method: 'GET',
      url: `/api/app/tenant-user/roles-for-user/${user.id}`,
    }).subscribe((roles) => {
      this.selectedUserRole = roles?.[0] || null;
      this.form.patchValue({ roleName: this.selectedUserRole }, { emitEvent: false });
      this.loadRolesForTenant(this.selectedTenantId);
      this.isModalOpen = true;
    });
  }

  save() {
    if (!this.form || this.form.invalid) {
      return;
    }

    const { tenantId, roleName, password, email, ...formValue } = this.form.value;
    
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
          this.list.get();
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
          this.list.get();
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
        }).subscribe(() => this.list.get());
      }
    });
  }

  onTenantFilterChange(tenantId: string | null): void {
    this.selectedTenantId = tenantId;
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
    this.permissionProviderKey = user.id;
    setTimeout(() => {
      this.isPermissionModalOpen = true;
    });
  }
}
