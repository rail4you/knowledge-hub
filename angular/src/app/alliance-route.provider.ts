import { inject, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@abp/ng.core';
import { ProductFeatureService } from './shared/product-feature.service';
import { firstValueFrom } from 'rxjs';

/**
 * 联盟协同菜单：仅当产品能力开关 `isAllianceEnabled` 打开时注册。
 *
 * 开关来自 `/api/app/product-feature`（ABP Feature，宿主管理员在「功能管理」中配置），
 * 不再依赖已移除的「版本 / 许可证」体系。
 */
export const ALLIANCE_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureAllianceRoute();
  }),
];

function configureAllianceRoute() {
  const routes = inject(RoutesService);
  const productFeatures = inject(ProductFeatureService);

  firstValueFrom(productFeatures.get())
    .then((features) => {
      if (!features?.isAllianceEnabled) {
        return;
      }

      routes.add([
        {
          path: '/admin/alliance',
          name: '::Menu:Alliance',
          parentName: 'AbpUiNavigation::Menu:Administration',
          layout: eLayoutType.application,
          requiredPolicy: 'KnowledgeHub.Alliance',
        },
      ]);
    })
    .catch(() => {
      // 开关读取失败 → 视为关闭，不阻塞启动
    });
}
