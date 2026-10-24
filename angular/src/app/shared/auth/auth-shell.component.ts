import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { SiteBrandComponent } from '../branding/site-brand.component';
import { SiteFooterComponent } from '../branding/site-footer.component';

/**
 * 认证页外壳（登录 / 注册共用）。
 *
 * <para>需求「社会学校的注册界面和登录界面都是一套 layout，表单放在右侧」：
 * 把整站导航栏 + 左侧品牌介绍 + 右侧表单卡片 + 页脚抽成这一个组件，
 * 登录页与注册页只负责各自的中段表单内容，视觉与交互完全一致。</para>
 *
 * <para>左侧品牌区内容固定（平台定位与能力介绍），右侧卡片标题/副标题由调用方传入，
 * 卡片内表单通过内容投影（ng-content）注入。</para>
 */
@Component({
  selector: 'app-auth-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, LocalizationPipe, SiteBrandComponent, SiteFooterComponent],
  templateUrl: './auth-shell.component.html',
  styleUrls: ['./auth-shell.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthShellComponent {
  /** 右侧表单卡片主标题（如「登录」/「社会学校注册」） */
  readonly title = input.required<string>();
  /** 标题下方说明文字 */
  readonly subtitle = input<string>('');
  /** 左侧品牌区主标题 */
  readonly heroTitle = input<string>('教学资源库<br />为机构而生的知识底座');
  /** 左侧品牌区说明 */
  readonly heroDesc = input<string>(
    '集成 AI 能力的新一代知识管理系统，帮助教育机构沉淀资源、加速教学与研发协同。'
  );
}
