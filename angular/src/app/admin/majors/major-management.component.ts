import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { MajorService } from '../../proxy/majors/major.service';
import type { CreateUpdateMajorDto, MajorDto } from '../../proxy/majors/dtos/models';

@Component({
  selector: 'app-major-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzFormModule,
    NzInputModule,
    NzModalModule,
    NzTableModule,
  ],
  templateUrl: './major-management.component.html',
  styleUrls: ['./major-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MajorManagementComponent implements OnInit {
  private readonly majorService = inject(MajorService);
  private readonly message = inject(NzMessageService);

  readonly items = signal<MajorDto[]>([]);
  readonly filter = signal<string>('');

  modalVisible = false;
  editingId: string | null = null;
  form: CreateUpdateMajorDto = this.createEmptyForm();

  ngOnInit(): void {
    this.reload();
  }

  createEmptyForm(): CreateUpdateMajorDto {
    return { name: '', code: '', description: '', trainingObjectives: '' };
  }

  reload(): void {
    this.majorService
      .getList({
        filter: this.filter() || undefined,
        maxResultCount: 200,
        skipCount: 0,
      })
      .subscribe({
        next: (res) => this.items.set(res.items || []),
        error: () => this.message.error('加载专业列表失败'),
      });
  }

  openCreate(): void {
    this.editingId = null;
    this.form = this.createEmptyForm();
    this.modalVisible = true;
  }

  openEdit(item: MajorDto): void {
    this.editingId = item.id;
    this.form = {
      name: item.name || '',
      code: item.code || '',
      description: item.description || '',
      trainingObjectives: item.trainingObjectives || '',
    };
    this.modalVisible = true;
  }

  save(): void {
    if (!this.form.name?.trim()) {
      this.message.warning('专业名称不能为空');
      return;
    }
    const request = this.editingId
      ? this.majorService.update(this.editingId, this.form)
      : this.majorService.create(this.form);

    request.subscribe({
      next: () => {
        this.modalVisible = false;
        this.message.success(this.editingId ? '专业已更新' : '专业已创建');
        this.reload();
      },
      error: (err) =>
        this.message.error(
          err?.error?.error?.message || err?.message || '保存失败'
        ),
    });
  }

  delete(item: MajorDto): void {
    this.majorService.delete(item.id).subscribe({
      next: () => {
        this.message.success('专业已删除');
        this.reload();
      },
      error: (err) =>
        this.message.error(
          err?.error?.error?.message || err?.message || '删除失败'
        ),
    });
  }
}
