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
          categoryName: x.categoryName,
          indicatorCode: x.indicatorCode,
          name: x.name,
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
      error: () => this.message.error('保存失败'),
    });
  }

  delete(id: string): void {
    this.doubleHighService.delete(id).subscribe({
      next: () => {
        this.message.success('项目已删除');
        this.reload();
      },
      error: () => this.message.error('删除失败'),
    });
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
