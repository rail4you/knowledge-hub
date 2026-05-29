import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQueries, useQuery } from '@tanstack/react-query';
import {
  ArrowLeft, GraduationCap, BookOpen, FileText, Network,
  Award, Newspaper, Users,
} from 'lucide-react';
import { useAuth } from '../lib/auth';
import { getMicroMajorDetail, getMicroMajorResources, enrollMicroMajor, getMyMicroMajorEnrollments, getKnowledgeGraph, api } from '../lib/api';
import type { MicroMajorDetailDto, MicroMajorResourceDto } from '../lib/types';
import { KnowledgeGraph } from '../components/knowledge-graph/KnowledgeGraph';

type TabKey = 'info' | 'courses' | 'materials' | 'graph' | 'enrollment' | 'news';

const tabs: { key: TabKey; label: string; icon: typeof BookOpen }[] = [
  { key: 'info', label: '微专业信息', icon: GraduationCap },
  { key: 'courses', label: '包含课程', icon: BookOpen },
  { key: 'materials', label: '素材中心', icon: FileText },
  { key: 'graph', label: '知识图谱', icon: Network },
  { key: 'enrollment', label: '报名与证书', icon: Award },
  { key: 'news', label: '相关资讯', icon: Newspaper },
];

export function MicroMajorDetailPage() {
  const { tenantId, id } = useParams<{ tenantId: string; id: string }>();
  const auth = useAuth();
  const [activeTab, setActiveTab] = useState<TabKey>('info');
  const [enrolling, setEnrolling] = useState(false);

  const [detailQuery, resourcesQuery, enrollmentsQuery] = useQueries({
    queries: [
      { queryKey: ['micro-major', 'detail', id], queryFn: () => getMicroMajorDetail(id!), enabled: !!id },
      { queryKey: ['micro-major', 'resources', id], queryFn: () => getMicroMajorResources(id!), enabled: !!id },
      { queryKey: ['micro-major', 'my-enrollments'], queryFn: () => getMyMicroMajorEnrollments(), enabled: auth.isAuthenticated },
    ],
  });

  const detail = detailQuery.data;
  const resources = resourcesQuery.data || [];
  const enrollments = enrollmentsQuery.data || [];
  const isLoading = detailQuery.isLoading;

  const currentEnrollment = enrollments.find((e: any) => e.microMajorId === id);

  const handleEnroll = async () => {
    if (!auth.isAuthenticated) {
      alert('请先登录');
      return;
    }
    setEnrolling(true);
    try {
      await enrollMicroMajor(id!);
      enrollmentsQuery.refetch();
    } catch {
      alert('报名失败');
    } finally {
      setEnrolling(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="animate-spin rounded-full h-8 w-8 border-3 border-[#E8E8E8] border-t-[#0056D2]" />
      </div>
    );
  }

  if (!detail) {
    return (
      <div className="text-center py-16 text-[#999]">
        <GraduationCap className="h-12 w-12 mx-auto mb-3 text-[#ccc]" />
        <p className="text-sm">微专业不存在</p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-[1200px] px-4 sm:px-6 lg:px-8 py-6">
      <Link to={`/tenant/${tenantId}/micro-majors`} className="inline-flex items-center gap-1 text-sm text-[#666] hover:text-[#0056D2] mb-4">
        <ArrowLeft className="h-4 w-4" /> 返回微专业列表
      </Link>

      {/* Header */}
      <div className="rounded-lg border border-[#E8E8E8] bg-white overflow-hidden mb-6">
        <div className="flex flex-col md:flex-row">
          <div className="md:w-72 h-44 md:h-auto flex items-center justify-center shrink-0"
            style={{ background: detail.coverImageUrl ? `url(${detail.coverImageUrl}) center/cover` : 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)' }}>
            {!detail.coverImageUrl && <GraduationCap className="h-14 w-14 text-white/60" />}
          </div>
          <div className="flex-1 p-6">
            <h1 className="text-xl font-bold text-[#1a1a1a] mb-2">{detail.title}</h1>
            {detail.summary && <p className="text-sm text-[#666] mb-3">{detail.summary}</p>}
            <div className="flex flex-wrap gap-2 mb-3">
              {detail.industryField && <span className="px-2 py-0.5 rounded text-xs bg-[#E8F0FE] text-[#0056D2]">{detail.industryField}</span>}
              {detail.collaborationUnit && <span className="px-2 py-0.5 rounded text-xs bg-[#F0F0FF] text-[#722ED1]">{detail.collaborationUnit}</span>}
            </div>
            <div className="flex flex-wrap gap-x-6 gap-y-1 text-xs text-[#999]">
              <span><BookOpen className="inline h-3.5 w-3.5 mr-1" />{detail.courseCount} 门课程</span>
              <span><Users className="inline h-3.5 w-3.5 mr-1" />{detail.enrollmentCount} 人报名</span>
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

        <div className="p-6 min-h-[300px]">
          {activeTab === 'info' && (
            <div className="max-w-3xl">
              <h2 className="text-lg font-semibold mb-4">微专业介绍</h2>
              <p className="text-sm text-[#666] leading-relaxed whitespace-pre-wrap">{detail.description || '暂无详细介绍'}</p>
            </div>
          )}

          {activeTab === 'courses' && (
            <div>
              {detail.courses.length === 0 ? (
                <div className="text-center py-8 text-[#999] text-sm">暂无课程</div>
              ) : (
                <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                  {detail.courses.map(c => (
                    <Link key={c.courseId} to={`/tenant/${tenantId}/courses/${c.courseId}`}
                      className="group rounded-lg border border-[#E8E8E8] bg-white overflow-hidden hover:border-[#0056D2] hover:shadow-md transition-all">
                      <div className="aspect-[16/10] bg-gradient-to-br from-[#f0f4ff] to-[#e8f0fe] flex items-center justify-center">
                        {c.courseCoverImageUrl ? <img src={c.courseCoverImageUrl} alt="" className="h-full w-full object-cover" /> :
                        <BookOpen className="h-10 w-10 text-[#0056D2]/40" />}
                      </div>
                      <div className="p-3">
                        <h4 className="text-sm font-semibold line-clamp-2 group-hover:text-[#0056D2]">{c.courseTitle || '未命名课程'}</h4>
                        <div className="text-xs text-[#999] mt-1">{c.major || ''}{c.semester ? ` · ${c.semester}` : ''}</div>
                        {c.isCore && <span className="inline-block mt-1 px-1.5 py-0.5 rounded text-[10px] bg-[#E8F0FE] text-[#0056D2]">核心课程</span>}
                      </div>
                    </Link>
                  ))}
                </div>
              )}
            </div>
          )}

          {activeTab === 'materials' && (
            <div>
              {resources.length === 0 ? (
                <div className="text-center py-8 text-[#999] text-sm">暂无素材</div>
              ) : (
                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  {resources.map(r => (
                    <Link key={r.id} to={`/tenant/${tenantId}/resources/${r.resourceId}`}
                      className="flex items-center gap-3 p-3 rounded-lg border border-[#E8E8E8] hover:border-[#0056D2] transition-colors">
                      <FileText className="h-8 w-8 text-[#999] shrink-0" />
                      <div className="min-w-0">
                        <div className="text-sm text-[#1a1a1a] truncate">{r.resourceName}</div>
                        <div className="text-xs text-[#999]">{r.fileExtension || '文件'} · {r.downloadCount} 次下载</div>
                      </div>
                    </Link>
                  ))}
                </div>
              )}
            </div>
          )}

          {activeTab === 'graph' && (
            <MicroMajorGraphTab microMajorId={id!} />
          )}

          {activeTab === 'enrollment' && (
            <div className="max-w-md">
              {currentEnrollment ? (
                <div className="p-4 rounded-lg bg-[#F6FFED] border border-[#B7EB8F] text-sm text-[#52C41A]">
                  ✅ 已报名 · 完成率: {(currentEnrollment as any).progress || 0}%
                </div>
              ) : (
                <div>
                  <p className="text-sm text-[#666] mb-4">报名参加该微专业，系统将为您解锁全部课程和学习资源。</p>
                  <button onClick={handleEnroll} disabled={enrolling}
                    className="px-6 py-2.5 rounded-md bg-[#0056D2] text-white text-sm font-medium hover:bg-[#0041A8] disabled:opacity-50">
                    {enrolling ? '报名中...' : '立即报名'}
                  </button>
                </div>
              )}
            </div>
          )}

          {activeTab === 'news' && (
            <div className="text-center py-8 text-[#999] text-sm">暂无相关资讯</div>
          )}
        </div>
      </div>
    </div>
  );
}

function MicroMajorGraphTab({ microMajorId }: { microMajorId: string }) {
  const { data, isLoading } = useQuery({
    queryKey: ['knowledge-graph', 'micro-major', microMajorId],
    queryFn: async () => {
      const { data } = await api.get(`/api/app/knowledge-graph/micro-major/${microMajorId}`);
      return data;
    },
    enabled: !!microMajorId,
  });

  const graphData = data ? { nodes: data.nodes || [], relations: data.relations || [] } : null;

  return <KnowledgeGraph mode="micro-major" data={graphData} isLoading={isLoading} />;
}
