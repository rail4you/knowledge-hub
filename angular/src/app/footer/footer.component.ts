import { Component } from '@angular/core';
import { SiteFooterComponent } from '../shared/branding/site-footer.component';

/** 管理端 LeptonX 页脚替换：三端统一单行文本，无链接 */
@Component({
  selector: 'abp-footer',
  standalone: true,
  imports: [SiteFooterComponent],
  template: `<app-site-footer></app-site-footer>`,
})
export class FooterComponent {}
