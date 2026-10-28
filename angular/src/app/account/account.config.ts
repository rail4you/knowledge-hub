import { provideAppInitializer, inject } from '@angular/core';
import { ReplaceableComponentsService, RoutesService, eLayoutType } from '@abp/ng.core';
import { eAccountComponents } from '@abp/ng.account';
import { eAccountRouteNames } from '@abp/ng.account/config';
import { AccountManageComponent } from './account-manage.component';

/**
 * 「我的账户」页定制：替换 ABP 自带页面 + 切到管理端布局。
 *
 * <para>
 * 1) 内容：把 `eAccountComponents.ManageProfile` 换成项目自有的
 *    「个人信息 / 联系方式 / 实名认证 / 账号安全」页面；
 *    ABP 默认页只有「个人设置 + 修改密码」两个标签，样式是主题通用表单，
 *    与本项目管理端风格不一致。
 * </para>
 *
 * <para>
 * 2) 布局：ABP 在 RoutesService 里给 `/account/*` 节点标了 `layout: "account"`，
 *    而本项目从未提供 account 布局（只有 application / empty），
 *    DynamicLayoutComponent 解析不到时会退化成无侧边栏的裸页面 ——
 *    这就是「我的账户」看起来和后台其他页面不像的原因。
 *    这里把 Manage 节点改成 `application`，与 /admin/* 等管理页同外壳（侧边栏 + 顶栏）。
 * </para>
 *
 * <para>
 * 初始化器顺序：必须晚于 `provideAccountConfig()`（它负责 configureRoutes），
 * 才能覆盖 ABP 注册的默认组件与布局，见 app.config.ts 中本 Provider 的位置。
 * </para>
 */
function initAccountManage() {
  const replaceableComponents = inject(ReplaceableComponentsService);
  replaceableComponents.add({
    key: eAccountComponents.ManageProfile,
    component: AccountManageComponent,
  });

  const routes = inject(RoutesService);
  routes.patch(eAccountRouteNames.ManageProfile, { layout: eLayoutType.application });
}

export const ACCOUNT_COMPONENTS_PROVIDER = [
  provideAppInitializer(() => {
    initAccountManage();
  }),
];
