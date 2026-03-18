import { RoutesService, eLayoutType } from '@abp/ng.core';
import { inject, provideAppInitializer } from '@angular/core';

export const APP_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routes = inject(RoutesService);
  routes.add([
      {
        path: '/',
        name: '::Menu:Home',
        iconClass: 'fas fa-home',
        order: 1,
        layout: eLayoutType.application,
      },
      {
        path: '/knowledge-hub',
        name: '::Menu:KnowledgeHub',
        iconClass: 'fas fa-database',
        order: 2,
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Documents || KnowledgeHub.Users || KnowledgeHub.Resources'
      },
      {
        path: '/resources',
        name: '::Menu:Resources',
        parentName: '::Menu:KnowledgeHub',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Resources',
      },
      {
        path: '/documents',
        name: '::Menu:Documents',
        parentName: '::Menu:KnowledgeHub',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Documents',
      },
      {
        path: '/users',
        name: '::Menu:Users',
        parentName: '::Menu:KnowledgeHub',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Users',
      }
  ]);
}
