import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AuthService } from '@abp/ng.core';

/** 资源库卡片数据（首页与公开探索页共用） */
export interface LibraryCardVm {
  id: string;
  /** 资源库名称（卡片主标题） */
  name: string;
  /** 建设单位（学校名称），卡片上单独一行展示 */
  schoolName?: string | null;
  desc: string;
  tone: 'sage' | 'blue' | 'sand';
  icon: string;
  num: string;
  courseCount: number;
  resourceCount: number;
  microMajorCount: number;
  /** 学校徽标 URL（为空时回退到图标） */
  logoUrl?: string | null;
  /** 后台设置的资源库封面图（为空时用渐变底色占位） */
  coverUrl?: string | null;
  courses: { id: string; title: string }[];
}

/**
 * 资源库卡片（首页与公开探索页共用）。
 * 结构与样式直接沿用首页的 `.library-card`，保证两处完全一致。
 */
@Component({
  selector: 'app-library-card',
  standalone: true,
  imports: [CommonModule, RouterModule, NzIconModule],
  templateUrl: './library-card.component.html',
  styleUrls: ['./library-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LibraryCardComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  /** 卡片数据 */
  readonly card = input.required<LibraryCardVm>();
  /** 来源标记（用于返回定位）：home = 首页，explore = 公开资源库页 */
  readonly from = input<string>('home');

  /** 课程链接的查询参数：探索页进入时带上 fromPage，返回按钮才能回到资源库页 */
  readonly courseQueryParams = computed<Record<string, string>>(() =>
    this.from() === 'explore'
      ? { from: 'explore', fromPage: 'library', section: 'tenants' }
      : { from: 'home', section: 'tenants' }
  );

  /** 是否已登录：未登录时点课程不预览，直接去登录页（需求：资源库中的课程点进去直接显示登录） */
  get isLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  /**
   * 整卡可点击：鼠标放在卡片上即可直接进入资源库。
   * 卡片内部的链接（校名 / 课程）会 stopPropagation，不会触发这里。
   */
  enterLibrary(): void {
    this.router.navigate(['/tenant', this.card().id], {
      queryParams: { from: this.from(), section: 'tenants' },
    });
  }

  /**
   * 点击资源库中的课程。
   *
   * <para>需求「资源库中的课程点进去，不要预览了，直接显示登录」：
   * 游客点课程直接跳登录页；已登录用户保持原有预览行为。</para>
   */
  openCourse(courseId: string, event: Event): void {
    event.stopPropagation();
    if (!courseId) return;
    const target = `/course-preview/${courseId}`;
    if (!this.isLoggedIn) {
      this.router.navigate(['/account/login'], { queryParams: { returnUrl: target } });
      return;
    }
    this.router.navigate(['/course-preview', courseId], { queryParams: this.courseQueryParams() });
  }

  /** 点击学校名称：跳学校简介（不触发整卡点击） */
  openSchool(event: Event): void {
    event.stopPropagation();
  }
}
