import { provideAppInitializer, inject } from '@angular/core';
import { ReplaceableComponentsService, RoutesService, eLayoutType } from '@abp/ng.core';
import { eAccountComponents } from '@abp/ng.account';
import { eAccountRouteNames } from '@abp/ng.account/config';
import { LoginComponent } from './login.component';

function initLoginComponent() {
  const replaceableComponents = inject(ReplaceableComponentsService);
  replaceableComponents.add({
    key: eAccountComponents.Login,
    component: LoginComponent,
  });

  // 关键修复：ABP 的 DynamicLayoutComponent 解析布局时，RoutesService 里
  // /account/* 节点的 `layout: "account"` 会覆盖 Angular 路由 data 里的 empty，
  // 而本项目从未提供 provideAccountLayout()，导致 /account/login 直接白屏
  // （console 仅有一条 "Account layout not found" 警告，无报错）。
  // 自研登录页是全幅定制页（含自己的顶栏/页脚），本就该用空布局；
  // 其余 account 页（注册/忘记密码）同样改为空布局，至少可见可用。
  // 注意：本初始化器必须在 provideAccountConfig() 的 configureRoutes() 之后执行，
  // 顺序见 app.config.ts 里 LOGIN_PROVIDER 的位置。
  const routes = inject(RoutesService);
  [
    eAccountRouteNames.Login,
    eAccountRouteNames.Register,
    eAccountRouteNames.ForgotPassword,
    eAccountRouteNames.ResetPassword,
  ].forEach(name => routes.patch(name, { layout: eLayoutType.empty }));
}

export const LOGIN_PROVIDER = [
  provideAppInitializer(() => {
    initLoginComponent();
  }),
];
