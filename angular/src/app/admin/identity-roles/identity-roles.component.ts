import { Component, OnInit, inject, signal } from '@angular/core';
import { ListService, LocalizationService, LocalizationPipe, PermissionDirective } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { IdentityRoleService } from './identity-role.service';
import type { IdentityRoleDto, IdentityRoleCreateDto, IdentityRoleUpdateDto } from './models';
import { FormGroup, FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
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

@Component({
  selector: 'app-identity-roles',
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

  private readonly list = inject(ListService);
  private readonly roleService = inject(IdentityRoleService);
  private readonly localization = inject(LocalizationService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);

  l(key: string): string {
    return this.localization.instant(key);
  }

  getRoleDisplayName(roleName: string | undefined): string {
    if (!roleName) return '';
    return this.localization.instant(`RoleName:${roleName}`);
  }

  ngOnInit(): void {
    this.buildForm();
    const roleStreamCreator = (query) =>
      this.roleService.getList({
        ...query,
        maxResultCount: this.pageSize,
        skipCount: (this.pageIndex - 1) * this.pageSize,
      });

    this.list.hookToQuery(roleStreamCreator).subscribe((response) => {
      this.roles = response;
    });
  }

  buildForm() {
    this.form = this.fb.group({
      name: [this.selectedRole.name || '', Validators.required],
      isDefault: [this.selectedRole.isDefault || false],
      isPublic: [this.selectedRole.isPublic !== false],
    });
  }

  createRole() {
    this.selectedRole = {} as IdentityRoleDto;
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

    if (this.selectedRole.id) {
      this.roleService.update(this.selectedRole.id, formValue as IdentityRoleUpdateDto).subscribe(() => {
        this.isModalOpen = false;
        this.form = undefined;
        this.list.get();
      });
    } else {
      this.roleService.create(formValue as IdentityRoleCreateDto).subscribe(() => {
        this.isModalOpen = false;
        this.form = undefined;
        this.list.get();
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
    this.list.get();
  }

  onPageSizeChange(size: number): void {
    this.pageSize = size;
    this.pageIndex = 1;
    this.list.get();
  }
}
