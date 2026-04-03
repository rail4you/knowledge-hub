import { Component, Input, signal, ElementRef, viewChild, AfterViewInit, OnChanges, SimpleChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import * as echarts from 'echarts/core';
import { GraphChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { TooltipComponent, LegendComponent } from 'echarts/components';

echarts.use([GraphChart, CanvasRenderer, TooltipComponent, LegendComponent]);

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

interface NetworkNode {
  id: string;
  name: string;
  symbolSize: number;
  category: number;
  difficulty?: number;
  importance?: string;
  chapterTitle?: string;
}

interface NetworkEdge {
  source: string;
  target: string;
  lineStyle?: {
    width?: number;
    color?: string;
  };
}

@Component({
  selector: 'app-knowledge-network-graph',
  standalone: true,
  imports: [CommonModule, NzCardModule, NzTagModule, NzBadgeModule],
  template: `
    <div class="network-container">
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
            <p><strong>难度：</strong>
              <nz-tag [nzColor]="getDifficultyColor(selectedNode()?.difficulty || 1)">
                {{ getDifficultyLabel(selectedNode()?.difficulty || 1) }}
              </nz-tag>
            </p>
            <p><strong>重要程度：</strong>
              <nz-tag nzColor="blue">{{ selectedNode()?.importance || '未设置' }}</nz-tag>
            </p>
            <p><strong>所属章节：</strong>{{ selectedNode()?.chapterTitle }}</p>
          </nz-card>
          <nz-card nzTitle="关联知识点" [nzSize]="'small'" class="related-nodes">
            @if (relatedNodes().length > 0) {
              <nz-tag *ngFor="let node of relatedNodes()" nzColor="green">{{ node }}</nz-tag>
            } @else {
              <p class="no-related">暂无关联</p>
            }
          </nz-card>
        </div>
      }
      <div class="legend">
        <div class="legend-title">难度图例</div>
        <div class="legend-item"><span class="dot easy"></span>入门</div>
        <div class="legend-item"><span class="dot medium"></span>初级</div>
        <div class="legend-item"><span class="dot hard"></span>中级</div>
        <div class="legend-item"><span class="dot harder"></span>高级</div>
        <div class="legend-item"><span class="dot hardest"></span>专家</div>
      </div>
    </div>
  `,
  styles: [`
    .network-container {
      display: flex;
      gap: 16px;
      height: 600px;
      position: relative;
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
    .related-nodes {
      margin-top: 12px;
    }
    .no-related {
      color: #999;
      font-size: 12px;
    }
    .legend {
      position: absolute;
      bottom: 20px;
      left: 20px;
      background: rgba(255,255,255,0.95);
      padding: 12px;
      border-radius: 4px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.15);
    }
    .legend-title {
      font-weight: bold;
      margin-bottom: 8px;
      font-size: 12px;
    }
    .legend-item {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 11px;
      margin-bottom: 4px;
    }
    .dot {
      width: 12px;
      height: 12px;
      border-radius: 50%;
      display: inline-block;
    }
    .dot.easy { background: #52c41a; }
    .dot.medium { background: #73d13d; }
    .dot.hard { background: #faad14; }
    .dot.harder { background: #ff7a45; }
    .dot.hardest { background: #f5222d; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KnowledgeNetworkGraphComponent implements AfterViewInit, OnChanges {
  @Input() chapters: ChapterDto[] = [];
  
  private readonly chartContainer = viewChild<ElementRef>('chartContainer');
  private chart: echarts.ECharts | null = null;
  selectedNode = signal<any>(null);
  relatedNodes = signal<string[]>([]);
  private nodeMap = new Map<string, any>();
  
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
        if (nodeData.id) {
          this.selectedNode.set(nodeData);
          this.findRelatedNodes(nodeData.id);
        }
      }
    });
    
    window.addEventListener('resize', () => {
      this.chart?.resize();
    });
  }
  
  private updateChart() {
    if (!this.chart) return;
    
    const { nodes, edges } = this.buildNetworkData();
    
    const option: echarts.EChartsCoreOption = {
      tooltip: {
        trigger: 'item',
        triggerOn: 'mousemove',
        formatter: (params: any) => {
          if (params.dataType === 'node') {
            const data = params.data as any;
            return `${data.name}`;
          }
          return '';
        }
      },
      legend: {
        data: ['章节节点', '知识点'],
        top: 10,
        left: 10
      },
      series: [{
        type: 'graph',
        name: '知识网络',
        layout: 'force',
        legendHoverLink: true,
        force: {
          repulsion: 150,
          gravity: 0.1,
          edgeLength: [50, 150],
          layoutAnimation: true
        },
        nodeScaleRatio: 0.6,
        draggable: true,
        roam: true,
        symbol: 'circle',
        symbolSize: (value: any, params: any) => {
          const data = params.data as any;
          return data.symbolSize || 30;
        },
        categories: [
          { name: '章节节点', itemStyle: { color: '#1890ff' } },
          { name: '知识点', itemStyle: { color: '#73d13d' } }
        ],
        label: {
          show: true,
          position: 'inside',
          formatter: '{b}',
          fontSize: 10,
          color: '#fff'
        },
        emphasis: {
          focus: 'adjacency',
          lineStyle: {
            width: 3
          }
        },
        lineStyle: {
          color: '#91caff',
          width: 1,
          curveness: 0.2
        },
        edgeSymbol: ['circle', 'arrow'],
        edgeSymbolSize: [4, 8],
        data: nodes,
        links: edges
      }]
    };
    
    this.chart.setOption(option);
  }
  
  private buildNetworkData(): { nodes: NetworkNode[]; edges: NetworkEdge[] } {
    const nodes: NetworkNode[] = [];
    const edges: NetworkEdge[] = [];
    this.nodeMap.clear();
    
    if (!this.chapters || this.chapters.length === 0) {
      return { nodes, edges };
    }
    
    let nodeId = 0;
    
    this.chapters.forEach((chapter, chapterIdx) => {
      const chapterNodeId = `chapter_${nodeId++}`;
      const chapterNode: NetworkNode = {
        id: chapterNodeId,
        name: chapter.title || `第${chapterIdx + 1}章`,
        symbolSize: 50,
        category: 0,
        difficulty: 1,
        chapterTitle: chapter.title
      };
      nodes.push(chapterNode);
      this.nodeMap.set(chapterNodeId, { ...chapterNode, description: chapter.description });
      
      if (chapter.knowledgeResources && chapter.knowledgeResources.length > 0) {
        chapter.knowledgeResources.forEach((kr) => {
          const krNodeId = `kr_${nodeId++}`;
          const krNode: NetworkNode = {
            id: krNodeId,
            name: kr.name,
            symbolSize: 20 + (kr.difficulty || 1) * 8,
            category: 1,
            difficulty: kr.difficulty || 1,
            importance: kr.importanceLevel,
            chapterTitle: chapter.title
          };
          nodes.push(krNode);
          this.nodeMap.set(krNodeId, { ...krNode, description: kr.description });
          
          edges.push({
            source: chapterNodeId,
            target: krNodeId,
            lineStyle: {
              width: 1,
              color: '#91caff'
            }
          });
          
          const krIndex = chapter.knowledgeResources.indexOf(kr);
          if (krIndex > 0) {
            const prevKr = chapter.knowledgeResources[krIndex - 1];
            const prevKrNodeId = `kr_${nodeId - (chapter.knowledgeResources.length - krIndex)}`;
            if (prevKrNodeId !== krNodeId) {
              edges.push({
                source: prevKrNodeId,
                target: krNodeId,
                lineStyle: {
                  width: 0.5,
                  color: '#d9d9d9'
                }
              });
            }
          }
        });
      }
      
      if (chapter.children && chapter.children.length > 0) {
        chapter.children.forEach(child => {
          const childNodeId = `chapter_child_${nodeId++}`;
          const childNode: NetworkNode = {
            id: childNodeId,
            name: child.title || '子章节',
            symbolSize: 40,
            category: 0,
            difficulty: 1,
            chapterTitle: chapter.title
          };
          nodes.push(childNode);
          this.nodeMap.set(childNodeId, { ...childNode, description: child.description });
          
          edges.push({
            source: chapterNodeId,
            target: childNodeId,
            lineStyle: {
              width: 2,
              color: '#69c0ff'
            }
          });
        });
      }
    });
    
    return { nodes, edges };
  }
  
  private findRelatedNodes(nodeId: string) {
    const related: string[] = [];
    const option = this.chart?.getOption() as any;
    if (!option?.series?.[0]?.links) return;
    
    option.series[0].links.forEach((link: any) => {
      if (link.source === nodeId) {
        const targetNode = this.nodeMap.get(link.target);
        if (targetNode) related.push(targetNode.name);
      }
      if (link.target === nodeId) {
        const sourceNode = this.nodeMap.get(link.source);
        if (sourceNode) related.push(sourceNode.name);
      }
    });
    
    this.relatedNodes.set(related);
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
