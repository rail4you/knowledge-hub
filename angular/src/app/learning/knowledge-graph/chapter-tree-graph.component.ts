import { Component, Input, signal, ElementRef, viewChild, AfterViewInit, OnChanges, SimpleChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import * as echarts from 'echarts';

interface KnowledgeResourceDto {
  id: string;
  name: string;
  description?: string;
  importanceLevel?: string;
  difficulty?: number;
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
  selector: 'app-chapter-tree-graph',
  standalone: true,
  imports: [CommonModule, NzCardModule, NzButtonModule, NzIconModule, NzTagModule],
  template: `
    <div class="tree-graph-container">
      <div class="chart-wrapper">
        <div #chartContainer class="chart-container"></div>
      </div>
      @if (selectedNode()) {
        <div class="node-detail">
          <nz-card nzTitle="章节详情" [nzSize]="'small'">
            <p><strong>标题：</strong>{{ selectedNode()?.title }}</p>
            @if (selectedNode()?.description) {
              <p><strong>描述：</strong>{{ selectedNode()?.description }}</p>
            }
            @if (selectedNode()?.knowledgeResources?.length) {
              <p><strong>知识点：</strong></p>
              <nz-tag *ngFor="let kr of selectedNode()?.knowledgeResources" [nzColor]="'blue'">
                {{ kr.name }}
              </nz-tag>
            }
          </nz-card>
        </div>
      }
    </div>
  `,
  styles: [`
    .tree-graph-container {
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
export class ChapterTreeGraphComponent implements AfterViewInit, OnChanges {
  @Input() chapters: ChapterDto[] = [];
  
  private readonly chartContainer = viewChild<ElementRef>('chartContainer');
  private chart: echarts.ECharts | null = null;
  selectedNode = signal<ChapterDto | null>(null);
  
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
    
    const treeData = this.buildTreeData(this.chapters);
    
    const option: echarts.EChartsOption = {
      tooltip: {
        trigger: 'item',
        triggerOn: 'mousemove',
        formatter: (params: any) => {
          const data = params.data;
          return `${data.name}`;
        }
      },
      series: [{
        type: 'tree',
        name: '章节结构',
        data: treeData,
        top: '5%',
        left: '10%',
        bottom: '5%',
        right: '20%',
        symbol: 'rectangle',
        symbolSize: [140, 40],
        orient: 'LR',
        initialTreeDepth: 2,
        label: {
          position: 'inside',
          formatter: '{b}',
          fontSize: 12,
          color: '#fff'
        },
        leaves: {
          label: {
            position: 'inside',
            formatter: '{b}',
            fontSize: 11,
            color: '#fff'
          }
        },
        emphasis: {
          focus: 'ancestor'
        },
        lineStyle: {
          color: '#1890ff',
          width: 2,
          curveness: 0
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
  
  private buildTreeData(chapters: ChapterDto[]): any[] {
    if (!chapters || chapters.length === 0) {
      return [{ name: '暂无章节', itemStyle: { color: '#999' } }];
    }
    
    const processChapter = (chapter: ChapterDto): any => {
      const node: any = {
        name: chapter.title || '未命名章节',
        extData: chapter,
        itemStyle: this.getNodeStyle(chapter)
      };
      
      if (chapter.children && chapter.children.length > 0) {
        node.children = chapter.children.map(c => processChapter(c));
      } else if (chapter.knowledgeResources && chapter.knowledgeResources.length > 0) {
        node.children = chapter.knowledgeResources.map((kr, idx) => ({
          name: kr.name,
          extData: { ...kr, title: kr.name },
          itemStyle: {
            color: this.getKnowledgeColor(kr)
          },
          label: {
            fontSize: 10,
            color: '#fff'
          }
        }));
      }
      
      return node;
    };
    
    return chapters.sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0))
      .map(c => processChapter(c));
  }
  
  private getNodeStyle(chapter: ChapterDto): any {
    return {
      color: chapter.parentId ? '#73d13d' : '#1890ff',
      borderRadius: 4
    };
  }
  
  private getKnowledgeColor(kr: KnowledgeResourceDto): string {
    const difficulty = kr.difficulty || 1;
    const colors = ['#52c41a', '#73d13d', '#faad14', '#ff7a45', '#f5222d'];
    return colors[Math.min(difficulty - 1, 4)] || '#1890ff';
  }
}
