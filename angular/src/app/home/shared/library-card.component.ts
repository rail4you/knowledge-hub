import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';

/** 资源库卡片数据（首页与公开探索页共用） */
export interface LibraryCardVm {
  id: string;
  name: string;
  desc: string;
  tone: 'sage' | 'blue' | 'sand';
  icon: string;
  num: string;
  courseCount: number;
  resourceCount: number;
  microMajorCount: number;
  /** 学校徽标 URL（为空时回退到图标） */
  logoUrl?: string | null;
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
}
