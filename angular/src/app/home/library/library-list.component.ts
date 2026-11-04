import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';

import { PortalService } from '../../proxy/portal/portal.service';
import type { PublicResourceLibraryDto } from '../../proxy/portal/models';
import { HomeTopNavComponent } from '../shared/home-top-nav.component';

/**
 * 共建共享资源库中心（公开）。
 *
 * <para>对齐原型改版后的 <c>#shared-center</c>（redesignedShared）结构：
 * 频道头图 → 快捷入口条 → 筛选面板 → 结果计数 → 资源库卡片网格。</para>
 *
 * <para>数据来源：<c>GET /api/app/portal/public-library-list</c>，
 * 后端只返回<b>跨校共建</b>资源库（院校共建 + 至少一个参建学校 + 门户公开 + 已发布），
 * 校级私有 / 校级共享库不会出现在这里。含主建学校（主建设单位）、
 * 参与建设学校与建设负责人。</para>
 */
@Component({
  selector: 'app-library-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NzIconModule, NzSpinModule, HomeTopNavComponent],
  templateUrl: './library-list.component.html',
  styleUrls: ['./library-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LibraryListComponent implements OnInit {
  private readonly portal = inject(PortalService);

  readonly loading = signal(true);

  /** 全部可见资源库（未过滤） */
  readonly libraries = signal<PublicResourceLibraryDto[]>([]);

  /** ── 筛选条件（原型 hubState['shared-center']） ── */
  readonly keyword = signal('');
  readonly major = signal('全部专业');
  readonly scope = signal('多校共建');

  /**
   * 筛选范围：本页只展示<b>跨校共建</b>资源库（后端已按此口径过滤），
   * 所以只保留「多校共建」一档；不再给「全部范围」，避免暗示本页含非共建库。
   */
  readonly scopeOptions = ['多校共建'];

  /** 专业方向可选项（按主建专业名去重） */
  readonly majorOptions = computed<string[]>(() => {
    const set = new Set<string>();
    for (const l of this.libraries()) {
      if (l.primaryMajorName) set.add(l.primaryMajorName);
    }
    return ['全部专业', ...Array.from(set)];
  });

  /** 过滤后的资源库 */
  readonly filteredLibraries = computed<PublicResourceLibraryDto[]>(() => {
    const q = (this.keyword() || '').trim().toLowerCase();
    const major = this.major();
    const scope = this.scope();
    return (this.libraries() || []).filter(l => {
      if (major !== '全部专业' && l.primaryMajorName !== major) return false;
      // 后端只会给跨校共建库，这里不再按范围剔除（保留 scope 档位仅为与原型的筛选面板一致）。
      if (scope !== '多校共建') return false;
      if (!q) return true;
      const hay = `${l.name || ''}${l.ownerTenantName || ''}${l.principal || ''}${(l.partnerNames || []).join('')}`;
      return hay.toLowerCase().includes(q);
    });
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
      },
    });
  }

  /** 执行搜索（原型是「选择条件后点击搜索」，这里保持同样的交互节奏） */
  onSearch(): void {
    this.filteredLibraries(); // 触发一次计算，语义上等价于「提交筛选」
  }

  /** 重置筛选条件 */
  onReset(): void {
    this.keyword.set('');
    this.major.set('全部专业');
    this.scope.set('多校共建');
  }

  /** 卡片封面配色（按索引稳定取色，对齐原型 green/blue/orange/purple） */
  coverTone(index: number): string {
    return ['green', 'blue', 'orange', 'purple'][index % 4];
  }

  /** 封面主标签：主建专业简称（原型取 label，取专业名前两字） */
  coverLabel(l: PublicResourceLibraryDto): string {
    const name = (l.primaryMajorName || '').trim();
    return name ? name.slice(0, 4) : '专业资源库';
  }
}
