import { Component, OnInit, inject, signal } from '@angular/core';
import { 
  ListService, 
  LocalizationService, 
  LocalizationPipe, 
  PermissionDirective, 
  RestService 
} from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { FormGroup, FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { PermissionManagementComponent } from '@abp/ng.permission-management';
import { finalize } from 'rxjs/operators';
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
import { NzDividerModule } from 'ng-zorro-antd/divider';

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
  userName?: string;
  email?: string;
  name?: string;
  surname?: string;
  isActive?: boolean;
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
    NzCheckboxModule,
    NzDividerModule,
    PermissionManagementComponent,
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
  selectedTenantIdForRoles: string | null = null;
  
  roles: RoleDto[] = [];
  selectedUserRoles: string[] = [];
  
  isPermissionModalOpen = false;
  permissionProviderKey = '';

  private readonly list = inject(ListService);
  private readonly restService = inject(RestService);
  private readonly localization = inject(LocalizationService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);

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
        },
      });

    this.list.hookToQuery(userStreamCreator).subscribe((response) => {
      this.users = response;
    });
  }

  buildForm() {
    this.form = this.fb.group({
      userName: [this.selectedUser.userName || '', Validators.required],
      email: [this.selectedUser.email || '', [Validators.required, Validators.email]],
      password: [''],
      name: [this.selectedUser.name || ''],
      surname: [this.selectedUser.surname || ''],
      isActive: [this.selectedUser.isActive ?? true],
      tenantId: [this.selectedTenantId || null],
    });
  }

  loadRolesForTenant() {
    const tenantId = this.selectedTenantIdForRoles;
    
    this.restService.request<any, { items: RoleDto[] }>({
      method: 'GET',
      url: '/api/app/tenant-role',
      params: { 
        maxResultCount: 1000,
        tenantId: tenantId || undefined
      }
    }).subscribe((response) => {
      this.roles = response.items || [];
    });
  }

  onTenantChangeForRoles(tenantId: string | null) {
    this.selectedTenantIdForRoles = tenantId;
    this.loadRolesForTenant();
  }

  isRoleSelected(roleName: string): boolean {
    return this.selectedUserRoles.includes(roleName);
  }

  toggleRole(roleName: string) {
    const index = this.selectedUserRoles.indexOf(roleName);
    if (index > -1) {
      this.selectedUserRoles.splice(index, 1);
    } else {
      this.selectedUserRoles.push(roleName);
    }
  }

  createUser() {
    this.selectedUser = {} as IdentityUserDto;
    this.selectedTenantId = null;
    this.selectedTenantIdForRoles = null;
    this.selectedUserRoles = [];
    this.buildForm();
    this.loadRolesForTenant();
    this.isModalOpen = true;
  }

  editUser(user: IdentityUserDto) {
    this.selectedUser = user;
    this.selectedTenantId = user.tenantId || null;
    this.selectedTenantIdForRoles = user.tenantId || null;
    this.buildForm();
    
    this.restService.request<any, { items: RoleDto[] }>({
      method: 'GET',
      url: `/api/identity/users/${user.id}/roles`,
    }).subscribe((response) => {
      this.selectedUserRoles = response.items.map(r => r.name || '');
      this.loadRolesForTenant();
      this.isModalOpen = true;
    });
  }

  save() {
    if (!this.form || this.form.invalid) {
      return;
    }

    const formValue = { ...this.form.value, roleNames: this.selectedUserRoles };
    this.isLoading.set(true);

    if (this.selectedUser.id) {
      this.restService.request<any, IdentityUserDto>({
        method: 'PUT',
        url: `/api/app/tenant-user/${this.selectedUser.id}`,
        body: formValue,
      }).subscribe({
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
      this.restService.request<any, IdentityUserDto>({
        method: 'POST',
        url: '/api/app/tenant-user',
        body: formValue,
      }).subscribe({
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
