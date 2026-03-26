import { Component, OnInit, inject, signal } from '@angular/core';
import { ListService, LocalizationService, LocalizationPipe, PermissionDirective, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { IdentityRoleService } from './identity-role.service';
import type { IdentityRoleDto, IdentityRoleCreateDto, IdentityRoleUpdateDto } from './models';
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

interface TenantDto {
  id?: string | null;
  name?: string;
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

  private readonly list = inject(ListService);
  private readonly roleService = inject(IdentityRoleService);
  private readonly localization = inject(LocalizationService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly restService = inject(RestService);

  l(key: string): string {
    return this.localization.instant(key);
  }

  getRoleDisplayName(roleName: string | undefined): string {
    if (!roleName) return '';
    const translated = this.localization.instant(`::RoleName:${roleName}`);
    return translated || roleName;
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
}
