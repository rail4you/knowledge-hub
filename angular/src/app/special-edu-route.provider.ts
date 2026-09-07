import { inject, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@abp/ng.core';
import { EditionService } from './install/edition.service';
import { firstValueFrom } from 'rxjs';

/**
 * 特教扩展模块菜单（应用布局左侧导航）：
 * 1. 管理入口 `/admin/special-education` 无条件注册，挂在系统管理（Administration）下，
 *    requiredPolicy 仅全局管理员持有 → 只有全局管理员能看到菜单项并进入页面。
 *    （不能跟租户 Feature 开关绑定，否则宿主上下文永远看不到。）
 * 2. 教师端独立分组 仅当 Edition.isSpecialEducationEnabled 为 true 时注册，
 *    关闭时整个分组消失（路由 Guard 仍会拦截直访）。
 */
export const SPECIAL_EDU_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureSpecialEduRoutes();
  }),
];

function configureSpecialEduRoutes() {
  const routes = inject(RoutesService);
  const editionService = inject(EditionService);

  // 管理入口：始终注册，可见性完全由 Manage 权限（仅 host admin）控制。
  routes.add([
    {
      path: '/admin/special-education',
      name: '::Menu:SpecialEduAdmin',
      iconClass: 'fas fa-toggle-on',
      parentName: 'AbpUiNavigation::Menu:Administration',
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.SpecialEducation.Manage',
    },
  ]);

  // 教师端分组：插件式开关。
  firstValueFrom(editionService.getEdition()).then(edition => {
    if (!edition?.isSpecialEducationEnabled) {
      return;
    }
    routes.add([
      {
        path: '/special-edu',
        name: '::Menu:SpecialEducation',
        iconClass: 'fas fa-heart',
        order: 8,
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.SpecialEducation',
      },
      {
        path: '/special-edu/teaching-design',
        name: '::Menu:TeachingDesign',
        iconClass: 'fas fa-file-alt',
        parentName: '::Menu:SpecialEducation',
        order: 1,
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.SpecialEducation.TeachingDesign',
      },
      {
        path: '/special-edu/iep',
        name: '::Menu:IEP',
        iconClass: 'fas fa-user-check',
        parentName: '::Menu:SpecialEducation',
        order: 2,
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.SpecialEducation.IEP',
      },
      {
        path: '/special-edu/resources',
        name: '::Menu:SpecialResources',
        iconClass: 'fas fa-photo-video',
        parentName: '::Menu:SpecialEducation',
        order: 3,
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.SpecialEducation.Resource',
      },
      {
        path: '/special-edu/braille',
        name: '::Menu:BrailleStudy',
        iconClass: 'fas fa-braille',
        parentName: '::Menu:SpecialEducation',
        order: 4,
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.SpecialEducation.Resource',
      },
    ]);
  });
}
