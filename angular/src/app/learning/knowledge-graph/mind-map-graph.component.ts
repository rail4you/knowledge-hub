import { Component, Input, signal, ElementRef, viewChild, AfterViewInit, OnChanges, SimpleChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTagModule } from 'ng-zorro-antd/tag';
import * as echarts from 'echarts/core';
import { TreeChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { TooltipComponent } from 'echarts/components';

echarts.use([TreeChart, CanvasRenderer, TooltipComponent]);

interface KnowledgeResourceDto {
  id: string;
  name: string;
  description?: string;
  importanceLevel?: string;
  difficulty?: number;
  sortOrder?: number;
}

interface ChapterDto {
  id: string;
  courseId?: string;
  parentId?: string;
  title?: string;
  description?: string;
  sortOrder?: number;
  knowledgeResources?: KnowledgeResourceDto[];
  children?: ChapterDto[];
}

@Component({
  selector: 'app-mind-map-graph',
  standalone: true,
  imports: [CommonModule, NzCardModule, NzTagModule],
  template: `
    <div class="mindmap-container">
      <div class="chart-wrapper">
        <div #chartContainer class="chart-container"></div>
      </div>
      @if (selectedNode()) {
        <div class="node-detail">
          <nz-card nzTitle="知识点详情" [nzSize]="'small'">
            <p><strong>名称：</strong>{{ selectedNode()?.name }}</p>
            @if (selectedNode()?.description) {
              <p><strong>描述：</strong>{{ selectedNode()?.description }}</p>
            }
            @if (selectedNode()?.difficulty) {
              <p><strong>难度：</strong>
                <nz-tag [nzColor]="getDifficultyColor(selectedNode()?.difficulty || 1)">
                  {{ getDifficultyLabel(selectedNode()?.difficulty || 1) }}
                </nz-tag>
              </p>
            }
            @if (selectedNode()?.importanceLevel) {
              <p><strong>重要程度：</strong>
                <nz-tag [nzColor]="'blue'">{{ selectedNode()?.importanceLevel }}</nz-tag>
              </p>
            }
          </nz-card>
        </div>
      }
    </div>
  `,
  styles: [`
    .mindmap-container {
      display: flex;
      gap: 16px;
      height: 600px;
    }
    .chart-wrapper {
      flex: 1;
      min-width: 0;
    }
    .chart-container {
      width: 100%;
      height: 100%;
      min-height: 500px;
    }
    .node-detail {
      width: 280px;
      flex-shrink: 0;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MindMapGraphComponent implements AfterViewInit, OnChanges {
  @Input() chapters: ChapterDto[] = [];
  
  private readonly chartContainer = viewChild<ElementRef>('chartContainer');
  private chart: echarts.ECharts | null = null;
  selectedNode = signal<any>(null);
  
  ngAfterViewInit() {
    this.initChart();
  }
  
  ngOnChanges(changes: SimpleChanges) {
    if (changes['chapters'] && this.chart) {
      this.updateChart();
    }
  }
  
  private initChart() {
    const container = this.chartContainer()?.nativeElement;
    if (!container) return;
    
    this.chart = echarts.init(container);
    this.updateChart();
    
    this.chart.on('click', (params: any) => {
      if (params.dataType === 'node') {
        const nodeData = params.data as any;
        if (nodeData.extData) {
          this.selectedNode.set(nodeData.extData);
        }
      }
    });
    
    window.addEventListener('resize', () => {
      this.chart?.resize();
    });
  }
  
  private updateChart() {
    if (!this.chart) return;
    
    const mindMapData = this.buildMindMapData();
    
    const option: echarts.EChartsCoreOption = {
      tooltip: {
        trigger: 'item',
        triggerOn: 'mousemove'
      },
      series: [{
        type: 'tree',
        name: '思维导图',
        data: [mindMapData],
        top: '10%',
        left: '50%',
        bottom: '10%',
        right: '20%',
        symbol: 'circle',
        symbolSize: (value: any, params: any) => {
          const data = params.data as any;
          const depth = data.depth || 0;
          if (depth === 0) return 80;
          if (depth === 1) return 60;
          return 40;
        },
        orient: 'RL',
        initialTreeDepth: 3,
        label: {
          position: 'right',
          formatter: '{b}',
          fontSize: 12,
          color: '#333'
        },
        labelLayout: {
          draggable: true
        },
        emphasis: {
          focus: 'ancestor'
        },
        lineStyle: {
          color: '#91caff',
          width: 2,
          curveness: 0.5
        },
        itemStyle: {
          color: '#1890ff',
          borderWidth: 0,
          borderRadius: 4
        },
        expandAndCollapse: true,
        animationDurationUpdate: 750
      }]
    };
    
    this.chart.setOption(option);
  }
  
  private buildMindMapData(): any {
    const courseTitle = '课程中心';
    
    const children: any[] = [];
    
    this.chapters.forEach((chapter, chapterIdx) => {
      const chapterChild: any = {
        name: chapter.title || `第${chapterIdx + 1}章`,
        extData: { name: chapter.title, description: chapter.description },
        itemStyle: { color: '#73d13d' }
      };
      
      if (chapter.knowledgeResources && chapter.knowledgeResources.length > 0) {
        chapterChild.children = chapter.knowledgeResources
          .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0))
          .map((kr, idx) => ({
            name: kr.name,
            extData: kr,
            itemStyle: {
              color: this.getKnowledgeColor(kr.difficulty || 1)
            }
          }));
      }
      
      if (chapter.children && chapter.children.length > 0) {
        if (!chapterChild.children) chapterChild.children = [];
        chapter.children.forEach(child => {
          chapterChild.children.push({
            name: child.title || '子章节',
            extData: { name: child.title, description: child.description },
            itemStyle: { color: '#95de64' }
          });
        });
      }
      
      children.push(chapterChild);
    });
    
    return {
      name: courseTitle,
      extData: { name: courseTitle },
      itemStyle: { color: '#1890ff' },
      children
    };
  }
  
  private getKnowledgeColor(difficulty: number): string {
    const colors = ['#52c41a', '#73d13d', '#faad14', '#ff7a45', '#f5222d'];
    return colors[Math.min(difficulty - 1, 4)] || '#95de64';
  }
  
  getDifficultyColor(difficulty: number): string {
    const colors = ['green', 'lime', 'gold', 'orange', 'red'];
    return colors[Math.min(difficulty - 1, 4)] || 'default';
  }
  
  getDifficultyLabel(difficulty: number): string {
    const labels = ['入门', '初级', '中级', '高级', '专家'];
    return labels[Math.min(difficulty - 1, 4)] || '未知';
  }
}
