import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfigStateService } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { SchoolService } from '../../proxy/schools/school.service';
import type { PublicSchoolDto, SchoolDto, UpdateSchoolDto } from '../../proxy/schools/dtos/models';
import { PortalService } from '../../proxy/portal/portal.service';
import type { TenantResourceSummaryDto } from '../../proxy/portal/models';

/**
 * 学校设置（管理端）。
 *
 * 需求「租户对应的学校是独立实体，需要有学校简介、学校徽标等配置项」：
 * - 学校档案区：维护本租户（学校）的名称、徽标、横幅、简介与联系方式；
 * - 平台设置区（仅 host 全局管理员可见）：指定平台唯一「社会学校」租户，
 *   决定公开注册用户的归属与「共享到社会学校」的目标。
 */
@Component({
  selector: 'app-school-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzCheckboxModule,
    NzDividerModule,
    NzFormModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzTooltipModule,
  ],
  templateUrl: './school-management.component.html',
  styleUrls: ['./school-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SchoolManagementComponent implements OnInit {
  private readonly schoolService = inject(SchoolService);
  private readonly portalService = inject(PortalService);
  private readonly message = inject(NzMessageService);
  private readonly configState = inject(ConfigStateService);

  readonly loading = signal<boolean>(true);
  readonly saving = signal<boolean>(false);
  readonly school = signal<SchoolDto | null>(null);
  readonly publicSchool = signal<PublicSchoolDto | null>(null);
  readonly tenants = signal<TenantResourceSummaryDto[]>([]);

  /** 是否 host 全局管理员（host 上下文没有 tenantId） */
  readonly isHost = signal<boolean>(false);

  form: UpdateSchoolDto = {
    name: '',
    logoUrl: '',
    bannerUrl: '',
    introduction: '',
    website: '',
    address: '',
    contactPhone: '',
    contactEmail: '',
    isPublic: true,
    sortOrder: 0,
    domain: '',
    slug: '',
  };

  ngOnInit(): void {
    const currentUser = this.configState.getOne('currentUser') as { tenantId?: string } | undefined;
    this.isHost.set(!currentUser?.tenantId);

    this.schoolService.getCurrent().subscribe({
      next: (res) => {
        this.school.set(res);
        this.form = {
          name: res.name || '',
          logoUrl: res.logoUrl || '',
          bannerUrl: res.bannerUrl || '',
          introduction: res.introduction || '',
          website: res.website || '',
          address: res.address || '',
          contactPhone: res.contactPhone || '',
          contactEmail: res.contactEmail || '',
          isPublic: res.isPublic,
          sortOrder: res.sortOrder,
          domain: res.domain || '',
          slug: res.slug || '',
        };
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载学校档案失败');
      },
    });

    this.schoolService.getPublicSchool().subscribe({
      next: (res) => this.publicSchool.set(res),
      error: () => this.publicSchool.set(null),
    });

    this.portalService.getPublicTenantList().subscribe({
      next: (list) => this.tenants.set(list || []),
      error: () => this.tenants.set([]),
    });
  }

  save(): void {
    this.saving.set(true);
    this.schoolService.updateCurrent(this.form).subscribe({
      next: (res) => {
        this.saving.set(false);
        this.school.set(res);
        this.message.success('学校档案已保存');
      },
      error: (err) => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '保存失败');
      },
    });
  }

  /** 保存平台唯一「社会学校」租户 */
  savePublicSchool(tenantId: string | null): void {
    if (!tenantId) {
      this.message.warning('请选择社会学校');
      return;
    }

    this.schoolService.setPublicSchool(tenantId).subscribe({
      next: (res) => {
        this.publicSchool.set(res);
        this.message.success(`已将「${res.name}」设置为社会学校`);
      },
      error: (err) => {
        this.message.error(err?.error?.error?.message || '设置失败');
      },
    });
  }
}
