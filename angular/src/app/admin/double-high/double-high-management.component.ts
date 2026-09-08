import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import {
  CreateUpdateDoubleHighProjectDto,
  DoubleHighProjectDto,
  DoubleHighProjectStatus,
  DoubleHighService,
} from '../../double-high/double-high.service';

@Component({
  selector: 'app-double-high-management',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzCardModule, NzDatePickerModule, NzInputModule, NzModalModule, NzSelectModule, NzTableModule],
  templateUrl: './double-high-management.component.html',
  styleUrls: ['./double-high-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DoubleHighManagementComponent implements OnInit {
  private readonly doubleHighService = inject(DoubleHighService);
  private readonly message = inject(NzMessageService);

  readonly items = signal<DoubleHighProjectDto[]>([]);
  readonly statuses = DoubleHighProjectStatus;
  // 表格分页（前端分页：数据已全量加载，按页切片展示）
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  readonly pagedItems = computed(() => {
    const all = this.items();
    const start = (this.pageIndex() - 1) * this.pageSize();
    return all.slice(start, start + this.pageSize());
  });

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageIndex.set(1);
  }

  // 关键修复：原实现是普通 boolean 属性，OnPush 组件在 subscribe 回调里
  // 改写它后不会触发变更检测，导致编辑弹窗永远不渲染（DOM 里 modal 元素
  // 都不存在，但组件实例里 modalVisible 已经是 true）。改用 signal 后，
  // set() 会自动把组件标脏，下一帧模板就能拿到最新值并把 modal 渲染出来。
  readonly modalVisible = signal(false);
  readonly saving = signal(false);
  editingId: string | null = null;
  keyword = '';
  form: CreateUpdateDoubleHighProjectDto = this.createEmptyForm();
  // nz-date-picker 绑定的本地 Date 对象，保存时再转 ISO 字符串
  startDate: Date | null = null;
  endDate: Date | null = null;

  ngOnInit(): void {
    this.reload();
  }

  createEmptyForm(): CreateUpdateDoubleHighProjectDto {
    return {
      title: '',
      batchCode: '',
      description: '',
      status: DoubleHighProjectStatus.Draft,
      startTime: undefined,
      endTime: undefined,
      // 指标在项目详情页单独管理，这里新建时为空
      indicators: [],
    };
  }

  reload(): void {
    this.pageIndex.set(1);
    this.doubleHighService.getList({
      filter: this.keyword?.trim() || undefined,
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.items.set(result.items || []));
  }

  resetSearch(): void {
    this.keyword = '';
    this.reload();
  }

  openCreate(): void {
    this.editingId = null;
    this.form = this.createEmptyForm();
    this.startDate = null;
    this.endDate = null;
    this.modalVisible.set(true);
  }

  openEdit(item: DoubleHighProjectDto): void {
    this.editingId = item.id;
    this.doubleHighService.getDetail(item.id).subscribe(detail => {
      this.form = {
        title: detail.title,
        batchCode: detail.batchCode,
        description: detail.description || '',
        status: detail.status,
        startTime: detail.startTime,
        endTime: detail.endTime,
        // 编辑时原样带回已有指标（本页不展示编辑），避免后端 ReplaceIndicators 把指标清空
        indicators: detail.indicators.map(x => ({
          parentId: x.parentId,
          categoryName: x.categoryName || '',
          indicatorCode: x.indicatorCode || '',
          name: x.name || '',
          description: x.description || '',
          unit: x.unit || '',
          dataSourceType: x.dataSourceType,
          targetValue: x.targetValue,
          weight: x.weight,
          sortOrder: x.sortOrder,
        })),
      };
      // nz-date-picker 需要 Date 对象，ISO 字符串转 Date
      this.startDate = detail.startTime ? new Date(detail.startTime) : null;
      this.endDate = detail.endTime ? new Date(detail.endTime) : null;
      // 关键修复：用 signal.set() 而不是 =，确保 OnPush 组件在异步回调里
      // 也能触发变更检测，让 modal 真正渲染到 DOM。
      this.modalVisible.set(true);
    });
  }

  save(): void {
    // 本页只维护项目基本信息，指标在项目详情页单独管理
    if (!this.form.title?.trim()) {
      this.message.warning('请填写项目名称');
      return;
    }
    if (!this.form.batchCode?.trim()) {
      this.message.warning('请填写批次编码');
      return;
    }
    // 2) 编码重复前端预检（编辑带回的已有指标）
    const codes = this.form.indicators.map(x => (x.indicatorCode ?? '').trim());
    if (new Set(codes).size !== codes.length) {
      this.message.warning('存在重复的指标编码，请检查后重试');
      return;
    }

    // 3) 同步 nz-date-picker 的 Date 到表单字符串（ISO，无秒截断兼容）
    const payload: CreateUpdateDoubleHighProjectDto = {
      ...this.form,
      title: this.form.title.trim(),
      batchCode: this.form.batchCode.trim(),
      description: this.form.description?.trim(),
      startTime: this.startDate ? this.startDate.toISOString() : undefined,
      endTime: this.endDate ? this.endDate.toISOString() : undefined,
      indicators: this.form.indicators.map(x => ({
        ...x,
        categoryName: (x.categoryName ?? '').trim(),
        indicatorCode: (x.indicatorCode ?? '').trim(),
        name: (x.name ?? '').trim(),
        description: x.description?.trim(),
        unit: x.unit?.trim(),
        weight: x.weight == null || x.weight === 0 ? 1 : Math.round(Number(x.weight)),
      })),
    };

    this.saving.set(true);
    const request = this.editingId
      ? this.doubleHighService.update(this.editingId, payload)
      : this.doubleHighService.create(payload);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.modalVisible.set(false);
        this.message.success('双高评估项目已保存');
        this.reload();
      },
      // 关键修复：旧实现 `error: () => this.message.error('保存失败')` 丢弃了 err 形参，
      // 一旦后端返回 500（例如 ReplaceIndicatorsAsync 中 NRE、外键冲突、唯一索引冲突），
      // 用户和开发者都看不到真实原因。
      // 现在把后端 message 透传出来，并打 console.error 留详细堆栈供排查。
      error: err => {
        this.saving.set(false);
        this.showApiError(err, '保存失败');
      },
    });
  }

  delete(id: string): void {
    this.doubleHighService.delete(id).subscribe({
      next: () => {
        this.message.success('项目已删除');
        this.reload();
      },
      error: err => this.showApiError(err, '删除失败'),
    });
  }

  /**
   * 统一提取并展示后端真实错误（与 chapter-management.component.ts 保持一致）。
   * 提取顺序与 ABP 默认 UserFriendlyException 响应体一致：
   *   { error: { code, message, details, ... } }
   * 兼容直接的 { message } 包装与原生 Error。
   */
  private showApiError(err: any, fallback: string): void {
    const detail =
      err?.error?.error?.message ||
      err?.error?.message ||
      err?.message ||
      '未知错误';
    console.error('[DoubleHigh]', fallback, err);
    this.message.error(`${fallback}：${detail}`);
  }

  getStatusLabel(status: DoubleHighProjectStatus): string {
    const labels: Record<number, string> = {
      [DoubleHighProjectStatus.Draft]: '草稿',
      [DoubleHighProjectStatus.Active]: '进行中',
      [DoubleHighProjectStatus.Closed]: '已关闭',
    };
    return labels[status] || '未知';
  }
}
