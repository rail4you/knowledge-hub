import { Link } from 'react-router-dom';
import { Network, ArrowRight } from 'lucide-react';

interface Props {
  tenantId: string;
}

export function MiniKnowledgeGraph({ tenantId }: Props) {
  // Static SVG representation of a simplified knowledge graph
  // Full interactive graph will be in Phase 5
  const nodePositions = [
    { cx: 50, cy: 20, r: 12, label: '课程', color: '#0056D2' },
    { cx: 20, cy: 60, r: 8, label: '知识点', color: '#52C41A' },
    { cx: 50, cy: 60, r: 8, label: '知识点', color: '#52C41A' },
    { cx: 80, cy: 60, r: 10, label: '素材', color: '#722ED1' },
    { cx: 20, cy: 90, r: 8, label: '知识点', color: '#FAAD14' },
    { cx: 50, cy: 90, r: 8, label: '知识点', color: '#52C41A' },
    { cx: 80, cy: 90, r: 8, label: '习题', color: '#EB2F96' },
  ];

  const edges = [
    { x1: 50, y1: 32, x2: 20, y2: 52, type: 'contains' },
    { x1: 50, y1: 32, x2: 50, y2: 52, type: 'contains' },
    { x1: 50, y1: 32, x2: 80, y2: 52, type: 'contains' },
    { x1: 20, y1: 68, x2: 20, y2: 82, type: 'prerequisite' },
    { x1: 50, y1: 68, x2: 50, y2: 82, type: 'prerequisite' },
    { x1: 80, y1: 68, x2: 80, y2: 82, type: 'references' },
  ];

  return (
    <section>
      <div className="flex items-end justify-between mb-6">
        <div>
          <h2 className="text-2xl font-bold text-[#1a1a1a]">知识图谱</h2>
          <p className="mt-1 text-sm text-[#999]">Knowledge graph</p>
        </div>
        <Link
          to={`/tenant/${tenantId}/micro-majors`}
          className="hidden sm:inline-flex items-center gap-1 text-sm text-[#0056D2] hover:text-[#0041A8] font-medium"
        >
          查看详情 <ArrowRight className="h-4 w-4" />
        </Link>
      </div>

      <div className="rounded-lg border border-[#E8E8E8] bg-white p-6">
        <div className="flex flex-col lg:flex-row gap-8 items-center">
          {/* Mini graph */}
          <div className="shrink-0">
            <svg viewBox="0 0 100 110" className="w-64 h-72">
              {/* Edges */}
              {edges.map((edge, i) => (
                <line
                  key={`edge-${i}`}
                  x1={edge.x1}
                  y1={edge.y1}
                  x2={edge.x2}
                  y2={edge.y2}
                  stroke={edge.type === 'prerequisite' ? '#FAAD14' : edge.type === 'references' ? '#722ED1' : '#0056D2'}
                  strokeWidth="1.5"
                  strokeDasharray={edge.type === 'prerequisite' ? '4,3' : edge.type === 'references' ? '2,2' : 'none'}
                  opacity="0.6"
                />
              ))}
              {/* Nodes */}
              {nodePositions.map((node, i) => (
                <g key={`node-${i}`}>
                  <circle
                    cx={node.cx}
                    cy={node.cy}
                    r={node.r}
                    fill={node.color}
                    fillOpacity="0.15"
                    stroke={node.color}
                    strokeWidth="1.5"
                  />
                  <text
                    x={node.cx}
                    y={node.cy + 0.5}
                    textAnchor="middle"
                    dominantBaseline="central"
                    fontSize="5"
                    fill={node.color}
                    fontWeight="600"
                  >
                    {node.label}
                  </text>
                </g>
              ))}
            </svg>
          </div>

          {/* Stats + Legend */}
          <div className="flex-1 space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="p-3 rounded-lg bg-[#F5F7FA] text-center">
                <div className="text-2xl font-bold text-[#0056D2]">48%</div>
                <div className="text-xs text-[#999] mt-1">资源覆盖率</div>
              </div>
              <div className="p-3 rounded-lg bg-[#F5F7FA] text-center">
                <div className="text-2xl font-bold text-[#1890FF]">6</div>
                <div className="text-xs text-[#999] mt-1">课程节点</div>
              </div>
            </div>

            {/* Legend */}
            <div className="flex flex-wrap gap-4 text-xs text-[#666]">
              <div className="flex items-center gap-1.5">
                <div className="w-3 h-0.5 bg-[#0056D2]" />
                包含关系
              </div>
              <div className="flex items-center gap-1.5">
                <div className="w-3 h-0.5 border-t border-dashed border-[#FAAD14]" />
                先后关系
              </div>
              <div className="flex items-center gap-1.5">
                <div className="w-3 h-0.5 border-t border-dotted border-[#722ED1]" />
                引用关系
              </div>
            </div>

            <p className="text-xs text-[#999] leading-relaxed">
              知识图谱展示微专业、课程、知识点和素材之间的关联关系，
              帮助学习者理解知识体系的整体结构。
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}
