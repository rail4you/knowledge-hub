import { useMemo } from 'react';
import ReactECharts from 'echarts-for-react';
import type { EChartsOption } from 'echarts';
import { Network, ZoomIn, ZoomOut, Maximize2 } from 'lucide-react';

// === Types ===
export interface GraphNode {
  id: string;
  name: string;
  type: 'micro-major' | 'course' | 'knowledge-point' | 'resource' | 'exercise';
  importanceLevel?: string;
  description?: string;
  metadata?: Record<string, any>;
}

export interface GraphEdge {
  id?: string;
  source: string;
  target: string;
  type: 'contains' | 'prerequisite' | 'parallel' | 'references' | 'corequisite' | 'related';
  weight?: number;
  description?: string;
}

export interface GraphData {
  nodes: GraphNode[];
  relations: GraphEdge[];
}

type GraphMode = 'course' | 'micro-major' | 'resource';

interface Props {
  mode: GraphMode;
  data: GraphData | null;
  isLoading?: boolean;
}

// === Node styling ===
const nodeColors: Record<string, string> = {
  'micro-major': '#0056D2',
  'course': '#1890FF',
  'knowledge-point': '#52C41A',
  'resource': '#722ED1',
  'exercise': '#EB2F96',
};

const nodeSizes: Record<string, number> = {
  'micro-major': 50,
  'course': 40,
  'knowledge-point': 30,
  'resource': 25,
  'exercise': 25,
};

const nodeImportanceSizes: Record<string, number> = {
  'high': 36,
  'normal': 30,
  'low': 24,
};

const edgeColors: Record<string, string> = {
  'contains': '#0056D2',
  'prerequisite': '#FAAD14',
  'parallel': '#52C41A',
  'references': '#722ED1',
  'corequisite': '#1890FF',
  'related': '#999999',
};

export function KnowledgeGraph({ mode, data, isLoading }: Props) {
  const option = useMemo((): EChartsOption | null => {
    if (!data || data.nodes.length === 0) return null;

    const isTreeMode = mode === 'micro-major';

    if (isTreeMode) {
      // Tree layout for micro-major
      const treeData = buildTreeData(data);
      return {
        tooltip: {
          trigger: 'item',
          formatter: (params: any) => {
            if (params.data.type === 'root') return params.name;
            return `<b>${params.name}</b><br/>类型: ${params.data.nodeType || ''}`;
          },
        },
        series: [{
          type: 'tree',
          data: [treeData],
          top: '5%',
          left: '10%',
          bottom: '5%',
          right: '15%',
          symbolSize: 10,
          orient: 'LR',
          label: { position: 'right', verticalAlign: 'middle', align: 'left', fontSize: 12, color: '#333' },
          leaves: { label: { position: 'right', verticalAlign: 'middle', align: 'left', fontSize: 11 } },
          expandAndCollapse: true,
          animationDuration: 550,
        }],
      };
    }

    // Force/Graph layout for course and resource
    const nodes = data.nodes.map(n => ({
      id: n.id,
      name: n.name,
      symbolSize: n.importanceLevel
        ? nodeImportanceSizes[n.importanceLevel] || nodeSizes[n.type] || 28
        : nodeSizes[n.type] || 28,
      itemStyle: { color: nodeColors[n.type] || '#999' },
      category: n.type,
      nodeType: n.type,
      description: n.description,
    }));

    const links = data.relations.map(r => ({
      source: r.source,
      target: r.target,
      lineStyle: {
        color: edgeColors[r.type] || '#999',
        type: r.type === 'prerequisite' ? 'dashed' : r.type === 'parallel' ? 'dotted' : 'solid',
        width: 1.5,
      },
    }));

    const categories = Object.entries(nodeColors).map(([name, color]) => ({
      name,
      itemStyle: { color },
    }));

    return {
      tooltip: {
        trigger: 'item',
        formatter: (params: any) => {
          if (params.dataType === 'edge') return '';
          return `<b>${params.name}</b>${params.data.description ? `<br/>${params.data.description}` : ''}`;
        },
      },
      legend: mode !== 'resource' ? {
        data: categories.map(c => ({ name: c.name, icon: 'circle' })),
        bottom: 0,
      } : undefined,
      series: [{
        type: 'graph',
        layout: mode === 'resource' ? 'circular' as any : 'force',
        data: nodes,
        links,
        categories,
        roam: true,
        draggable: true,
        force: {
          repulsion: mode === 'resource' ? 200 : 300,
          edgeLength: [80, 200],
          gravity: 0.1,
        },
        circular: mode === 'resource' ? { rotateLabel: true } as any : undefined,
        label: { show: true, fontSize: 11, color: '#333', formatter: (p: any) => p.name.length > 8 ? p.name.substring(0, 8) + '...' : p.name },
        emphasis: { focus: 'adjacency', lineStyle: { width: 3 } },
        lineStyle: { curveness: 0.2 },
      } as any],
    };
  }, [data, mode]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="animate-spin rounded-full h-8 w-8 border-3 border-[#E8E8E8] border-t-[#0056D2]" />
      </div>
    );
  }

  if (!data || data.nodes.length === 0) {
    return (
      <div className="text-center py-16 text-[#999]">
        <Network className="h-14 w-14 mx-auto mb-3 text-[#ccc]" />
        <p className="text-sm font-medium mb-1">暂无知识图谱数据</p>
        <p className="text-xs">请在管理端为课程/微专业构建知识图谱后查看</p>
      </div>
    );
  }

  return (
    <div className="relative">
      <ReactECharts
        option={option!}
        style={{ height: '500px', width: '100%' }}
        notMerge
        lazyUpdate
        opts={{ renderer: 'svg' }}
      />
      <div className="absolute top-2 right-2 flex items-center gap-1">
        <span className="text-xs text-[#999] bg-white/80 px-2 py-1 rounded">
          {data.nodes.length} 节点 · {data.relations.length} 关系
        </span>
      </div>
    </div>
  );
}

function buildTreeData(data: GraphData): any {
  // Build adjacency for tree structure
  const nodeMap = new Map<string, GraphNode>();
  const childrenMap = new Map<string, GraphNode[]>();

  data.nodes.forEach(n => {
    nodeMap.set(n.id, n);
    childrenMap.set(n.id, []);
  });

  data.relations.forEach(r => {
    if (r.type === 'contains') {
      const children = childrenMap.get(r.source) || [];
      const targetNode = nodeMap.get(r.target);
      if (targetNode) {
        children.push(targetNode);
        childrenMap.set(r.source, children);
      }
    }
  });

  // Find root nodes (no incoming "contains" edge)
  const childIds = new Set(data.relations.filter(r => r.type === 'contains').map(r => r.target));
  const roots = data.nodes.filter(n => !childIds.has(n.id));

  const buildNode = (n: GraphNode): any => ({
    name: n.name,
    nodeType: n.type,
    itemStyle: { color: nodeColors[n.type] || '#999' },
    children: (childrenMap.get(n.id) || []).map(buildNode),
  });

  if (roots.length === 0) return { name: '知识图谱', children: data.nodes.map(buildNode) };
  if (roots.length === 1) return buildNode(roots[0]);
  return { name: '知识图谱', children: roots.map(buildNode) };
}
