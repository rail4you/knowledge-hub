import { provideAppInitializer, inject } from '@angular/core';
import { MenuVisibilityService } from './menu-visibility.service';

/**
 * 启动期应用「按老师配置的一级菜单可见性」。
 *
 * <p>必须注册在路由提供者（`APP_ROUTE_PROVIDER` / 特殊教育分组等）之后：
 * 初始化器按 providers 顺序执行，先有菜单节点才能裁剪。</p>
 *
 * <p>拉取失败不阻断启动（服务内部已经 catch，返回空列表 = 全部可见）。</p>
 */
export const MENU_VISIBILITY_PROVIDER = [
  provideAppInitializer(() => {
    // initialize() 只注册订阅（等 currentUser 就绪后再拉配置），不阻塞启动。
    inject(MenuVisibilityService).initialize();
  }),
];
