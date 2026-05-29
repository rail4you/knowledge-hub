import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQueries, useQuery } from '@tanstack/react-query';
import {
  ArrowLeft, FileText, Download, Play,
  Info, BookOpen, Network, Sparkles,
} from 'lucide-react';
import { getResourceDetail, getResourceFileUrl, api } from '../lib/api';
import type { ResourceDetail } from '../lib/types';
import { VideoPlayer } from '../components/course/VideoPlayer';
import { KnowledgeGraph } from '../components/knowledge-graph/KnowledgeGraph';

type TabKey = 'preview' | 'info' | 'courses' | 'graph' | 'similar';

const tabs: { key: TabKey; label: string; icon: typeof FileText }[] = [
  { key: 'preview', label: '预览', icon: Play },
  { key: 'info', label: '基本信息', icon: Info },
  { key: 'courses', label: '关联课程', icon: BookOpen },
  { key: 'graph', label: '知识图谱', icon: Network },
  { key: 'similar', label: '相似推荐', icon: Sparkles },
];

const fileTypeLabels: Record<string, string> = {
  pdf: 'PDF 文档', doc: 'Word 文档', docx: 'Word 文档',
  ppt: 'PPT 演示', pptx: 'PPT 演示',
  xls: 'Excel 表格', xlsx: 'Excel 表格',
  mp4: '视频', avi: '视频', mov: '视频', wmv: '视频',
  jpg: '图片', jpeg: '图片', png: '图片', gif: '图片',
};

export function ResourceDetailPage() {
  const { tenantId, resourceId } = useParams<{ tenantId: string; resourceId: string }>();
  const [activeTab, setActiveTab] = useState<TabKey>('preview');

  const [detailQuery, fileUrlQuery] = useQueries({
    queries: [
      { queryKey: ['resource', 'detail', resourceId], queryFn: () => getResourceDetail(resourceId!), enabled: !!resourceId },
      { queryKey: ['resource', 'file-url', resourceId], queryFn: () => getResourceFileUrl(resourceId!), enabled: !!resourceId && activeTab === 'preview' },
    ],
  });

  const resource = detailQuery.data;
  const fileUrl = fileUrlQuery.data;
  const isLoading = detailQuery.isLoading;
  const ext = resource?.fileExtension?.toLowerCase() || '';
  const typeLabel = fileTypeLabels[ext] || ext.toUpperCase() || '未知类型';
  const isVideo = ['mp4', 'avi', 'mov', 'wmv', 'webm'].includes(ext);

  if (isLoading) {
    return <div className="flex items-center justify-center min-h-[400px]"><div className="animate-spin rounded-full h-8 w-8 border-3 border-[#E8E8E8] border-t-[#0056D2]" /></div>;
  }
  if (!resource) {
    return <div className="text-center py-16 text-[#999]"><FileText className="h-12 w-12 mx-auto mb-3 text-[#ccc]" /><p className="text-sm">素材不存在</p></div>;
  }

  const formatDate = (d?: string | null) => d ? new Date(d).toLocaleDateString('zh-CN') : '-';
  const formatSize = (bytes?: number | null) => {
    if (!bytes) return '-';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  return (
    <div className="mx-auto max-w-[1200px] px-4 sm:px-6 lg:px-8 py-6">
      <Link to={`/tenant/${tenantId}/resources`} className="inline-flex items-center gap-1 text-sm text-[#666] hover:text-[#0056D2] mb-4">
        <ArrowLeft className="h-4 w-4" /> 返回素材列表
      </Link>

      {/* Header */}
      <div className="bg-white rounded-lg border border-[#E8E8E8] p-6 mb-6">
        <div className="flex items-start gap-4">
          <div className={`flex h-14 w-14 shrink-0 items-center justify-center rounded-lg ${
            isVideo ? 'bg-[#722ED1]/10 text-[#722ED1]' : 'bg-[#0056D2]/10 text-[#0056D2]'
          }`}>
            {isVideo ? <Play className="h-7 w-7" /> : <FileText className="h-7 w-7" />}
          </div>
          <div className="flex-1 min-w-0">
            <h1 className="text-lg font-bold text-[#1a1a1a]">{resource.name}</h1>
            <div className="flex flex-wrap gap-3 mt-2 text-xs text-[#999]">
              <span className="px-1.5 py-0.5 rounded bg-[#F5F7FA]">{typeLabel}</span>
              {resource.categoryName && <span>{resource.categoryName}</span>}
              <span>{formatSize(resource.fileSize)}</span>
            </div>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="bg-white rounded-lg border border-[#E8E8E8] overflow-hidden">
        <div className="flex overflow-x-auto border-b border-[#E8E8E8] bg-[#F5F7FA]">
          {tabs.map(tab => {
            const Icon = tab.icon;
            return (
              <button key={tab.key} onClick={() => setActiveTab(tab.key)}
                className={`flex items-center gap-1.5 px-5 py-3 text-sm font-medium whitespace-nowrap border-b-2 -mb-[1px] transition-colors ${
                  activeTab === tab.key ? 'text-[#0056D2] border-[#0056D2] bg-white' : 'text-[#666] border-transparent hover:text-[#0056D2]'
                }`}>
                <Icon className="h-4 w-4" />{tab.label}
              </button>
            );
          })}
        </div>

        <div className="p-6 min-h-[400px]">
          {activeTab === 'preview' && (
            <div>
              {isVideo && fileUrl?.url ? (
                <VideoPlayer src={fileUrl.url} title={resource.name} />
              ) : fileUrl?.url ? (
                <div className="text-center py-8">
                  <FileText className="h-16 w-16 mx-auto mb-4 text-[#ccc]" />
                  <p className="text-sm text-[#666] mb-4">{resource.originalFileName || resource.name}</p>
                  <a href={fileUrl.url} target="_blank" rel="noopener noreferrer"
                    className="inline-flex items-center gap-2 px-5 py-2.5 rounded-md bg-[#0056D2] text-white text-sm font-medium hover:bg-[#0041A8]">
                    <Download className="h-4 w-4" /> 下载预览
                  </a>
                </div>
              ) : (
                <div className="text-center py-8 text-[#999] text-sm">暂无预览</div>
              )}
            </div>
          )}

          {activeTab === 'info' && (
            <div className="max-w-2xl">
              <h3 className="text-sm font-semibold mb-4">基本信息</h3>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <InfoRow label="文件名" value={resource.originalFileName || resource.name} />
                <InfoRow label="文件类型" value={typeLabel} />
                <InfoRow label="文件大小" value={formatSize(resource.fileSize)} />
                <InfoRow label="分类" value={resource.categoryName || '-'} />
                <InfoRow label="上传者" value={resource.creatorName || '-'} />
                <InfoRow label="上传时间" value={formatDate(resource.creationTime)} />
                <InfoRow label="浏览次数" value={resource.viewCount?.toString() || '0'} />
                <InfoRow label="下载次数" value={resource.downloadCount?.toString() || '0'} />
              </div>
              {resource.description && (
                <div className="mt-4">
                  <h3 className="text-sm font-semibold mb-2">描述</h3>
                  <p className="text-sm text-[#666]">{resource.description}</p>
                </div>
              )}
              {resource.keywords && (
                <div className="mt-4">
                  <h3 className="text-sm font-semibold mb-2">关键词</h3>
                  <div className="flex flex-wrap gap-2">
                    {resource.keywords.split(/[,，]/).map((kw, i) => (
                      <span key={i} className="px-2 py-0.5 rounded text-xs bg-[#F5F7FA] text-[#666]">{kw.trim()}</span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}

          {activeTab === 'courses' && (
            <div className="text-center py-8 text-[#999] text-sm">暂无关联课程数据</div>
          )}

          {activeTab === 'graph' && (
            <ResourceGraphTab resourceId={resourceId!} />
          )}

          {activeTab === 'similar' && (
            <div className="text-center py-8 text-[#999] text-sm">相似推荐将在后续版本中上线</div>
          )}
        </div>
      </div>
    </div>
  );
}

function ResourceGraphTab({ resourceId }: { resourceId: string }) {
  const { data, isLoading } = useQuery({
    queryKey: ['knowledge-graph', 'resource', resourceId],
    queryFn: async () => {
      const { data } = await api.get(`/api/app/knowledge-graph/resource/${resourceId}`);
      return data;
    },
    enabled: !!resourceId,
  });

  const graphData = data ? { nodes: data.nodes || [], relations: data.relations || [] } : null;
  return <KnowledgeGraph mode="resource" data={graphData} isLoading={isLoading} />;
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-xs text-[#999] mb-1">{label}</div>
      <div className="text-sm text-[#1a1a1a]">{value || '-'}</div>
    </div>
  );
}
