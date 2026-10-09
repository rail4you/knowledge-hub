import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { resolvePostLoginUrl } from './post-login.guard';

/**
 * 登录后中转页（`/post-login`）。
 *
 * 正常情况下 `postLoginGuard` 在路由层就已按角色跳走，本组件不会被实例化；
 * 保留组件内的二次分发作为兜底（例如守卫执行时极端竞态），避免用户卡在空白页。
 */
@Component({
  selector: 'app-post-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="post-login"><i class="fa fa-spinner fa-spin"></i><span>正在进入系统…</span></div>`,
  styles: [
    `.post-login{min-height:60vh;display:flex;gap:10px;align-items:center;justify-content:center;color:#697e88;font-size:15px;}`,
  ],
})
export class PostLoginComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly configState = inject(ConfigStateService);
  private readonly router = inject(Router);

  ngOnInit(): void {
    const target = resolvePostLoginUrl(this.authService, this.configState) ?? '/account/login';
    void this.router.navigate([target], { replaceUrl: true });
  }
}
