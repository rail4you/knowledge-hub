import { Component, CUSTOM_ELEMENTS_SCHEMA, OnInit, inject, signal } from '@angular/core';
import { Router, NavigationStart, NavigationEnd, NavigationCancel, NavigationError } from '@angular/router';
import { DOCUMENT } from '@angular/common';
import { DynamicLayoutComponent } from '@abp/ng.core';
import { AuthErrorModalComponent } from './core/auth/auth-error-modal.component';
import { QuickNavComponent } from './shared/quick-nav/quick-nav.component';
import { filter, take } from 'rxjs/operators';

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar></abp-loader-bar>
    <abp-dynamic-layout defaultLayout="application"></abp-dynamic-layout>
    <app-auth-error-modal></app-auth-error-modal>
    <!-- 全局快速导航面板：任意页面 Ctrl/Cmd + K 弹出 -->
    <app-quick-nav></app-quick-nav>
    @if (navigating()) {
      <!-- 路由切换 loading：abp-loader-bar 只跟 HTTP 请求，懒加载 chunk / 守卫耗时
           期间页面是空白的，这里补一个不拦截点击的半透明提示 -->
      <div class="route-loading-veil" aria-hidden="true">
        <div class="route-loading-box">
          <i class="fa fa-spinner fa-spin"></i>
          <span>页面加载中…</span>
        </div>
      </div>
    }
  `,
  styles: [
    `.route-loading-veil{position:fixed;inset:0;z-index:2000;display:flex;align-items:center;justify-content:center;background:rgba(255,255,255,.55);pointer-events:none;}`,
    `.route-loading-box{display:flex;align-items:center;gap:10px;padding:12px 20px;border-radius:12px;background:#fff;border:1px solid #e7edf5;box-shadow:0 12px 32px -12px rgba(19,35,58,.25);color:#13233a;font-size:14px;}`,
    `.route-loading-box .fa{color:#1890ff;font-size:16px;}`,
  ],
  standalone: true,
  imports: [DynamicLayoutComponent, AuthErrorModalComponent, QuickNavComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
export class AppComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);

  /** 路由导航进行中（懒加载 chunk / 守卫 / 目标页首屏期间为 true）。 */
  readonly navigating = signal(false);
  private navTimer: ReturnType<typeof setTimeout> | null = null;

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

    // 全局路由 loading：NavigationStart 后 300ms 若还没落定才显示，
    // 普通快速跳转不闪；落定/取消/失败一律隐藏。pointer-events:none，永不卡点击。
    this.router.events.subscribe(event => {
      if (event instanceof NavigationStart) {
        this.clearNavTimer();
        this.navTimer = setTimeout(() => this.navigating.set(true), 300);
      } else if (
        event instanceof NavigationEnd ||
        event instanceof NavigationCancel ||
        event instanceof NavigationError
      ) {
        this.clearNavTimer();
        this.navigating.set(false);
      }
    });
  }

  private clearNavTimer(): void {
    if (this.navTimer) {
      clearTimeout(this.navTimer);
      this.navTimer = null;
    }
  }
}