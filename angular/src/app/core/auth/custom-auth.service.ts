import { Injectable, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { Params } from '@angular/router';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { AbpOAuthService } from '@abp/ng.oauth';

/**
 * 自定义 AuthService：
 * 在登出时清除 `__host_login` cookie，避免登出后再点首页「登录」时仍走宿主模式。
 *
 * Bug 背景：
 *   1. 用户访问 /admin-login 时，前端 LoginComponent 会写入 `__host_login=1` cookie，
 *      告知后端 Login.cshtml 渲染宿主登录表单；
 *   2. 正常登录流程里，登录成功时（后端 KnowledgeHubLoginModel.OnPostAsync）
 *      已经会清除这个 cookie；
 *   3. 但用户可能「仅访问了 /admin-login 但未提交」或「在 LeptonX 用户菜单登出」，
 *      此时 cookie 不会被清除。下次从首页点击登录时，后端会再次进入宿主模式，
 *      反复显示系统管理员登录页。
 *
 * 此服务在 logout() 触发时同步清除该 cookie，作为纵深防御，覆盖所有登出入口
 * （首页、LeptonX 用户菜单、租户主页、学生端布局等）。
 */
@Injectable()
export class CustomAuthService extends AbpOAuthService {
  // 注意：不能用 'document' 命名——父类 AbpOAuthService 已声明为 private。
  // 用 'doc' 避免与父类同名属性冲突。
  private readonly doc = inject(DOCUMENT);

  override logout(queryParams?: Params): Observable<any> {
    // 必须在 super.logout() 之前清除：logout 内部会触发 window.location 跳转去
    // OAuth end-session endpoint，跳转后当前页面 JS 上下文就不可用了。
    this.clearHostLoginCookie();
    return super.logout(queryParams).pipe(
      // 兜底：即使 logout 内部走非跳转分支（如 SSR），subscribe 时也确保 cookie 被清。
      tap(() => this.clearHostLoginCookie()),
    );
  }

  private clearHostLoginCookie(): void {
    try {
      this.doc.cookie =
        '__host_login=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
    } catch {
      // 跨域或隐私模式下写入 cookie 可能抛错，登出流程不应因此失败。
    }
  }
}
