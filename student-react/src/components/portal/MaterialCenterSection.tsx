import { Link } from 'react-router-dom';
import { FileText, Download, ArrowRight } from 'lucide-react';

interface MaterialBrief {
  id: string;
  name: string;
  fileExtension?: string;
  downloadCount: number;
  coverUrl?: string;
}

interface Props {
  materials: MaterialBrief[];
  tenantId: string;
}

const fileTypeColors: Record<string, string> = {
  pdf: '#FF4D4F',
  doc: '#1890FF',
  docx: '#1890FF',
  ppt: '#FA8C16',
  pptx: '#FA8C16',
  xls: '#52C41A',
  xlsx: '#52C41A',
  mp4: '#722ED1',
  video: '#722ED1',
};

const fileTypeIcons: Record<string, string> = {
  pdf: 'PDF',
  doc: 'DOC',
  docx: 'DOC',
  ppt: 'PPT',
  pptx: 'PPT',
  xls: 'XLS',
  xlsx: 'XLS',
  mp4: 'VID',
  video: 'VID',
};

export function MaterialCenterSection({ materials, tenantId }: Props) {
  const displayMaterials = materials.slice(0, 8);

  return (
    <section>
      <div className="flex items-end justify-between mb-6">
        <div>
          <h2 className="text-2xl font-bold text-[#1a1a1a]">素材中心</h2>
          <p className="mt-1 text-sm text-[#999]">Material center</p>
        </div>
        <Link
          to={`/tenant/${tenantId}/resources`}
          className="hidden sm:inline-flex items-center gap-1 text-sm text-[#0056D2] hover:text-[#0041A8] font-medium"
        >
          全部素材 <ArrowRight className="h-4 w-4" />
        </Link>
      </div>

      {displayMaterials.length === 0 ? (
        <div className="text-center py-8 text-[#999] text-sm">暂无素材</div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {displayMaterials.map(material => {
            const ext = material.fileExtension?.toLowerCase() || 'unknown';
            const color = fileTypeColors[ext] || '#999';
            const label = fileTypeIcons[ext] || ext.toUpperCase().substring(0, 3);

            return (
              <Link
                key={material.id}
                to={`/tenant/${tenantId}/resources/${material.id}`}
                className="group rounded-lg border border-[#E8E8E8] bg-white overflow-hidden hover:border-[#0056D2] hover:shadow-md transition-all"
              >
                {/* Thumbnail */}
                <div className="aspect-[4/3] bg-gray-50 flex items-center justify-center relative">
                  <div
                    className="absolute top-2 left-2 px-2 py-0.5 rounded text-[10px] font-bold text-white"
                    style={{ backgroundColor: color }}
                  >
                    {label}
                  </div>
                  <FileText className="h-10 w-10 text-[#ccc] group-hover:text-[#0056D2]/40 transition-colors" />
                </div>
                {/* Info */}
                <div className="p-3">
                  <h4 className="text-sm font-medium text-[#1a1a1a] line-clamp-2 group-hover:text-[#0056D2] transition-colors">
                    {material.name}
                  </h4>
                  <div className="flex items-center gap-3 mt-2 text-xs text-[#999]">
                    {material.fileExtension && (
                      <span>文件大小: --</span>
                    )}
                    <span className="flex items-center gap-1">
                      <Download className="h-3 w-3" />
                      {material.downloadCount}
                    </span>
                  </div>
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </section>
  );
}
