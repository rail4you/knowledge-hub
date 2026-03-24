import { Component, signal, inject, OnInit, ChangeDetectionStrategy, viewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzListModule } from 'ng-zorro-antd/list';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzTagModule } from 'ng-zorro-antd/tag';
import * as echarts from 'echarts';
import type { EChartsOption } from 'echarts/index';

interface LearningDashboard {
  totalCourses: number;
  completedCourses: number;
  inProgressCourses: number;
  notStartedCourses: number;
  totalLearningTime: number;
  averageProgress: number;
  dailyTimeLabels: string[];
  dailyTimeValues: number[];
  knowledgeDimensions: { name: string }[];
  masteryValues: number[];
  recentLearning: {
    courseId: string;
    courseName: string;
    progress: number;
    lastAccessAt: Date;
  }[];
}

@Component({
  selector: 'app-progress-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    NzCardModule,
    NzButtonModule,
    NzProgressModule,
    NzStatisticModule,
    NzSpinModule,
    NzIconModule,
    NzListModule,
    NzGridModule,
    NzAvatarModule,
    NzTagModule
  ],
  templateUrl: './progress-dashboard.component.html',
  styleUrls: ['./progress-dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProgressDashboardComponent implements OnInit {
  private readonly router = inject(Router);
  
  dashboard = signal<LearningDashboard | null>(null);
  loading = signal(true);
  
  private readonly chartContainer = viewChild<ElementRef>('chartContainer');
  private chart: echarts.ECharts | null = null;
  
  ngOnInit() {
    this.loadDashboard();
  }
  
  loadDashboard() {
    this.loading.set(true);
    setTimeout(() => {
      this.dashboard.set({
        totalCourses: 5,
        completedCourses: 1,
        inProgressCourses: 3,
        notStartedCourses: 1,
        totalLearningTime: 45.5,
        averageProgress: 42,
        dailyTimeLabels: ['周一', '周二', '周三', '周四', '周五', '周六', '周日'],
        dailyTimeValues: [2.5, 1.8, 3.2, 2.1, 4.5, 3.8, 2.2],
        knowledgeDimensions: [
          { name: '基础知识' },
          { name: '应用能力' },
          { name: '创新能力' },
          { name: '分析能力' },
          { name: '综合能力' }
        ],
        masteryValues: [60, 50, 40, 70, 55],
        recentLearning: [
          { courseId: '1', courseName: 'Python 编程基础', progress: 35, lastAccessAt: new Date() },
          { courseId: '2', courseName: '机器学习导论', progress: 60, lastAccessAt: new Date() }
        ]
      });
      this.loading.set(false);
      setTimeout(() => this.initChart(), 100);
    }, 500);
  }
  
  private initChart() {
    const container = this.chartContainer()?.nativeElement;
    if (!container) return;
    
    this.chart = echarts.init(container);
    
    const dashboard = this.dashboard();
    if (!dashboard) return;
    
    this.chart.setOption({
      tooltip: { trigger: 'axis' },
      xAxis: {
        type: 'category',
        data: dashboard.dailyTimeLabels
      },
      yAxis: { type: 'value', name: '小时' },
      series: [{
        name: '学习时长',
        type: 'bar',
        data: dashboard.dailyTimeValues,
        itemStyle: { color: '#1890ff' }
      }]
    });
  }
  
  continueLearning(courseId: string) {
    this.router.navigate(['/learning/courses', courseId]);
  }
}
