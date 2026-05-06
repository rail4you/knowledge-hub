import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import {
  CreateDoubleHighEvidenceDto,
  DoubleHighDataSourceType,
  DoubleHighEvidenceType,
  DoubleHighIndicatorDto,
  DoubleHighProjectDetailDto,
  DoubleHighProjectStatus,
  DoubleHighService,
} from './double-high.service';

@Component({
  selector: 'app-double-high-project-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzDescriptionsModule,
    NzInputModule,
    NzModalModule,
    NzProgressModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './double-high-project-detail.component.html',
  styleUrls: ['./double-high-project-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DoubleHighProjectDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly doubleHighService = inject(DoubleHighService);
  private readonly message = inject(NzMessageService);

  readonly project = signal<DoubleHighProjectDetailDto | null>(null);
  readonly dataSources = DoubleHighDataSourceType;
  readonly evidenceTypes = DoubleHighEvidenceType;
  readonly statuses = DoubleHighProjectStatus;

  evidenceVisible = false;
  selectedIndicatorId: string | null = null;
  manualValues: Record<string, number | null> = {};
  manualNotes: Record<string, string> = {};
  evidenceForm: CreateDoubleHighEvidenceDto = this.createEmptyEvidenceForm();

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.load(id);
    }
  }

  createEmptyEvidenceForm(): CreateDoubleHighEvidenceDto {
    return {
      projectId: '',
      indicatorId: '',
      title: '',
      description: '',
      evidenceType: DoubleHighEvidenceType.ExternalLink,
      resourceId: undefined,
      attachmentUrl: '',
      externalLink: '',
      sortOrder: 1,
    };
  }

  load(id: string): void {
    this.doubleHighService.getDetail(id).subscribe(detail => {
      this.project.set(detail);
      for (const item of detail.indicators) {
        this.manualValues[item.id] = item.latestValue?.value ?? null;
        this.manualNotes[item.id] = item.latestValue?.note || '';
      }
    });
  }

  collect(): void {
    const project = this.project();
    if (!project) {
      return;
    }

    this.doubleHighService.collectProject(project.id).subscribe({
      next: () => {
        this.message.success('自动采集完成');
        this.load(project.id);
      },
      error: () => this.message.error('自动采集失败'),
    });
  }

  saveManualValue(indicator: DoubleHighIndicatorDto): void {
    const value = this.manualValues[indicator.id];
    if (value === null || value === undefined) {
      this.message.warning('请输入手工值');
      return;
    }

    this.doubleHighService.saveManualValue({
      indicatorId: indicator.id,
      value,
      note: this.manualNotes[indicator.id],
    }).subscribe({
      next: () => {
        this.message.success('手工填报已保存');
        const project = this.project();
        if (project) {
          this.load(project.id);
        }
      },
      error: () => this.message.error('保存失败'),
    });
  }

  openEvidence(indicator: DoubleHighIndicatorDto): void {
    const project = this.project();
    if (!project) {
      return;
    }

    this.selectedIndicatorId = indicator.id;
    this.evidenceForm = {
      projectId: project.id,
      indicatorId: indicator.id,
      title: '',
      description: '',
      evidenceType: DoubleHighEvidenceType.ExternalLink,
      resourceId: undefined,
      attachmentUrl: '',
      externalLink: '',
      sortOrder: this.getIndicatorEvidences(indicator.id).length + 1,
    };
    this.evidenceVisible = true;
  }

  saveEvidence(): void {
    this.doubleHighService.addEvidence(this.evidenceForm).subscribe({
      next: () => {
        this.evidenceVisible = false;
        this.message.success('佐证材料已添加');
        const project = this.project();
        if (project) {
          this.load(project.id);
        }
      },
      error: () => this.message.error('佐证材料保存失败'),
    });
  }

  deleteEvidence(id: string): void {
    this.doubleHighService.deleteEvidence(id).subscribe({
      next: () => {
        this.message.success('佐证材料已删除');
        const project = this.project();
        if (project) {
          this.load(project.id);
        }
      },
      error: () => this.message.error('删除失败'),
    });
  }

  exportReport(): void {
    const project = this.project();
    if (!project) {
      return;
    }

    this.doubleHighService.exportReport(project.id).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${project.batchCode}_双高评估报表.xlsx`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => this.message.error('导出失败'),
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

  getSourceLabel(type: DoubleHighDataSourceType): string {
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

  getIndicatorEvidences(indicatorId: string) {
    return (this.project()?.evidences || []).filter(x => x.indicatorId === indicatorId);
  }
}
