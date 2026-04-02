import { provideAppInitializer, inject } from '@angular/core';
import { ReplaceableComponentsService } from '@abp/ng.core';
import { LoginComponent } from './login.component';

function initLoginComponent() {
  const replaceableComponents = inject(ReplaceableComponentsService);
  replaceableComponents.add({
    key: 'Account.LoginComponent',
    component: LoginComponent,
  });
}

export const LOGIN_PROVIDER = [
  provideAppInitializer(() => {
    initLoginComponent();
  }),
];
