import { ChangeDetectionStrategy, Component, input } from '@angular/core';
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
  /** 来源标记（用于返回定位）：home = 首页，explore = 探索页 */
  readonly from = input<string>('home');
}
