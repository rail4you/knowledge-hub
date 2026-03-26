import { provideAppInitializer, inject } from '@angular/core';
import { ReplaceableComponentsService } from '@abp/ng.core';
import { eIdentityComponents } from '@abp/ng.identity';
import { IdentityUsersComponent } from './admin/identity-users/identity-users.component';

function initIdentityUsers() {
  const replaceableComponents = inject(ReplaceableComponentsService);
  replaceableComponents.add({
    key: eIdentityComponents.Users,
    component: IdentityUsersComponent,
  });
}

export const IDENTITY_USERS_PROVIDER = [
  provideAppInitializer(() => {
    initIdentityUsers();
  }),
];
