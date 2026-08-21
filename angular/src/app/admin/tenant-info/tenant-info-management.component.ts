import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzUploadModule } from 'ng-zorro-antd/upload';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { TenantInfoService } from '../../proxy/tenant-infos/tenant-info.service';
import { TenantType } from '../../proxy/tenant-infos/enums/tenant-type.enum';
import { OssUploadService, OssUploadResultDto } from '../../shared/oss-upload.service';

import type {
  TenantInfoDto,
  TenantInfoListItemDto,
  CreateUpdateTenantInfoDto,
  SpecialProjectItem,
} from '../../proxy/tenant-infos/dtos/models';

@Component({
  selector: 'app-tenant-info-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzTableModule,
    NzFormModule,
    NzInputModule,
    NzSelectModule,
    NzTagModule,
    NzIconModule,
    NzEmptyModule,
    NzAlertModule,
    NzUploadModule,
    NzSpinModule,
    NzModalModule,
  ],
  templateUrl: './tenant-info-management.component.html',
  styleUrls: ['./tenant-info-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TenantInfoManagementComponent implements OnInit {
  private readonly tenantInfoService = inject(TenantInfoService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly message = inject(NzMessageService);

  // ── 租户列表（表格） ──
  readonly tenants = signal<TenantInfoListItemDto[]>([]);
  readonly listLoading = signal(false);

  // ── 当前编辑的租户 ──
  readonly selected = signal<TenantInfoListItemDto | null>(null);
  readonly detailLoading = signal(false);
  readonly saving = signal(false);
  readonly editModalVisible = signal(false);

  // ── 编辑表单 ──
  readonly coverImages = signal<string[]>([]);
  readonly uploading = signal(false);
  form: CreateUpdateTenantInfoDto = this.createEmptyForm();

  readonly TenantType = TenantType;

  ngOnInit(): void {
    this.loadTenants();
  }

  private createEmptyForm(): CreateUpdateTenantInfoDto {
    return {
      name: '',
      type: TenantType.Professional,
      description: null,
      coverImageList: [],
      talentTrainingPlan: null,
      professionalTeachingStandards: null,
      specialProjectList: [],
    };
  }

  // ── 加载所有租户 ──
  loadTenants(): void {
    this.listLoading.set(true);
    this.tenantInfoService.getList().subscribe({
      next: (res) => {
        const list = (res || []).filter((x) => !!x.tenantId);
        this.tenants.set(list);
        this.listLoading.set(false);
      },
      error: () => {
        this.listLoading.set(false);
        this.message.error('加载租户列表失败');
      },
    });
  }

  // ── 选择租户并加载其关联信息 ──
  selectTenant(item: TenantInfoListItemDto): void {
    if (!item.tenantId) return;
    // 先轻量更新选中态，并弹出编辑弹窗
    this.selected.set(item);
    this.editModalVisible.set(true);
    this.detailLoading.set(true);
    this.tenantInfoService.getByTenantId(item.tenantId).subscribe({
      next: (res: TenantInfoDto) => {
        this.detailLoading.set(false);
        this.applyDetail(res);
        // 用加载到的展示名称刷新列表行
        this.tenants.update((list) =>
          list.map((t) => (t.tenantId === item.tenantId ? { ...t, name: res.name || t.name, type: res.type ?? t.type } : t))
        );
      },
      error: () => {
        this.detailLoading.set(false);
        this.message.error('加载租户详细信息失败');
      },
    });
  }

  private applyDetail(res: TenantInfoDto): void {
    this.form = {
      name: res.name || '',
      type: res.type ?? TenantType.Professional,
      description: res.description || null,
      coverImageList: res.coverImageList || [],
      talentTrainingPlan: res.talentTrainingPlan || null,
      professionalTeachingStandards: res.professionalTeachingStandards || null,
      specialProjectList: (res.specialProjectList || []).map((x) => ({
        title: x.title || '',
        description: x.description || null,
      })),
    };
    this.coverImages.set(res.coverImageList || []);
  }

  // ── OSS 封面图上传 ──
  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      this.message.error('只能上传图片文件');
      return;
    }
    if (file.size / 1024 / 1024 > 5) {
      this.message.error('图片大小不能超过 5MB');
      return;
    }
    this.doUpload(file);
    input.value = ''; // 允许重复选择同一文件
  }

  private doUpload(file: File): void {
    this.uploading.set(true);
    this.ossUploadService.uploadImage(file).subscribe({
      next: (res: OssUploadResultDto) => {
        const newImages = [...this.coverImages(), res.url];
        this.coverImages.set(newImages);
        this.form.coverImageList = newImages;
        this.uploading.set(false);
        this.message.success('图片上传成功');
      },
      error: () => {
        this.uploading.set(false);
        this.message.error('图片上传失败，请重试');
      },
    });
  }

  removeImage(index: number): void {
    const newImages = this.coverImages().filter((_, i) => i !== index);
    this.coverImages.set(newImages);
    this.form.coverImageList = newImages;
  }

  // ── 保存当前租户的信息 ──
  save(): void {
    const tenantId = this.selected()?.tenantId;
    if (!tenantId) {
      this.message.warning('请先选择一个租户');
      return;
    }
    if (!this.form.name?.trim()) {
      this.message.warning('名称不能为空');
      return;
    }

    this.form.coverImageList = this.coverImages();

    this.saving.set(true);
    this.tenantInfoService.saveByTenantId(tenantId, this.form).subscribe({
      next: () => {
        this.saving.set(false);
        this.message.success(`「${this.selected()?.name || ''}」的租户信息已保存`);
        this.editModalVisible.set(false);
        this.loadTenants();
      },
      error: (err: any) => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || err?.message || '保存失败');
      },
    });
  }

  // ── 特色项目 ──
  addSpecialProject(): void {
    this.form.specialProjectList = [
      ...this.form.specialProjectList,
      { title: '', description: null },
    ];
  }

  removeSpecialProject(index: number): void {
    this.form.specialProjectList = this.form.specialProjectList.filter(
      (_: any, i: number) => i !== index
    );
  }
}
