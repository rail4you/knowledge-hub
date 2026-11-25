import { inject, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@abp/ng.core';
import { firstValueFrom } from 'rxjs';
import { ProductFeatureService } from './shared/product-feature.service';

/**
 * 课程思政模块的菜单（插件式）。
 *
 * <p>只有产品能力开关 `isCourseIdeologyEnabled` 为 true 时才注册：
 * 关闭后侧边栏三项整体消失，路由守卫仍会拦截直访 —— 这就是"模块可以整体拿掉"的运行时保证。</p>
 *
 * <p>菜单挂在「课程内容」分组下（order 7/8/9），与章节 / 习题 / 资源 / 教案 / 案例 / 试卷并列，
 * 因为思政内容天然属于课程内容建设的一部分。</p>
 */
export const COURSE_IDEOLOGY_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureIdeologyRoutes();
  }),
];

function configureIdeologyRoutes() {
  const routes = inject(RoutesService);
  const productFeatures = inject(ProductFeatureService);

  firstValueFrom(productFeatures.get())
    .then(features => {
      if (!features?.isCourseIdeologyEnabled) {
        return;
      }

      routes.add([
        {
          path: '/learning/ideology-cases',
          name: '::Menu:IdeologyCases',
          iconClass: 'fas fa-flag',
          parentName: '::Menu:CourseContentGroup',
          order: 7,
          layout: eLayoutType.application,
          requiredPolicy: 'KnowledgeHub.CourseIdeology',
        },
        {
          path: '/learning/ideology-graph',
          name: '::Menu:IdeologyGraph',
          iconClass: 'fas fa-share-alt',
          parentName: '::Menu:CourseContentGroup',
          order: 8,
          layout: eLayoutType.application,
          requiredPolicy: 'KnowledgeHub.CourseIdeology',
        },
        {
          path: '/learning/ideology-coverage',
          name: '::Menu:IdeologyCoverage',
          iconClass: 'fas fa-chart-bar',
          parentName: '::Menu:CourseContentGroup',
          order: 9,
          layout: eLayoutType.application,
          requiredPolicy: 'KnowledgeHub.CourseIdeology.ViewStatistics',
        },
      ]);
    })
    .catch(() => {
      // 开关读取失败 = 按"未启用"处理，绝不影响主流程。
    });
}
