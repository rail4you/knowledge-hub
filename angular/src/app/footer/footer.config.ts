import { provideAppInitializer, inject } from '@angular/core';
import { ReplaceableComponentsService } from '@abp/ng.core';
import { eThemeLeptonXComponents } from '@abp/ng.theme.lepton-x';
import { FooterComponent } from './footer.component';
import { SiteBrandComponent } from '../shared/branding/site-brand.component';

function initFooter() {
  const replaceableComponents = inject(ReplaceableComponentsService);
  replaceableComponents.add({
    key: eThemeLeptonXComponents.Footer,
    component: FooterComponent,
  });
  // 管理端侧边栏 Logo 也走全局品牌（图片 URL，空则文字 Logo + 标题）
  replaceableComponents.add({
    key: eThemeLeptonXComponents.Logo,
    component: SiteBrandComponent,
  });
}

export const FOOTER_PROVIDER = [
  provideAppInitializer(() => {
    initFooter();
  }),
];
