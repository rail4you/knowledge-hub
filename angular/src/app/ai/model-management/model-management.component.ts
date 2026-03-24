import { Component, signal, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzUploadModule, NzUploadChangeParam } from 'ng-zorro-antd/upload';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzBadgeModule } from 'ng-zorro-antd/badge';

interface AIModel {
  id: string;
  name: string;
  version: string;
  type: string;
  status: 'active' | 'inactive' | 'training';
  accuracy: number;
  trainedOn: string;
  description: string;
}

@Component({
  selector: 'app-model-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzGridModule,
    NzTableModule,
    NzTagModule,
    NzIconModule,
    NzUploadModule,
    NzModalModule,
    NzTooltipModule,
    NzProgressModule,
    NzBadgeModule
  ],
  templateUrl: './model-management.component.html',
  styleUrls: ['./model-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ModelManagementComponent {
  private readonly message = inject(NzMessageService);
  private readonly apiUrl = 'http://localhost:5000';

  models = signal<AIModel[]>([
    {
      id: '1',
      name: 'Qwen-Plus',
      version: '1.0',
      type: '大语言模型',
      status: 'active',
      accuracy: 95,
      trainedOn: '2026-03-01',
      description: '通义千问Plus模型，适用于通用问答场景'
    },
    {
      id: '2',
      name: 'LessonPlan-Generator',
      version: '2.1',
      type: '教案生成',
      status: 'active',
      accuracy: 88,
      trainedOn: '2026-03-15',
      description: '专门训练的教学设计辅助模型'
    },
    {
      id: '3',
      name: 'CaseAnalyzer',
      version: '1.5',
      type: '案例分析',
      status: 'active',
      accuracy: 85,
      trainedOn: '2026-03-10',
      description: '商业案例分析专用模型'
    },
    {
      id: '4',
      name: 'CareerAdvisor',
      version: '1.0',
      type: '职业规划',
      status: 'inactive',
      accuracy: 82,
      trainedOn: '2026-03-20',
      description: '学生职业发展指导模型'
    }
  ]);

  isLoading = signal(false);
  selectedModel = signal<AIModel | null>(null);
  isModalVisible = signal(false);

  handleUpload = (info: NzUploadChangeParam): void => {
    const file = info.file;
    if (info.file.status === 'done') {
      this.message.success(`模型 ${file.name} 上传成功`);
      this.loadModels();
    } else if (info.file.status === 'error') {
      this.message.error(`模型 ${file.name} 上传失败`);
    }
  };

  loadModels(): void {
    this.isLoading.set(true);
    fetch(`${this.apiUrl}/api/models`)
      .then(res => {
        if (!res.ok) throw new Error('Failed to load models');
        return res.json();
      })
      .then(data => {
        if (Array.isArray(data)) {
          this.models.set(data);
        }
      })
      .catch(() => {
        this.message.warning('使用本地模型数据');
      })
      .finally(() => {
        this.isLoading.set(false);
      });
  }

  showDetail(model: AIModel): void {
    this.selectedModel.set(model);
    this.isModalVisible.set(true);
  }

  toggleStatus(model: AIModel): void {
    this.models.update(models => 
      models.map(m => 
        m.id === model.id 
          ? { ...m, status: m.status === 'active' ? 'inactive' : 'active' as const }
          : m
      )
    );
    this.message.success(`模型 ${model.name} 状态已更新`);
  }

  deleteModel(model: AIModel): void {
    this.models.update(models => models.filter(m => m.id !== model.id));
    this.message.success(`模型 ${model.name} 已删除`);
  }

  getStatusColor(status: string): string {
    switch (status) {
      case 'active': return 'green';
      case 'inactive': return 'default';
      case 'training': return 'blue';
      default: return 'default';
    }
  }

  getStatusText(status: string): string {
    switch (status) {
      case 'active': return '运行中';
      case 'inactive': return '已停止';
      case 'training': return '训练中';
      default: return status;
    }
  }

  getAccuracyStatus(accuracy: number): 'success' | 'normal' | 'exception' {
    if (accuracy >= 90) return 'success';
    if (accuracy >= 70) return 'normal';
    return 'exception';
  }

  getActiveCount(): number {
    return this.models().filter(m => m.status === 'active').length;
  }

  getInactiveCount(): number {
    return this.models().filter(m => m.status === 'inactive').length;
  }

  getTrainingCount(): number {
    return this.models().filter(m => m.status === 'training').length;
  }
}