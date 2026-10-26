import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { TeachingLibraryWorkbenchComponent } from './teaching-library-workbench.component';

/**
 * 教学资源库工作台 —— 独立页面（教师端侧边栏入口）。
 *
 * 与系统工作台下方嵌入的同名区块共用 `TeachingLibraryWorkbenchComponent`；
 * 本页只负责页面级页头（标题 + 返回工作台），避免出现两个同名标题。
 */
@Component({
  selector: 'app-teaching-library-workbench-page',
  standalone: true,
  imports: [CommonModule, RouterLink, NzIconModule, TeachingLibraryWorkbenchComponent],
  template: `
    <div class="kh-page tlw-page">
      <header class="kh-page-head">
        <div>
          <div class="kh-eyebrow">TEACHING RESOURCE LIBRARY</div>
          <h1>教学资源库工作台</h1>
          <p>你能看到的全部教学资源库：数据汇总、快捷访问，以及资源 / 课程 / 实训的入库建设操作。</p>
        </div>
        <div class="kh-page-head__aside">
          <a class="tlw-back" routerLink="/admin/workbench">
            <i nz-icon nzType="arrow-left"></i>
            返回工作台
          </a>
        </div>
      </header>

      <app-teaching-library-workbench [embedded]="true"></app-teaching-library-workbench>
    </div>
  `,
  styles: [
    `
      .tlw-page {
        gap: 24px;
      }

      .tlw-back {
        display: inline-flex;
        align-items: center;
        gap: 7px;
        height: 38px;
        padding: 0 16px;
        border: 1px solid var(--kh-line);
        border-radius: 8px;
        background: var(--kh-panel);
        color: var(--kh-ink-2);
        font-size: 13px;
        text-decoration: none;
        transition: border-color 0.15s, color 0.15s;

        &:hover {
          border-color: var(--kh-accent-light);
          color: var(--kh-accent-deep);
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeachingLibraryWorkbenchPageComponent {}
