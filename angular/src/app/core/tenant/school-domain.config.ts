import { provideAppInitializer, inject } from '@angular/core';
import { SchoolDomainContextService } from './school-domain-context.service';

/**
 * 启动期探测当前域名归属的学校。
 *
 * <p>这是「域名绑定多租户」的前端入口：后端已按域名把请求落入对应租户，
 * 这里只负责让前端知道「我现在在哪所学校的地盘上」，以便锁定机构下拉、
 * 固定会话租户、裁剪跨校内容区块。</p>
 */
export const SCHOOL_DOMAIN_PROVIDER = [
  provideAppInitializer(() => {
    const ctx = inject(SchoolDomainContextService);
    return ctx.initialize();
  }),
];
