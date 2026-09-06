import { inject, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@abp/ng.core';
import { EditionService } from './install/edition.service';
import { firstValueFrom } from 'rxjs';

/**
 * 特教扩展模块菜单：独立分组，插件式开关。
 * 仅当 Edition.isSpecialEducationEnabled 为 true 时注册，
 * 关闭时整个分组消失（路由 Guard 仍会拦截直访）。
 */
export const SPECIAL_EDU_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureSpecialEduRoutes();
  }),
];

function configureSpecialEduRoutes() {
  const routes = inject(RoutesService);
  const editionService = inject(EditionService);

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
        path: '/admin/special-education',
        name: '::Menu:SpecialEduAdmin',
        iconClass: 'fas fa-toggle-on',
        parentName: '::Menu:SpecialEducation',
        order: 4,
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.SpecialEducation.Manage',
      },
    ]);
  });
}
