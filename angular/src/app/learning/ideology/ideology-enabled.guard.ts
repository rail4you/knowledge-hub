import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs/operators';
import { ProductFeatureService } from '../../shared/product-feature.service';

/**
 * 课程思政模块的开关守卫：产品能力开关关闭时，直连 URL 也进不去。
 *
 * <p>为什么需要它：菜单可以按开关不注册，但路由仍在 `app.routes.ts` 里，
 * 老师把链接存了书签 / 别人发过来仍能直达。这里统一兜到课程列表页，
 * 保证"关掉模块 = 完全不可达"，这也是模块可整体移除运行时的那一半保证。</p>
 */
export const ideologyEnabledGuard: CanActivateFn = () => {
  const features = inject(ProductFeatureService);
  const router = inject(Router);

  return features
    .get()
    .pipe(map(feature => (feature?.isCourseIdeologyEnabled ? true : router.createUrlTree(['/learning/course-list']))));
};
