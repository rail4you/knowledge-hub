import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { HomeTopNavComponent } from '../shared/home-top-nav.component';
import { StudentMicroMajorDetailComponent } from '../../student/micro-majors/student-micro-major-detail.component';
import { BrandingService } from '../../shared/branding/branding.service';

/**
 * 微专业详情页（门户 layout）。
 *
 * <para>
 * 需求「微专业详情页面应该在首页的 layout，不用显示学生端的左侧菜单和首部」：
 * 详情页从学生端壳层（左侧菜单 + 学生页头）挪到门户壳层 ——
 * 顶部是首页通用的 `app-home-top-nav`，底部是门户页脚，与学生端样式解耦。
 * </para>
 *
 * <para>
 * 页面主体复用学生端详情组件（真实数据：培养方案 / 课程 / 报名 / 考核 / 配套资料），
 * 只替换外层壳层，避免复制一份业务逻辑。
 * </para>
 */
@Component({
  selector: 'app-micro-major-detail-page',
  standalone: true,
  imports: [CommonModule, RouterModule, HomeTopNavComponent, StudentMicroMajorDetailComponent],
  template: `
    <div class="mmd-page">
      <app-home-top-nav section="micro"></app-home-top-nav>

      <main class="mmd-main">
        <app-student-micro-major-detail></app-student-micro-major-detail>
      </main>

      <footer class="site-footer">
        {{ footerBrand }} · 让知识流动，让成长发生<br />
        全专业教学资源平台 · 课程、资源、微专业与实训统一归集
      </footer>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
        background: #f8f9f6;
      }

      .mmd-main {
        max-width: 1376px;
        margin: 0 auto;
        padding: 30px 28px 70px;
        box-sizing: border-box;
      }

      .site-footer {
        border-top: 1px solid var(--kh-line, #dfe7e1);
        background: #edf2ef;
        padding: 28px;
        text-align: center;
        font-size: 12px;
        line-height: 1.9;
        color: #687b73;
      }

      @media (max-width: 720px) {
        .mmd-main {
          padding: 20px 18px 50px;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MicroMajorDetailPageComponent {
  private readonly branding = inject(BrandingService);

  /** 页脚品牌名（与 explore 页一致，走品牌配置）。 */
  get footerBrand(): string {
    return (this.branding.appTitle() || '易课通').trim();
  }
}
