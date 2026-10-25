import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';

import { TenantListService } from '../../proxy/controllers/tenant-list.service';
import type { TenantInfoDto } from '../../proxy/controllers/models';
import { HomeTopNavComponent } from '../shared/home-top-nav.component';

/**
 * 院校门户目录（公开，游客可访问）。
 *
 * <para>对齐原型 <c>#campuses</c>（campusDirectory）：频道头图 → 学校搜索 → 院校卡片网格。
 * 每张卡片进入该校的独立门户 <c>/school/:id</c>。</para>
 *
 * <para>数据来源：<c>GET /api/public/tenants</c>。列表里有两个非院校项要过滤：
 * 「全局」伪租户（id 为空）与平台唯一「社会学校」（开放注册归属机构，不是院校）。</para>
 */
@Component({
  selector: 'app-campus-directory',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NzIconModule, NzSpinModule, HomeTopNavComponent],
  templateUrl: './campus-directory.component.html',
  styleUrls: ['./campus-directory.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampusDirectoryComponent implements OnInit {
  private readonly tenantListService = inject(TenantListService);

  readonly loading = signal(true);

  /** 全部院校（已过滤掉「全局」与「社会学校」） */
  readonly campuses = signal<TenantInfoDto[]>([]);

  /** 学校名称 / 关键词 */
  readonly keyword = signal('');

  /** 过滤后的院校 */
  readonly filteredCampuses = computed<TenantInfoDto[]>(() => {
    const q = (this.keyword() || '').trim().toLowerCase();
    const all = this.campuses() || [];
    if (!q) return all;
    return all.filter(c => {
      const hay = `${c.schoolName || ''}${c.name || ''}${c.tenantName || ''}`;
      return hay.toLowerCase().includes(q);
    });
  });

  ngOnInit(): void {
    this.tenantListService.getTenants().subscribe({
      next: list => {
        this.campuses.set(
          (list || []).filter(t => !!t.id && t.isPublicSchool !== true),
        );
        this.loading.set(false);
      },
      error: () => {
        this.campuses.set([]);
        this.loading.set(false);
      },
    });
  }

  /** 校徽占位：取校名前两个字（原型 campus-mark 的 short 语义） */
  markText(c: TenantInfoDto): string {
    const name = (c.schoolName || c.name || '').trim();
    return name ? name.slice(0, 2) : '院校';
  }

  /** 院校展示名：学校档案名优先 */
  displayName(c: TenantInfoDto): string {
    return c.schoolName || c.name || c.tenantName || '未命名院校';
  }
}
