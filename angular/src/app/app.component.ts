import { Component, CUSTOM_ELEMENTS_SCHEMA, OnInit, inject, signal } from '@angular/core';
import { Router, NavigationStart, NavigationEnd, NavigationCancel, NavigationError } from '@angular/router';
import { DOCUMENT } from '@angular/common';
import { DynamicLayoutComponent, eLayoutType } from '@abp/ng.core';
import { AuthErrorModalComponent } from './core/auth/auth-error-modal.component';
import { QuickNavComponent } from './shared/quick-nav/quick-nav.component';
import { filter, take } from 'rxjs/operators';

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar></abp-loader-bar>
    <abp-dynamic-layout [defaultLayout]="defaultLayout"></abp-dynamic-layout>
    <app-auth-error-modal></app-auth-error-modal>
    <!-- 全局快速导航面板：任意页面 Ctrl/Cmd + K 弹出 -->
    <app-quick-nav></app-quick-nav>
    @if (navigating()) {
      <!-- 路由切换 loading：abp-loader-bar 只跟 HTTP 请求，懒加载 chunk / 守卫耗时
           期间页面是空白的，这里补一个不拦截点击的品牌化提示（对齐墨青绿主题） -->
      <div class="route-loading-veil" aria-hidden="true">
        <div class="route-loading-box">
          <span class="route-loading-ring"></span>
          <span>页面加载中…</span>
        </div>
      </div>
    }
  `,
  styles: [
    `.route-loading-veil{position:fixed;inset:0;z-index:2000;display:flex;align-items:center;justify-content:center;background:rgba(248,249,246,.5);backdrop-filter:blur(3px);-webkit-backdrop-filter:blur(3px);pointer-events:none;}`,
    `.route-loading-box{display:flex;align-items:center;gap:10px;padding:11px 18px;border-radius:999px;background:rgba(255,255,255,.94);border:1px solid #e1e7e0;box-shadow:0 16px 36px -18px rgba(24,52,68,.3);color:#183444;font-size:13px;letter-spacing:.3px;}`,
    `.route-loading-ring{width:16px;height:16px;border-radius:50%;border:2px solid rgba(8,127,122,.18);border-top-color:#087f7a;animation:route-spin .8s linear infinite;}`,
    `@keyframes route-spin{to{transform:rotate(360deg);}}`,
    `@media (prefers-reduced-motion: reduce){.route-loading-ring{animation:none;}}`,
  ],
  standalone: true,
  imports: [DynamicLayoutComponent, AuthErrorModalComponent, QuickNavComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
export class AppComponent implements OnInit {
  /**
   * eLayoutType 是 declare const enum（编译期内联，没有运行时对象），
   * 既不能在模板里当枚举用，也不能作为值赋给类字段；这里用类型断言落到字面量上。
   */
  protected readonly defaultLayout = 'application' as eLayoutType;

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

    // 懒加载 chunk 加载失败自愈：发版/重启后浏览器拿着旧 index 引用已不存在的
    // hash chunk，路由会 NavigationError（ChunkLoadError）并停在空白页——这也是
    // "重启后访问课程等页面 404、多刷几次才好"的成因之一。会话内自动刷新一次，
    // 让浏览器拿到最新 index；加标记防循环（新 index 下标记仍在则不再刷）。
    this.router.events
      .pipe(filter(event => event instanceof NavigationError))
      .subscribe(event => {
        const err = (event as NavigationError).error;
        const msg = String((err as any)?.message ?? err ?? '');
        if (/ChunkLoadError|Loading chunk|Failed to fetch dynamically imported module/i.test(msg)) {
          try {
            if (!sessionStorage.getItem('kh_chunk_reloaded')) {
              sessionStorage.setItem('kh_chunk_reloaded', '1');
              this.document.location.reload();
            }
          } catch {
            // sessionStorage 不可用（如隐私模式）时不自动刷，避免循环。
          }
        }
      });

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