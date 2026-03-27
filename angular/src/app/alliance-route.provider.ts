import { inject, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@abp/ng.core';
import { EditionService } from './install/edition.service';
import { firstValueFrom } from 'rxjs';

export const ALLIANCE_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureAllianceRoute();
  }),
];

function configureAllianceRoute() {
  const routes = inject(RoutesService);
  const editionService = inject(EditionService);

  const edition = editionService.getEdition();
  
  firstValueFrom(edition).then((editionData) => {
    if (editionData?.isAllianceEnabled) {
      routes.add([
        {
          path: '/admin/alliance',
          name: '::Menu:Alliance',
          parentName: 'AbpUiNavigation::Menu:Administration',
          layout: eLayoutType.application,
          requiredPolicy: 'KnowledgeHub.Alliance',
        },
      ]);
    }
  });
}
