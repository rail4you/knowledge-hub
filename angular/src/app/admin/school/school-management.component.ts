import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import type { WritableSignal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfigStateService } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { SchoolService } from '../../proxy/schools/school.service';
import type { PublicSchoolDto, SchoolDto, UpdateSchoolDto } from '../../proxy/schools/dtos/models';
import { PortalService } from '../../proxy/portal/portal.service';
import type { TenantResourceSummaryDto } from '../../proxy/portal/models';
import { OssUploadService } from '../../shared/oss-upload.service';

/**
 * 学校管理（资源库管理 → 学校管理）。
 *
 * <p>业务前提：<b>租户与学校 1:1</b> —— 每个租户就是一所学校，学校档案
 * （名称 / 徽标 / 简介 / 联系方式 / 域名绑定）配置完成后即 `/school/{tenantId}`
 * 院校门户页与首页院校卡片展示的内容。</p>
 *
 * <p>权限模型：</p>
 * <ul>
 *   <li>host 全局管理员：列表返回全部租户对应的学校，可逐校编辑（跨租户保存由后端校验）；</li>
 *   <li>租户管理员（SchoolAdmin）：列表只返回本校，只能编辑本校档案。</li>
 * </ul>
 *
 * <p>平台级设置（社会学校 / 域名绑定基础域名）只有 host 可见。</p>
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
    NzEmptyModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './school-management.component.html',
  styleUrls: ['./school-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SchoolManagementComponent implements OnInit {
  private readonly schoolService = inject(SchoolService);
  private readonly portalService = inject(PortalService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly message = inject(NzMessageService);
  private readonly configState = inject(ConfigStateService);

  readonly loading = signal<boolean>(true);
  readonly saving = signal<boolean>(false);
  readonly logoUploading = signal<boolean>(false);
  readonly bannerUploading = signal<boolean>(false);

  /** 学校列表（host 为全部学校，租户上下文只有本校） */
  readonly schools = signal<SchoolDto[]>([]);
  readonly keyword = signal<string>('');

  readonly filteredSchools = computed(() => {
    const kw = this.keyword().trim().toLowerCase();
    const list = this.schools();
    if (!kw) {
      return list;
    }
    return list.filter(
      (s) =>
        (s.name || '').toLowerCase().includes(kw) ||
        (s.domain || '').toLowerCase().includes(kw) ||
        (s.slug || '').toLowerCase().includes(kw),
    );
  });

  readonly selected = signal<SchoolDto | null>(null);

  /** 是否 host 全局管理员（host 上下文没有 tenantId） */
  readonly isHost = signal<boolean>(false);

  readonly publicSchool = signal<PublicSchoolDto | null>(null);
  readonly tenants = signal<TenantResourceSummaryDto[]>([]);
  readonly baseDomains = signal<string[]>([]);
  baseDomainsInput = '';

  form: UpdateSchoolDto = this.emptyForm();

  ngOnInit(): void {
    const currentUser = this.configState.getOne('currentUser') as { tenantId?: string } | undefined;
    this.isHost.set(!currentUser?.tenantId);

    this.loadSchools();

    if (this.isHost()) {
      this.loadPlatformSettings();
    }
  }

  // ── 学校列表 ────────────────────────────────────────────────

  private loadSchools(): void {
    this.loading.set(true);
    this.schoolService.getList().subscribe({
      next: (list) => {
        this.schools.set(list || []);
        this.loading.set(false);
        if (!this.selected() && (list || []).length) {
          this.select(list[0]);
        }
      },
      error: () => {
        this.loading.set(false);
        this.schools.set([]);
        this.message.error('加载学校列表失败');
      },
    });
  }

  select(school: SchoolDto): void {
    this.selected.set(school);
    this.form = {
      name: school.name || '',
      logoUrl: school.logoUrl || '',
      bannerUrl: school.bannerUrl || '',
      introduction: school.introduction || '',
      website: school.website || '',
      address: school.address || '',
      contactPhone: school.contactPhone || '',
      contactEmail: school.contactEmail || '',
      isPublic: school.isPublic,
      sortOrder: school.sortOrder ?? 0,
      domain: school.domain || '',
      slug: school.slug || '',
    };
  }

  // ── 保存学校档案 ────────────────────────────────────────────

  save(): void {
    const target = this.selected();
    if (!target) {
      this.message.warning('请先选择要编辑的学校');
      return;
    }
    const tenantId = target.tenantId;
    if (!tenantId) {
      this.message.error('学校缺少租户标识，无法保存');
      return;
    }
    if (!this.form.name?.trim()) {
      this.message.warning('学校名称不能为空');
      return;
    }

    this.saving.set(true);
    this.schoolService.saveByTenantId(tenantId, this.form).subscribe({
      next: (res) => {
        this.saving.set(false);
        this.schools.update((list) => list.map((s) => (s.tenantId === res.tenantId ? res : s)));
        this.select(res);
        this.message.success(`「${res.name}」的学校档案已保存`);
      },
      error: (err) => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '保存失败');
      },
    });
  }

  /** 还原为最近一次保存的内容 */
  reset(): void {
    const target = this.selected();
    if (target) {
      this.select(target);
    }
  }

  // ── 图片上传（OSS） ─────────────────────────────────────────

  onLogoSelected(event: Event): void {
    this.uploadImage(event, this.logoUploading, (url) => (this.form.logoUrl = url));
  }

  onBannerSelected(event: Event): void {
    this.uploadImage(event, this.bannerUploading, (url) => (this.form.bannerUrl = url));
  }

  private uploadImage(event: Event, flag: WritableSignal<boolean>, apply: (url: string) => void): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    if (!file.type.startsWith('image/')) {
      this.message.error('只能上传图片文件');
      return;
    }
    if (file.size / 1024 / 1024 > 5) {
      this.message.error('图片大小不能超过 5MB');
      return;
    }

    flag.set(true);
    this.ossUploadService.uploadImage(file).subscribe({
      next: (res) => {
        flag.set(false);
        apply(res.url);
        this.message.success('图片上传成功，保存后生效');
      },
      error: () => {
        flag.set(false);
        this.message.error('图片上传失败，请重试');
      },
    });
    input.value = '';
  }

  // ── 展示辅助 ────────────────────────────────────────────────

  /** 校徽占位：取校名前两个字 */
  markText(name?: string | null): string {
    const n = (name || '').trim();
    return n ? n.slice(0, 2) : '学校';
  }

  /** 学校门户地址（公开预览页） */
  previewUrl(tenantId?: string | null): string {
    return `/school/${tenantId || ''}`;
  }

  /** 子域名预览：{slug}.{基础域名} */
  slugPreview(): string {
    const slug = (this.form.slug || '').trim();
    const base = this.baseDomains()[0];
    return slug && base ? `${slug}.${base}` : '';
  }

  // ── 平台级设置（仅 host） ──────────────────────────────────

  private loadPlatformSettings(): void {
    this.schoolService.getPublicSchool().subscribe({
      next: (res) => this.publicSchool.set(res),
      error: () => this.publicSchool.set(null),
    });

    this.portalService.getPublicTenantList().subscribe({
      next: (list) => this.tenants.set(list || []),
      error: () => this.tenants.set([]),
    });

    this.schoolService.getDomainSettings().subscribe({
      next: (res) => {
        this.baseDomains.set(res?.baseDomains || []);
        this.baseDomainsInput = (res?.baseDomains || []).join(', ');
      },
      error: () => undefined,
    });
  }

  /** 保存平台基础域名（仅 host 可见） */
  saveBaseDomains(): void {
    const list = this.baseDomainsInput
      .split(/[,;，\s]+/)
      .map((x) => x.trim())
      .filter((x) => !!x);

    this.schoolService.setBaseDomains({ baseDomains: list }).subscribe({
      next: (res) => {
        this.baseDomains.set(res?.baseDomains || []);
        this.baseDomainsInput = (res?.baseDomains || []).join(', ');
        this.message.success('平台基础域名已保存，学校子域名即刻生效');
      },
      error: (err) => this.message.error(err?.error?.error?.message || '保存失败'),
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
      error: (err) => this.message.error(err?.error?.error?.message || '设置失败'),
    });
  }

  private emptyForm(): UpdateSchoolDto {
    return {
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
  }
}
