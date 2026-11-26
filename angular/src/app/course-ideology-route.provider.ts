import { inject, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@abp/ng.core';
import { firstValueFrom } from 'rxjs';
import { ProductFeatureService } from './shared/product-feature.service';

/**
 * 课程思政模块的菜单（插件式 · **独立顶级分组**）。
 *
 * <p>为什么独立成组而不是挂在「课程内容」下：思政是"价值塑造"的一条独立工作线，
 * 混在章节/习题/资源/教案里，老师会把思政案例当成普通课程素材，找不到重点。</p>
 *
 * <p>只有产品能力开关 `isCourseIdeologyEnabled` 为 true 时才注册；
 * 关闭后整组消失，路由守卫 `ideologyEnabledGuard` 也会拦住直连 URL ——
 * 这是"模块可以整体拿掉"的运行时保证。</p>
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
          // 顶级分组「课程思政」：排在「课堂互动」(9) 之后、「智学分析」(11) 之前（order 10）。
          // 注意：顶级 order 必须是整数（小数会让 ABP 的比较器在和无 order 的 invisible 节点比较时退化）。
          path: '/ideology',
          name: '::Menu:IdeologyGroup',
          iconClass: 'fas fa-flag',
          order: 10,
          layout: eLayoutType.application,
          requiredPolicy: 'KnowledgeHub.CourseIdeology',
        },
        {
          path: '/ideology/cases',
          name: '::Menu:IdeologyCases',
          iconClass: 'fas fa-book-open',
          parentName: '::Menu:IdeologyGroup',
          order: 1,
          layout: eLayoutType.application,
          requiredPolicy: 'KnowledgeHub.CourseIdeology',
        },
        {
          path: '/ideology/graph',
          name: '::Menu:IdeologyGraph',
          iconClass: 'fas fa-share-alt',
          parentName: '::Menu:IdeologyGroup',
          order: 2,
          layout: eLayoutType.application,
          requiredPolicy: 'KnowledgeHub.CourseIdeology',
        },
        {
          path: '/ideology/elements',
          name: '::Menu:IdeologyElements',
          iconClass: 'fas fa-tags',
          parentName: '::Menu:IdeologyGroup',
          order: 3,
          layout: eLayoutType.application,
          requiredPolicy: 'KnowledgeHub.CourseIdeology.ManageElement',
        },
        {
          // 覆盖分析与报告生成合并在同一页（统计数据 + xlsx 报告）。
          path: '/ideology/coverage',
          name: '::Menu:IdeologyCoverage',
          iconClass: 'fas fa-chart-pie',
          parentName: '::Menu:IdeologyGroup',
          order: 4,
          layout: eLayoutType.application,
          requiredPolicy: 'KnowledgeHub.CourseIdeology.ViewStatistics',
        },
      ]);
    })
    .catch(() => {
      // 开关读取失败 = 按"未启用"处理，绝不影响主流程。
    });
}
