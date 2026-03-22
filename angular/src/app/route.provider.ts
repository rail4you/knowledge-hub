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
        requiredPolicy: 'KnowledgeHub.Resources'
      },
      {
        path: '/resources',
        name: '::Menu:Resources',
        parentName: '::Menu:KnowledgeHub',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Resources',
      },
      {
        path: '/search',
        name: '::Menu:Search',
        iconClass: 'fas fa-search',
        order: 3,
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Search',
      },
      {
        path: '/admin/indexing-jobs',
        name: '::Menu:IndexingJobs',
        iconClass: 'fas fa-tasks',
        parentName: '::Menu:KnowledgeHub',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Resources',
      }
  ]);
}
