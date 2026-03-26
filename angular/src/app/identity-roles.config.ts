import { provideAppInitializer, inject } from '@angular/core';
import { ReplaceableComponentsService } from '@abp/ng.core';
import { eIdentityComponents } from '@abp/ng.identity';
import { IdentityRolesComponent } from './admin/identity-roles/identity-roles.component';

function initIdentityRoles() {
  const replaceableComponents = inject(ReplaceableComponentsService);
  replaceableComponents.add({
    key: eIdentityComponents.Roles,
    component: IdentityRolesComponent,
  });
}

export const IDENTITY_ROLES_PROVIDER = [
  provideAppInitializer(() => {
    initIdentityRoles();
  }),
];
