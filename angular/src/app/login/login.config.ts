import { provideAppInitializer, inject } from '@angular/core';
import { ReplaceableComponentsService } from '@abp/ng.core';
import { eAccountComponents } from '@abp/ng.account';
import { LoginComponent } from './login.component';

function initLoginComponent() {
  const replaceableComponents = inject(ReplaceableComponentsService);
  replaceableComponents.add({
    key: eAccountComponents.Login,
    component: LoginComponent,
  });
}

export const LOGIN_PROVIDER = [
  provideAppInitializer(() => {
    initLoginComponent();
  }),
];
