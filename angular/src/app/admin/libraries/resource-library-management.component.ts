import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ResourceLibraryService } from '../../proxy/resource-libraries/resource-library.service';
import type {
  AddResourceLibraryMemberDto,
  CreateResourceLibraryDto,
  ResourceLibraryDto,
  ResourceLibraryMemberDto,
  UpdateResourceLibraryDto,
} from '../../proxy/resource-libraries/dtos/models';
import { MajorService } from '../../proxy/majors/major.service';
import type { MajorDto } from '../../proxy/majors/dtos/models';
import { PortalService } from '../../proxy/portal/portal.service';
import type { TenantResourceSummaryDto } from '../../proxy/portal/models';

/**
 * 资源库管理（管理端，多校共建）。
 *
 * 需求「资源库可以多校共建，体现主建学校（专业），和合作学校」：
 * - 库由本校（主建学校）创建并绑定主建专业；
 * - 合作学校通过成员表加入，加入后即可浏览库内资源/课程；
 * - 只有主建学校可以改库、管成员、发布 / 归档。
 */
@Component({
  selector: 'app-resource-library-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzFormModule,
    NzInputModule,
    NzModalModule,
    NzSelectModule,
    NzSpaceModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './resource-library-management.component.html',
  styleUrls: ['./resource-library-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceLibraryManagementComponent implements OnInit {
  private readonly libraryService = inject(ResourceLibraryService);
  private readonly majorService = inject(MajorService);
  private readonly portalService = inject(PortalService);
  private readonly message = inject(NzMessageService);

  readonly items = signal<ResourceLibraryDto[]>([]);
  readonly majors = signal<MajorDto[]>([]);
  readonly tenants = signal<TenantResourceSummaryDto[]>([]);
  readonly loading = signal<boolean>(false);

  /** 建库 / 改库弹窗 */
  modalVisible = false;
  editingId: string | null = null;
  form: CreateResourceLibraryDto & UpdateResourceLibraryDto = this.emptyForm();

  /** 成员弹窗 */
  memberModalVisible = false;
  memberLibrary: ResourceLibraryDto | null = null;
  memberTenantId: string | null = null;

  readonly statusLabels: Record<number, string> = { 0: '草稿', 1: '已发布', 2: '已归档' };

  ngOnInit(): void {
    this.reload();

    this.majorService.getList({ maxResultCount: 200, skipCount: 0 }).subscribe({
      next: (res) => this.majors.set(res.items || []),
      error: () => this.majors.set([]),
    });

    this.portalService.getPublicTenantList().subscribe({
      next: (list) => this.tenants.set(list || []),
      error: () => this.tenants.set([]),
    });
  }

  emptyForm(): CreateResourceLibraryDto & UpdateResourceLibraryDto {
    return {
      name: '',
      description: '',
      coverUrl: '',
      primaryMajorId: null,
      isPublic: true,
      sortOrder: 0,
    };
  }

  reload(): void {
    this.loading.set(true);
    this.libraryService.getList({}).subscribe({
      next: (list) => {
        this.items.set(list || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载资源库失败');
      },
    });
  }

  openCreate(): void {
    this.editingId = null;
    this.form = this.emptyForm();
    this.modalVisible = true;
  }

  openEdit(item: ResourceLibraryDto): void {
    this.editingId = item.id;
    this.form = {
      name: item.name || '',
      description: item.description || '',
      coverUrl: item.coverUrl || '',
      primaryMajorId: item.primaryMajorId || null,
      isPublic: item.isPublic,
      sortOrder: item.sortOrder,
    };
    this.modalVisible = true;
  }

  save(): void {
    if (!this.form.name?.trim()) {
      this.message.warning('请填写资源库名称');
      return;
    }

    if (this.editingId) {
      this.libraryService.update(this.editingId, this.form as UpdateResourceLibraryDto).subscribe({
        next: () => {
          this.modalVisible = false;
          this.message.success('已保存');
          this.reload();
        },
        error: (err) => this.message.error(err?.error?.error?.message || '保存失败'),
      });
      return;
    }

    this.libraryService.create(this.form as CreateResourceLibraryDto).subscribe({
      next: () => {
        this.modalVisible = false;
        this.message.success('资源库已创建（草稿），加入合作学校后可发布');
        this.reload();
      },
      error: (err) => this.message.error(err?.error?.error?.message || '创建失败'),
    });
  }

  remove(item: ResourceLibraryDto): void {
    this.libraryService.delete(item.id).subscribe({
      next: () => {
        this.message.success('已删除');
        this.reload();
      },
      error: (err) => this.message.error(err?.error?.error?.message || '删除失败'),
    });
  }

  /** 发布 / 归档 */
  changeStatus(item: ResourceLibraryDto, status: number): void {
    this.libraryService.update(item.id, { status } as UpdateResourceLibraryDto).subscribe({
      next: () => {
        this.message.success(status === 1 ? '已发布' : '已归档');
        this.reload();
      },
      error: (err) => this.message.error(err?.error?.error?.message || '操作失败'),
    });
  }

  openMembers(item: ResourceLibraryDto): void {
    this.memberLibrary = item;
    this.memberTenantId = null;
    this.memberModalVisible = true;
  }

  addMember(): void {
    if (!this.memberLibrary || !this.memberTenantId) {
      this.message.warning('请选择合作学校');
      return;
    }

    const input: AddResourceLibraryMemberDto = { memberTenantId: this.memberTenantId };
    this.libraryService.addMember(this.memberLibrary.id, input).subscribe({
      next: () => {
        this.message.success('已加入共建');
        this.memberTenantId = null;
        this.reload();
        this.refreshMembers();
      },
      error: (err) => this.message.error(err?.error?.error?.message || '加入失败'),
    });
  }

  removeMember(member: ResourceLibraryMemberDto): void {
    if (!this.memberLibrary) {
      return;
    }

    this.libraryService.removeMember(this.memberLibrary.id, member.memberTenantId).subscribe({
      next: () => {
        this.message.success('已移除');
        this.reload();
        this.refreshMembers();
      },
      error: (err) => this.message.error(err?.error?.error?.message || '移除失败'),
    });
  }

  /** 成员变更后刷新弹窗内的成员列表 */
  private refreshMembers(): void {
    if (!this.memberLibrary) {
      return;
    }

    const id = this.memberLibrary.id;
    this.libraryService.get(id).subscribe({
      next: (fresh) => {
        this.memberLibrary = fresh;
        this.items.update(list => list.map(x => (x.id === id ? fresh : x)));
      },
      error: () => undefined,
    });
  }

  /** 可选合作学校：排除主建学校与已在库内的成员 */
  candidateTenants(library: ResourceLibraryDto): TenantResourceSummaryDto[] {
    const existing = new Set((library.members || []).map(m => m.memberTenantId));
    return this.tenants().filter(t => t.id && t.id !== library.ownerTenantId && !existing.has(t.id));
  }
}
