import { Component } from '@angular/core';
import { DynamicLayoutComponent } from '@abp/ng.core';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { AuthErrorModalComponent } from './core/auth/auth-error-modal.component';

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar />
    <abp-dynamic-layout />
    <app-auth-error-modal></app-auth-error-modal>
  `,
  imports: [LoaderBarComponent, DynamicLayoutComponent, AuthErrorModalComponent],
})
export class AppComponent {}
