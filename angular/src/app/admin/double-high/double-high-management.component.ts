import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import {
  CreateUpdateDoubleHighIndicatorDto,
  CreateUpdateDoubleHighProjectDto,
  DoubleHighDataSourceType,
  DoubleHighProjectDto,
  DoubleHighProjectStatus,
  DoubleHighService,
} from '../../double-high/double-high.service';

@Component({
  selector: 'app-double-high-management',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzCardModule, NzInputModule, NzModalModule, NzSelectModule, NzTableModule],
  templateUrl: './double-high-management.component.html',
  styleUrls: ['./double-high-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DoubleHighManagementComponent implements OnInit {
  private readonly doubleHighService = inject(DoubleHighService);
  private readonly message = inject(NzMessageService);

  readonly items = signal<DoubleHighProjectDto[]>([]);
  readonly statuses = DoubleHighProjectStatus;
  readonly dataSources = DoubleHighDataSourceType;

  modalVisible = false;
  editingId: string | null = null;
  form: CreateUpdateDoubleHighProjectDto = this.createEmptyForm();

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
      indicators: [this.createEmptyIndicator(1)],
    };
  }

  createEmptyIndicator(sortOrder: number): CreateUpdateDoubleHighIndicatorDto {
    return {
      categoryName: '',
      indicatorCode: '',
      name: '',
      description: '',
      unit: '',
      dataSourceType: DoubleHighDataSourceType.Manual,
      targetValue: undefined,
      weight: 1,
      sortOrder,
    };
  }

  reload(): void {
    this.doubleHighService.getList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.items.set(result.items || []));
  }

  openCreate(): void {
    this.editingId = null;
    this.form = this.createEmptyForm();
    this.modalVisible = true;
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
        indicators: detail.indicators.map(x => ({
          parentId: x.parentId,
          // 关键修复：原实现 categoryName / indicatorCode / name 没有 `|| ''` 兜底，
          // 一旦后端返回 null（例如手动构造的数据 / 旧版数据 / 测试桩），前端会把 null
          // 原样发回；后端 ReplaceIndicatorsAsync 中的 .Trim() 会抛 NullReferenceException，
          // 直接 500，前端只看到笼统的"保存失败"。
          // 改为与 description / unit 同样的兜底，把字符串字段都规整成 '' 再发出去。
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
      this.modalVisible = true;
    });
  }

  addIndicator(): void {
    this.form.indicators.push(this.createEmptyIndicator(this.form.indicators.length + 1));
  }

  removeIndicator(index: number): void {
    this.form.indicators.splice(index, 1);
    this.form.indicators.forEach((item, idx) => item.sortOrder = idx + 1);
  }

  save(): void {
    const request = this.editingId
      ? this.doubleHighService.update(this.editingId, this.form)
      : this.doubleHighService.create(this.form);

    request.subscribe({
      next: () => {
        this.modalVisible = false;
        this.message.success('双高评估项目已保存');
        this.reload();
      },
      // 关键修复：旧实现 `error: () => this.message.error('保存失败')` 丢弃了 err 形参，
      // 一旦后端返回 500（例如 ReplaceIndicatorsAsync 中 NRE、外键冲突、唯一索引冲突），
      // 用户和开发者都看不到真实原因。
      // 现在把后端 message 透传出来，并打 console.error 留详细堆栈供排查。
      error: err => this.showApiError(err, '保存失败'),
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

  getDataSourceLabel(type: DoubleHighDataSourceType): string {
    const labels: Record<number, string> = {
      [DoubleHighDataSourceType.Manual]: '手工填报',
      [DoubleHighDataSourceType.ResourceCount]: '资源数量',
      [DoubleHighDataSourceType.CourseCount]: '课程数量',
      [DoubleHighDataSourceType.MicroMajorCount]: '微专业数量',
      [DoubleHighDataSourceType.PracticumProjectCount]: '实训项目数量',
      [DoubleHighDataSourceType.NewsArticleCount]: '资讯数量',
      [DoubleHighDataSourceType.MicroMajorEnrollmentCount]: '微专业报名量',
      [DoubleHighDataSourceType.PracticumEnrollmentCount]: '实训参与量',
    };
    return labels[type] || '未知';
  }
}
