import { Injectable, inject } from '@angular/core';
import { ConfigStateService, LocalizationService, PermissionService, RestService, RoutesService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Observable, of } from 'rxjs';
import { catchError, filter, map, take } from 'rxjs/operators';

/** 某个用户的一级菜单可见性配置（后端 MenuVisibilityDto）。 */
export interface MenuVisibilityDto {
  userId: string;
  userName: string;
  displayName?: string | null;
  hiddenMenus: string[];
}

/** 老师列表行（后端 MenuVisibilityListItemDto）——「菜单可见性管理」页用。 */
export interface MenuVisibilityListItem {
  userId: string;
  userName: string;
  displayName?: string | null;
  tenantId?: string | null;
  tenantName?: string | null;
  hiddenMenus: string[];
  hiddenCount?: number;
}

/** 可配置的一级菜单项（前端从路由树推导，后端只存菜单名）。 */
export interface TopLevelMenuItem {
  /** 路由节点名，如 `::Menu:Employment` —— 后端存的就是它。 */
  name: string;
  /** 本地化后的显示名。 */
  displayName: string;
  iconClass?: string;
  /** 一级菜单自身的路径（分组节点没有）。 */
  path?: string;
  /** 组内菜单数量（分组节点用来说明"这个分组里有几个页面"）。 */
  childCount: number;
}

/**
 * 按老师控制侧边栏一级菜单的可见性。
 *
 * <p>两个职责：</p>
 * <ol>
 *   <li><b>读取/保存配置</b>：`/api/app/menu-visibility/*`（租户管理员配置老师，用户读自己的）；</li>
 *   <li><b>把配置落到侧边栏</b>：替换 `RoutesService.hide`，让被隐藏的一级菜单（含其全部子项）
 *       从 ABP 的 `visible$` 里消失 —— LeptonX 侧边栏就是订阅 `visible$` 渲染的，所以这是唯一改动点。</li>
 * </ol>
 *
 * <p>为什么改 `hide` 而不是 `routes.remove()`：`remove` 会把节点从路由树里摘掉，
 * 深链页会因此丢掉布局解析（`DynamicLayoutComponent` 找不到节点会落到默认布局）；
 * 而且“特教分组”等 Feature 开关会在启动后才 `add()` 菜单，remove 掉的节点会被重新加回来。
 * `hide` 是每次 `publish()` 都会调用的判定函数，天然对所有后续 `add()/refresh()` 生效。</p>
 */
@Injectable({ providedIn: 'root' })
export class MenuVisibilityService {
  private readonly rest = inject(RestService);
  private readonly routes = inject(RoutesService);
  private readonly configState = inject(ConfigStateService);
  private readonly localization = inject(LocalizationService);
  private readonly permission = inject(PermissionService);

  /** 当前用户被隐藏的一级菜单名。 */
  private hiddenNames = new Set<string>();
  private hidePatched = false;

  // ── 配置读写 ─────────────────────────────────────────────

  /** 当前用户被隐藏的一级菜单名（未登录 / 失败都返回空列表，不阻塞启动）。 */
  getMyHiddenMenus(): Observable<string[]> {
    return this.rest
      .request<any, string[]>({ method: 'GET', url: '/api/app/menu-visibility/my-hidden-menus' })
      .pipe(
        map(list => (list || []).filter(Boolean)),
        catchError(() => of([])),
      );
  }

  /** 老师列表（「菜单可见性管理」页）。 */
  getTeachers(params: {
    skipCount?: number;
    maxResultCount?: number;
    filter?: string;
    tenantId?: string | null;
  }): Observable<PagedResultDto<MenuVisibilityListItem>> {
    return this.rest.request<any, PagedResultDto<MenuVisibilityListItem>>({
      method: 'GET',
      url: '/api/app/menu-visibility/teachers',
      params: {
        skipCount: params.skipCount ?? 0,
        maxResultCount: params.maxResultCount ?? 10,
        filter: params.filter || undefined,
        tenantId: params.tenantId || undefined,
      },
    });
  }

  /** 读取某个老师被隐藏的一级菜单名（租户管理员）。 */
  getUserMenus(userId: string): Observable<MenuVisibilityDto> {
    return this.rest.request<any, MenuVisibilityDto>({
      method: 'GET',
      url: `/api/app/menu-visibility/user-menus/${userId}`,
    });
  }

  /** 保存某个老师的菜单可见性（传隐藏列表，空数组 = 全部可见）。 */
  setUserMenus(userId: string, hiddenMenus: string[]): Observable<void> {
    return this.rest.request<any, void>({
      method: 'POST',
      url: '/api/app/menu-visibility/set-user-menus',
      body: { userId, hiddenMenus: hiddenMenus || [] },
    });
  }

  /** 清空某个老师的菜单限制（恢复全部可见）。 */
  resetUserMenus(userId: string): Observable<void> {
    return this.rest.request<any, void>({
      method: 'POST',
      url: `/api/app/menu-visibility/reset-user-menus/${userId}`,
    });
  }

  // ── 应用到侧边栏 ─────────────────────────────────────────

  /**
   * 启动时拉自己的配置并应用。未登录（登录页 / 门户）直接跳过。
   * 由 `MENU_VISIBILITY_PROVIDER` 在 app initializer 里调用，早于首屏渲染，避免菜单闪一下再消失。
   */
  /**
   * 启动时拉自己的配置并应用。
   *
   * <p>不阻塞启动、也不依赖初始化器顺序：等 `currentUser` 真正就绪（带 userId）后再拉配置。
   * 未登录（登录页 / 公开门户）永远等不到带 userId 的 currentUser，因此不会发请求。</p>
   *
   * <p>代价是被隐藏的菜单可能在首帧闪一下再消失（侧边栏订阅 `visible$`，我们
   * `refresh()` 后它会立刻重排），换来的好处是启动链路不会因为一个菜单接口变慢/失败而卡住。</p>
   */
  initialize(): void {
    this.configState
      .getDeep$('currentUser')
      .pipe(
        filter((user: any) => !!user?.id),
        take(1),
      )
      .subscribe(() => this.loadAndApply());
  }

  private loadAndApply(): void {
    this.getMyHiddenMenus().subscribe(names => this.applyHiddenMenus(names));
  }

  /** 把隐藏列表应用到侧边栏（幂等，可重复调用）。 */
  applyHiddenMenus(names: string[]): void {
    this.hiddenNames = new Set((names || []).filter(Boolean));

    if (!this.hidePatched) {
      // RoutesService.hide 是实例上的判定函数（invisible || 无权限 → 隐藏），这里叠加"被配置隐藏"。
      const originalHide = this.routes.hide.bind(this.routes);
      (this.routes as any).hide = (item: any) => this.shouldHide(item) || originalHide(item);
      this.hidePatched = true;
    }

    // 触发重新计算 visible$（LeptonX 的 AbpNavbarService 订阅它，会立即重排侧边栏）。
    this.routes.refresh();
  }

  /** 清空隐藏（例如退出登录时）。 */
  clear(): void {
    this.applyHiddenMenus([]);
  }

  /**
   * 是否隐藏某个路由节点：被直接隐藏，或祖先（一级菜单）被隐藏。
   * ABP 的 visible$ 只逐项过滤，父节点被隐藏时子节点会"浮"成一级菜单，所以必须连子孙一起隐藏。
   */
  private shouldHide(item: any): boolean {
    if (!this.hiddenNames.size || !item?.name) {
      return false;
    }
    if (this.hiddenNames.has(item.name)) {
      return true;
    }

    const flat = this.routes.flat || [];
    let parentName: string | undefined = item.parentName;
    let guard = 0;
    while (parentName && guard++ < 32) {
      if (this.hiddenNames.has(parentName)) {
        return true;
      }
      parentName = flat.find(x => x.name === parentName)?.parentName;
    }
    return false;
  }

  // ── 给我（租户管理员）用的菜单目录 ───────────────────────

  /**
   * 侧边栏上「可配置的第一级菜单」列表。
   *
   * <p>从路由树实时推导，不写死菜单清单 —— 新增菜单会自动出现在配置弹窗里。
   * 口径与侧边栏一致：有权限、非 `invisible`、有自身路径或有子项。</p>
   */
  getTopLevelMenus(): TopLevelMenuItem[] {
    const flat = (this.routes.flat || []) as any[];
    if (!flat.length) {
      return [];
    }

    const hasVisibleChild = (name: string) =>
      flat.some(x => x.parentName === name && !x.invisible);

    const result: TopLevelMenuItem[] = [];
    for (const item of flat) {
      if (!item?.name || item.parentName || item.invisible) {
        continue;
      }
      const childCount = flat.filter(x => x.parentName === item.name && !x.invisible).length;
      const hasOwnPath = Boolean(item.path);
      if (!hasOwnPath && !childCount) {
        continue;
      }
      // 与侧边栏同口径：没权限的菜单本来就不显示，不给配置；有权限但组内全不可见的也跳过。
      if (!this.permission.getGrantedPolicy(item.requiredPolicy)) {
        continue;
      }
      if (!hasOwnPath && !hasVisibleChild(item.name)) {
        continue;
      }

      result.push({
        name: item.name,
        displayName: this.localization.instant(item.name),
        iconClass: item.iconClass,
        path: item.path,
        childCount,
      });
    }

    return result;
  }
}
