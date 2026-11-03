import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { LearningAnalyticsService } from '../../learning-analytics/services/learning-analytics.service';
import {
  LearningAnalyticsWorkbench,
  LearningAnalyticsWorkbenchTask,
} from '../../learning-analytics/models/learning-analytics.models';

/** 闭环环节卡片 */
interface AnalysisStep {
  no: string;
  title: string;
  desc: string;
  url: string;
  primary: string;
  secondary: string;
  warn?: string;
  tone: 'teal' | 'blue' | 'violet' | 'green';
}

/**
 * 智学分析工作台：把四个环节串成一条链
 * ① 知识点治理 → ② 课程联动设置 → ③ 试卷采集与校对 → ④ 分析报告与推送
 */
@Component({
  selector: 'app-learning-analysis-workbench',
  standalone: true,
  imports: [
    CommonModule,
    NzButtonModule,
    NzIconModule,
    NzProgressModule,
    NzSpinModule,
  ],
  templateUrl: './learning-analysis-workbench.component.html',
  styleUrls: ['./learning-analysis-workbench.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LearningAnalysisWorkbenchComponent implements OnInit {
  private readonly service = inject(LearningAnalyticsService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly data = signal<LearningAnalyticsWorkbench | null>(null);

  readonly steps = computed<AnalysisStep[]>(() => {
    const d = this.data();
    if (!d) {
      return [];
    }

    return [
      {
        no: '①',
        title: '知识点治理',
        desc: '知识点归一化闭环（确认灰区 / 合并 / 拆分）与习题关联；OCR 匹配与掌握率统计都依赖它。',
        url: '/learning/knowledge-points',
        primary: `${d.knowledgePointTotal} 个知识点`,
        secondary: `已确认 ${d.knowledgePointConfirmed} · 习题关联 ${d.exerciseLinkTotal} · 覆盖 ${d.knowledgePointCourseCount} 门课`,
        warn: d.knowledgePointPending > 0 ? `${d.knowledgePointPending} 个灰区待确认` : undefined,
        tone: 'teal',
      },
      {
        no: '②',
        title: '课程联动设置',
        desc: '配置课程前后关系并推送前置课程复习包，是「加强前置课程」教学诊断建议的落地依据。',
        url: '/learning/analysis/linkage',
        primary: `${d.courseLinkTotal} 条前后关系`,
        secondary: `涉及 ${d.courseLinkCourseCount} 门课 · 已推复习包 ${d.reviewPackageTotal}`,
        warn: d.courseLinkTotal === 0 ? '尚未配置课程联动' : undefined,
        tone: 'blue',
      },
      {
        no: '③',
        title: '试卷采集与校对',
        desc: '上传纸质试卷 → OCR 识别 → 人工校对 → 推送，是分析数据的采集入口。',
        url: '/learning/analysis/paper',
        primary: `${d.batchTotal} 个批次 / ${d.scriptTotal} 份`,
        secondary: `平均分 ${d.averageScore} · 已推送批次 ${d.pushedBatchCount}`,
        warn:
          d.pendingConfirmCount > 0
            ? `${d.pendingConfirmCount} 题待校对`
            : d.runningBatchCount > 0
              ? `${d.runningBatchCount} 个批次识别中`
              : undefined,
        tone: 'violet',
      },
      {
        no: '④',
        title: '分析报告与推送',
        desc: '基于已确认批次产出班级薄弱点、教学诊断与学生报告，可推送、导出与留档。',
        url: '/learning/analysis/report',
        primary: `${d.analyzedBatchCount} 个批次已分析`,
        secondary: `已推送 ${d.pushedStudentCount} 份学生报告 · 留档 ${d.reportFileTotal} 个文件`,
        warn: d.pushedStudentCount === 0 && d.analyzedBatchCount > 0 ? '分析完成但尚未推送' : undefined,
        tone: 'green',
      },
    ];
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.service.getWorkbench().subscribe({
      next: data => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('工作台数据加载失败');
      },
    });
  }

  open(url: string): void {
    void this.router.navigate([url]);
  }

  formatTime(value?: string): string {
    if (!value) {
      return '';
    }
    const date = new Date(value);
    if (isNaN(date.getTime())) {
      return value;
    }
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${date.getMonth() + 1}-${pad(date.getDate())}`;
  }

  trackTask(_: number, item: LearningAnalyticsWorkbenchTask): string {
    return item.id;
  }
}
