import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { AuthService } from '@abp/ng.core';

import { PortalService } from '../../proxy/portal/portal.service';
import type { PublicResourceLibraryDto } from '../../proxy/portal/models';
import { HomeTopNavComponent } from '../shared/home-top-nav.component';

/**
 * 租户资源库列表页（公开）。
 *
 * <para>对齐原型 <c>shared-center</c>：页头说明 + 专业方向筛选 + 资源库卡片网格。</para>
 *
 * <para>数据来源：<c>GET /api/app/portal/public-library-list</c>，
 * 返回全部「已发布 + 公开展示」的多校共建资源库，含主建学校、参建学校与建设负责人。</para>
 */
@Component({
  selector: 'app-library-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NzIconModule, NzSpinModule, NzEmptyModule, HomeTopNavComponent],
  templateUrl: './library-list.component.html',
  styleUrls: ['./library-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LibraryListComponent implements OnInit {
  private readonly portal = inject(PortalService);
  private readonly authService = inject(AuthService);

  /** 是否已登录：登录后才展示「管理共建资源库」入口（管理端权限由路由守卫再校验） */
  get isLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  readonly loading = signal(true);
  readonly libraries = signal<PublicResourceLibraryDto[]>([]);
  readonly error = signal('');

  /** 专业方向筛选（按主建专业名去重） */
  readonly selectedMajor = signal('全部专业');

  /** 可选专业方向列表 */
  readonly majorOptions = computed<string[]>(() => {
    const set = new Set<string>();
    for (const l of this.libraries()) {
      if (l.primaryMajorName) set.add(l.primaryMajorName);
    }
    return ['全部专业', ...Array.from(set)];
  });

  /** 按专业方向过滤后的资源库 */
  readonly filteredLibraries = computed<PublicResourceLibraryDto[]>(() => {
    const major = this.selectedMajor();
    const all = this.libraries() || [];
    if (major === '全部专业') return all;
    return all.filter(l => l.primaryMajorName === major);
  });

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.portal.getPublicLibraryList().subscribe({
      next: list => {
        this.libraries.set(list || []);
        this.loading.set(false);
      },
      error: () => {
        this.libraries.set([]);
        this.loading.set(false);
        this.error.set('资源库列表加载失败，请稍后重试。');
      },
    });
  }

  onMajorChange(major: string): void {
    this.selectedMajor.set(major);
  }

  /** 卡片封面主色（按索引稳定取色，对齐原型 green/blue/orange/purple） */
  coverTone(index: number): string {
    return ['green', 'blue', 'orange', 'purple'][index % 4];
  }

  /** 封面上的短标签：取主建专业名前 4 字（无专业时回退「专业资源库」） */
  coverLabel(l: PublicResourceLibraryDto): string {
    const name = (l.primaryMajorName || '').trim();
    return name ? name.slice(0, 4) : '专业资源库';
  }

  /** 参与建设的学校摘要文本 */
  partnerText(l: PublicResourceLibraryDto): string {
    const partners = l.partnerNames || [];
    if (!partners.length) return '暂无参与建设学校';
    return partners.join('、');
  }
}
