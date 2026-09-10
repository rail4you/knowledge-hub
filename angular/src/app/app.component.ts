import { Component, CUSTOM_ELEMENTS_SCHEMA, OnInit, inject } from '@angular/core';
import { Router, NavigationEnd, NavigationCancel, NavigationError } from '@angular/router';
import { DOCUMENT } from '@angular/common';
import { DynamicLayoutComponent } from '@abp/ng.core';
import { AuthErrorModalComponent } from './core/auth/auth-error-modal.component';
import { filter, take } from 'rxjs/operators';

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar></abp-loader-bar>
    <abp-dynamic-layout defaultLayout="application"></abp-dynamic-layout>
    <app-auth-error-modal></app-auth-error-modal>
  `,
  standalone: true,
  imports: [DynamicLayoutComponent, AuthErrorModalComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
export class AppComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);

  ngOnInit() {
    // 冷启动（登录/退出回跳）时 index.html 的品牌闪屏一直盖在最上层，
    // 首个导航落定（懒加载 chunk + 守卫全部完成、目标组件已实例化）后再移除，
    // 用户不会看到"只有侧边菜单、内容区空白"的不完整管理页。
    // NavigationCancel/Error 同样移除，避免把用户锁在闪屏后；另有 20s 兜底。
    const removeSplash = () => {
      try {
        this.document.getElementById('boot-splash')?.remove();
      } catch {
        /* ignore */
      }
    };
    this.router.events
      .pipe(
        filter(
          event =>
            event instanceof NavigationEnd ||
            event instanceof NavigationCancel ||
            event instanceof NavigationError
        ),
        take(1)
      )
      .subscribe(removeSplash);
    setTimeout(removeSplash, 20000);
  }
}