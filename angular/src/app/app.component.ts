import { Component, CUSTOM_ELEMENTS_SCHEMA, AfterViewInit, Renderer2, inject, NgZone } from '@angular/core';
import { DynamicLayoutComponent } from '@abp/ng.core';
import { AuthErrorModalComponent } from './core/auth/auth-error-modal.component';

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar></abp-loader-bar>
    <abp-dynamic-layout></abp-dynamic-layout>
    <app-auth-error-modal></app-auth-error-modal>
  `,
  standalone: true,
  imports: [DynamicLayoutComponent, AuthErrorModalComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
export class AppComponent {}