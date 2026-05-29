import { useState, useEffect } from 'react';
import { useParams, useSearchParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  Search, FileText, Play, Download, Star, Eye, Heart, Grid3X3, List,
  Filter, X, ChevronRight, ChevronDown, History, BookOpen, Inbox,
} from 'lucide-react';
import DOMPurify from 'dompurify';
import { useAuth } from '../lib/auth';
import { api, setTenantId } from '../lib/api';
import { downloadResourceFile } from '../lib/file-api';
import { FilePreviewModal } from '../components/FilePreviewModal';
import type { Resource } from '../lib/types';

// ===== Types =====
interface ResourceCategory {
  id: string; name: string; parentId?: string | null; children?: ResourceCategory[];
}
interface SearchResultItem {
  id: string; resourceId: string; resourceName: string; pageNumber: number;
  content: string; highlightedContent?: string | null; title?: string | null;
  relevanceScore: number; fileExtension: string; resourceType: number;
  categoryName?: string; uploadDate: string; sourceType?: 'document' | 'video';
}
interface SearchHistoryItem {
  id: string; queryText: string; creationTime: string; resultCount: number;
}

type TabKey = 'resources' | 'favorites' | 'search' | 'history';
const tabs: { key: TabKey; label: string; icon: typeof BookOpen }[] = [
  { key: 'resources', label: '资源库', icon: BookOpen },
  { key: 'favorites', label: '我的收藏', icon: Star },
  { key: 'search', label: '资源搜索', icon: Search },
  { key: 'history', label: '搜索历史', icon: History },
];

const typeTabs = [
  { key: null, label: '全部' }, { key: 0, label: '文档' }, { key: 1, label: '视频' }, { key: 2, label: '图片' }, { key: 3, label: '其他' },
];
const PAGE_SIZE = 12;

const getFileTypeInfo = (ext?: string | null) => {
  const map: Record<string, { color: string; label: string }> = {
    '.pdf': { color: '#FF4D4F', label: 'PDF' }, '.doc': { color: '#1890FF', label: 'DOC' }, '.docx': { color: '#1890FF', label: 'DOC' },
    '.ppt': { color: '#FA8C16', label: 'PPT' }, '.pptx': { color: '#FA8C16', label: 'PPT' },
    '.xls': { color: '#52C41A', label: 'XLS' }, '.xlsx': { color: '#52C41A', label: 'XLS' },
    '.mp4': { color: '#722ED1', label: 'VID' }, '.jpg': { color: '#EB2F96', label: 'IMG' }, '.png': { color: '#EB2F96', label: 'IMG' },
  };
  return map[ext?.toLowerCase() || ''] || { color: '#999', label: (ext || '?').toUpperCase().substring(0, 3) };
};
const formatSize = (bytes?: number | null) => {
  if (!bytes) return ''; if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`; return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
};
const formatDate = (d?: string | null) => d ? new Date(d).toLocaleDateString('zh-CN') : '';

// ===== Main Page =====
export function ResourceListPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const auth = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const [activeTab, setActiveTab] = useState<TabKey>(
    (searchParams.get('tab') as TabKey) || 'resources'
  );

  useEffect(() => { if (tenantId) setTenantId(tenantId); }, [tenantId]);

  const handleTabChange = (key: TabKey) => {
    setActiveTab(key);
    setSearchParams({ tab: key }, { replace: true });
  };

  return (
    <div className="mx-auto max-w-[1400px] px-4 sm:px-6 lg:px-8 py-6">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-[#1a1a1a]">资源中心</h1>
        <p className="text-sm text-[#999] mt-1">浏览、搜索、收藏教学资源</p>
      </div>

      {/* Tab bar */}
      <div className="flex overflow-x-auto bg-white rounded-lg border border-[#E8E8E8] mb-6">
        {tabs.map(tab => {
          const Icon = tab.icon;
          return (
            <button key={tab.key} onClick={() => handleTabChange(tab.key)}
              className={`flex items-center gap-1.5 px-5 py-3 text-sm font-medium whitespace-nowrap border-b-2 -mb-[1px] transition-colors ${
                activeTab === tab.key ? 'text-[#0056D2] border-[#0056D2]' : 'text-[#666] border-transparent hover:text-[#0056D2]'
              }`}>
              <Icon className="h-4 w-4" />{tab.label}
            </button>
          );
        })}
      </div>

      {/* Tab content */}
      <div style={{ display: activeTab === 'resources' ? 'block' : 'none' }}>
        <ResourcesPanel tenantId={tenantId!} />
      </div>
      <div style={{ display: activeTab === 'favorites' ? 'block' : 'none' }}>
        {auth.isAuthenticated ? <FavoritesPanel /> : <LoginPrompt />}
      </div>
      <div style={{ display: activeTab === 'search' ? 'block' : 'none' }}>
        <SearchPanel />
      </div>
      <div style={{ display: activeTab === 'history' ? 'block' : 'none' }}>
        {auth.isAuthenticated ? <HistoryPanel /> : <LoginPrompt />}
      </div>
    </div>
  );
}

function LoginPrompt() {
  return (
    <div className="text-center py-16 text-[#999] bg-white rounded-lg border border-[#E8E8E8]">
      <Star className="h-12 w-12 mx-auto mb-3 text-[#ccc]" />
      <p className="text-sm font-medium mb-2">请先登录</p>
      <p className="text-xs">登录后可使用收藏和历史功能</p>
    </div>
  );
}

// ===== Resources Panel =====
function ResourcesPanel({ tenantId }: { tenantId: string }) {
  const auth = useAuth();
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [selectedType, setSelectedType] = useState<number | null>(null);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);
  const [page, setPage] = useState(0);
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');
  const [categories, setCategories] = useState<ResourceCategory[]>([]);
  const [expandedCategories, setExpandedCategories] = useState<Set<string>>(new Set());
  const [showFilters, setShowFilters] = useState(false);
  const [collectedIds, setCollectedIds] = useState<Record<string, boolean>>({});
  const [previewResource, setPreviewResource] = useState<Resource | null>(null);

  useEffect(() => { api.get('/api/app/resource/categories').then(res => setCategories(res.data || [])).catch(() => {}); }, []);
  useEffect(() => { const t = setTimeout(() => { setDebouncedSearch(search); setPage(0); }, 300); return () => clearTimeout(t); }, [search]);

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['resources', tenantId, debouncedSearch, selectedType, selectedCategoryId, page],
    queryFn: async () => {
      const params: Record<string, any> = { skipCount: page * PAGE_SIZE, maxResultCount: PAGE_SIZE, status: 3 };
      if (debouncedSearch) params.filter = debouncedSearch;
      if (selectedType !== null) params.resourceType = selectedType;
      if (selectedCategoryId) params.categoryId = selectedCategoryId;
      const { data } = await api.get('/api/app/resource/filtered-list', { params });
      (data.items || []).forEach((r: Resource) => {
        if (!r.id || !auth.isAuthenticated) return;
        api.post(`/api/app/resource/is-collected/${r.id}`).then(res => setCollectedIds(prev => ({ ...prev, [r.id!]: res.data }))).catch(() => {});
      });
      return data;
    },
    enabled: !!tenantId,
  });

  const resources: Resource[] = data?.items || [];
  const totalCount = data?.totalCount || 0;
  const totalPages = Math.ceil(totalCount / PAGE_SIZE);

  const toggleCollection = (r: Resource) => {
    if (!r.id || !auth.isAuthenticated) return;
    const url = collectedIds[r.id] ? `/api/app/resource/uncollect/${r.id}` : `/api/app/resource/collect/${r.id}`;
    api.post(url).then(() => setCollectedIds(prev => ({ ...prev, [r.id!]: !collectedIds[r.id!] }))).catch(() => {});
  };
  const handleDownload = async (r: Resource) => { if (r.id) try { await downloadResourceFile(r.id, r.originalFileName || r.name || 'download'); } catch {} };
  const toggleCategory = (id: string) => { const n = new Set(expandedCategories); n.has(id) ? n.delete(id) : n.add(id); setExpandedCategories(n); };

  const renderCategories = (cats: ResourceCategory[], depth = 0): any => cats.map(cat => {
    const hasKids = cat.children && cat.children.length > 0;
    const expanded = expandedCategories.has(cat.id);
    const active = selectedCategoryId === cat.id;
    return (
      <div key={cat.id}>
        <button onClick={() => { setSelectedCategoryId(active ? null : cat.id); if (hasKids) toggleCategory(cat.id); }}
          className={`w-full flex items-center gap-1.5 px-2 py-2 rounded text-sm text-left ${active ? 'bg-[#E8F0FE] text-[#0056D2] font-medium' : 'text-[#666] hover:bg-[#F5F7FA]'}`}
          style={{ paddingLeft: depth * 16 + 8 }}>
          {hasKids ? (expanded ? <ChevronDown className="h-3.5 w-3.5 shrink-0" /> : <ChevronRight className="h-3.5 w-3.5 shrink-0" />) : <span className="w-3.5 shrink-0" />}
          <span className="truncate">{cat.name}</span>
        </button>
        {hasKids && expanded && renderCategories(cat.children!, depth + 1)}
      </div>
    );
  });

  const ResourceCard = ({ r }: { r: Resource }) => {
    const ti = getFileTypeInfo(r.fileExtension);
    const cid = collectedIds[r.id!];
    return (
      <div className="group rounded-lg border border-[#E8E8E8] bg-white overflow-hidden hover:border-[#0056D2] hover:shadow-md transition-all">
        <div className="aspect-[16/10] bg-gray-50 flex items-center justify-center relative cursor-pointer" onClick={() => setPreviewResource(r)}>
          <div className="absolute top-2 left-2 px-2 py-0.5 rounded text-[10px] font-bold text-white" style={{ backgroundColor: ti.color }}>{ti.label}</div>
          <FileText className="h-12 w-12 text-[#ccc] group-hover:text-[#0056D2]/40 transition-colors" />
          <div className="absolute inset-0 bg-black/0 group-hover:bg-black/5 flex items-center justify-center">
            <Eye className="h-8 w-8 text-white opacity-0 group-hover:opacity-100 drop-shadow-lg" />
          </div>
        </div>
        <div className="p-3">
          <div className="text-sm font-medium text-[#1a1a1a] line-clamp-2">{r.name}</div>
          {r.description && <p className="text-xs text-[#999] mt-1 line-clamp-1">{r.description}</p>}
          <div className="flex items-center gap-3 mt-2 text-xs text-[#999]">
            <span className="flex items-center gap-1"><Download className="h-3 w-3" />{r.downloadCount}</span>
            <span className="flex items-center gap-1"><Eye className="h-3 w-3" />{r.viewCount}</span>
            <span>{formatSize(r.fileSize)}</span>
          </div>
          <div className="flex items-center gap-2 mt-3 pt-3 border-t border-[#F0F0F0]">
            <button onClick={(e) => { e.preventDefault(); setPreviewResource(r); }} className="flex-1 flex items-center justify-center gap-1.5 py-1.5 rounded-md bg-[#F5F7FA] text-xs text-[#666] hover:bg-[#E8F0FE] hover:text-[#0056D2]"> <Eye className="h-3.5 w-3.5" />预览</button>
            <button onClick={(e) => { e.preventDefault(); handleDownload(r); }} className="flex-1 flex items-center justify-center gap-1.5 py-1.5 rounded-md bg-[#0056D2] text-white text-xs hover:bg-[#0041A8]"> <Download className="h-3.5 w-3.5" />下载</button>
            <button onClick={(e) => { e.preventDefault(); toggleCollection(r); }} className={`p-1.5 rounded-md ${cid ? 'text-[#FF4D4F] bg-[#FFF1F0]' : 'text-[#ccc] hover:text-[#FF4D4F] hover:bg-[#FFF1F0]'}`}>
              <Heart className={`h-4 w-4 ${cid ? 'fill-current' : ''}`} />
            </button>
          </div>
        </div>
      </div>
    );
  };

  return (
    <>
      <div className="flex flex-col sm:flex-row gap-4 mb-6">
        <div className="flex items-center gap-2 flex-1 max-w-lg bg-white rounded-lg border border-[#E8E8E8] px-3 py-2">
          <Search className="h-4 w-4 text-[#999] shrink-0" />
          <input type="text" value={search} onChange={e => setSearch(e.target.value)} onKeyDown={e => e.key === 'Enter' && refetch()}
            placeholder="搜索资源名称..." className="flex-1 border-0 outline-none text-sm" />
          {search && <button onClick={() => { setSearch(''); setDebouncedSearch(''); }} className="text-[#999]"><X className="h-4 w-4" /></button>}
        </div>
        <div className="flex items-center gap-1 overflow-x-auto">
          {typeTabs.map(t => (
            <button key={String(t.key)} onClick={() => { setSelectedType(t.key); setPage(0); }}
              className={`px-3 py-2 rounded-md text-xs font-medium whitespace-nowrap ${selectedType === t.key ? 'bg-[#0056D2] text-white' : 'bg-white border border-[#E8E8E8] text-[#666] hover:border-[#0056D2]'}`}>{t.label}</button>
          ))}
        </div>
      </div>

      <div className="flex gap-6">
        <aside className={`shrink-0 ${showFilters ? 'block' : 'hidden'} lg:block w-56`}>
          <div className="sticky top-20 bg-white rounded-lg border border-[#E8E8E8] p-4">
            <div className="flex items-center justify-between mb-3">
              <h3 className="text-sm font-semibold">资源分类</h3>
              {selectedCategoryId && <button onClick={() => setSelectedCategoryId(null)} className="text-xs text-[#0056D2]">清除</button>}
            </div>
            <div className="space-y-0.5 max-h-[60vh] overflow-y-auto">
              <button onClick={() => setSelectedCategoryId(null)} className={`w-full text-left px-2 py-2 rounded text-sm ${!selectedCategoryId ? 'bg-[#E8F0FE] text-[#0056D2] font-medium' : 'text-[#666] hover:bg-[#F5F7FA]'}`}>全部分类</button>
              {renderCategories(categories)}
            </div>
          </div>
        </aside>

        <div className="flex-1 min-w-0">
          {isLoading && <div className="flex justify-center min-h-[300px]"><div className="animate-spin rounded-full h-8 w-8 border-3 border-[#E8E8E8] border-t-[#0056D2]" /></div>}
          {!isLoading && resources.length === 0 && <div className="text-center py-16 text-[#999] bg-white rounded-lg border border-[#E8E8E8]"><FileText className="h-12 w-12 mx-auto mb-3 text-[#ccc]" /><p className="text-sm">{debouncedSearch || selectedType !== null || selectedCategoryId ? '未找到匹配的资源' : '暂无资源'}</p></div>}
          {!isLoading && resources.length > 0 && viewMode === 'grid' && <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">{resources.map(r => <ResourceCard key={r.id} r={r} />)}</div>}
          {!isLoading && resources.length > 0 && viewMode === 'list' && (
            <div className="space-y-2">{resources.map(r => {
              const ti = getFileTypeInfo(r.fileExtension); const cid = collectedIds[r.id!];
              return (
                <div key={r.id} className="flex items-center gap-4 p-3 rounded-lg border border-[#E8E8E8] bg-white hover:border-[#0056D2]">
                  <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg cursor-pointer" style={{ backgroundColor: `${ti.color}15`, color: ti.color }} onClick={() => setPreviewResource(r)}><FileText className="h-5 w-5" /></div>
                  <div className="flex-1 min-w-0"><div className="text-sm font-medium truncate">{r.name}</div>{r.description && <div className="text-xs text-[#999] mt-0.5 line-clamp-1">{r.description}</div>}</div>
                  <div className="flex items-center gap-3 text-xs text-[#999] shrink-0"><span><Download className="inline h-3 w-3 mr-0.5" />{r.downloadCount}</span><span><Eye className="inline h-3 w-3 mr-0.5" />{r.viewCount}</span><span>{formatSize(r.fileSize)}</span></div>
                  <div className="flex items-center gap-1 shrink-0">
                    <button onClick={() => setPreviewResource(r)} className="p-1.5 rounded-md text-[#999] hover:bg-[#F5F7FA] hover:text-[#0056D2]"><Eye className="h-4 w-4" /></button>
                    <button onClick={() => handleDownload(r)} className="p-1.5 rounded-md text-[#999] hover:bg-[#F5F7FA] hover:text-[#0056D2]"><Download className="h-4 w-4" /></button>
                    <button onClick={() => toggleCollection(r)} className={`p-1.5 rounded-md ${cid ? 'text-[#FF4D4F]' : 'text-[#ccc] hover:text-[#FF4D4F]'}`}><Heart className={`h-4 w-4 ${cid ? 'fill-current' : ''}`} /></button>
                  </div>
                </div>
              );
            })}</div>
          )}
          {totalPages > 1 && <Pagination page={page} totalPages={totalPages} onChange={setPage} />}
        </div>
      </div>
      {previewResource && <FilePreviewModal resourceId={previewResource.id ?? null} resourceName={previewResource.name} fileExtension={previewResource.fileExtension ?? ''} fileSize={previewResource.fileSize ?? 0} onClose={() => setPreviewResource(null)} />}
    </>
  );
}

// ===== Favorites Panel =====
function FavoritesPanel() {
  const [resources, setResources] = useState<Resource[]>([]);
  const [loading, setLoading] = useState(true);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(0);
  const [previewResource, setPreviewResource] = useState<Resource | null>(null);

  useEffect(() => { load(0); }, []);
  const load = (p: number) => {
    setLoading(true);
    api.get('/api/app/resource/collected-list', { params: { skipCount: p * PAGE_SIZE, maxResultCount: PAGE_SIZE } })
      .then(res => { setResources(res.data?.items || []); setTotalCount(res.data?.totalCount || 0); setLoading(false); })
      .catch(() => { setResources([]); setLoading(false); });
  };

  const removeFavorite = (r: Resource) => { api.post(`/api/app/resource/uncollect/${r.id}`).then(() => load(page)).catch(() => {}); };
  const handleDownload = async (r: Resource) => { if (r.id) try { await downloadResourceFile(r.id, r.originalFileName || r.name || 'download'); } catch {} };
  const totalPages = Math.ceil(totalCount / PAGE_SIZE);

  if (loading) return <div className="flex justify-center min-h-[300px]"><div className="animate-spin rounded-full h-8 w-8 border-3 border-[#E8E8E8] border-t-[#0056D2]" /></div>;
  if (resources.length === 0) return <div className="text-center py-16 text-[#999] bg-white rounded-lg border border-[#E8E8E8]"><Star className="h-12 w-12 mx-auto mb-3 text-[#ccc]" /><p className="text-sm">暂无收藏资源</p></div>;

  return (
    <>
      <div className="text-sm text-[#999] mb-4">共 {totalCount} 个收藏</div>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {resources.map(r => {
          const ti = getFileTypeInfo(r.fileExtension);
          return (
            <div key={r.id} className="rounded-lg border border-[#E8E8E8] bg-white overflow-hidden hover:border-[#0056D2] hover:shadow-md transition-all">
              <div className="aspect-[16/10] bg-gray-50 flex items-center justify-center relative cursor-pointer" onClick={() => setPreviewResource(r)}>
                <div className="absolute top-2 left-2 px-2 py-0.5 rounded text-[10px] font-bold text-white" style={{ backgroundColor: ti.color }}>{ti.label}</div>
                <FileText className="h-12 w-12 text-[#ccc]" />
              </div>
              <div className="p-3">
                <div className="text-sm font-medium line-clamp-2">{r.name}</div>
                <div className="flex items-center gap-3 mt-2 text-xs text-[#999]">
                  <span><Download className="inline h-3 w-3 mr-0.5" />{r.downloadCount}</span>
                  <span><Eye className="inline h-3 w-3 mr-0.5" />{r.viewCount}</span>
                </div>
                <div className="flex items-center gap-2 mt-3 pt-3 border-t border-[#F0F0F0]">
                  <button onClick={() => setPreviewResource(r)} className="flex-1 py-1.5 rounded-md bg-[#F5F7FA] text-xs text-[#666] hover:bg-[#E8F0FE]"><Eye className="inline h-3.5 w-3.5 mr-1" />预览</button>
                  <button onClick={() => handleDownload(r)} className="flex-1 py-1.5 rounded-md bg-[#0056D2] text-white text-xs"><Download className="inline h-3.5 w-3.5 mr-1" />下载</button>
                  <button onClick={() => removeFavorite(r)} className="p-1.5 rounded-md text-[#FF4D4F] bg-[#FFF1F0]"><Heart className="h-4 w-4 fill-current" /></button>
                </div>
              </div>
            </div>
          );
        })}
      </div>
      {totalPages > 1 && <Pagination page={page} totalPages={totalPages} onChange={(p) => { setPage(p); load(p); }} />}
      {previewResource && <FilePreviewModal resourceId={previewResource.id ?? null} resourceName={previewResource.name} fileExtension={previewResource.fileExtension ?? ''} fileSize={previewResource.fileSize ?? 0} onClose={() => setPreviewResource(null)} />}
    </>
  );
}

// ===== Search Panel =====
function SearchPanel() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [query, setQuery] = useState(searchParams.get('query') || '');
  const [results, setResults] = useState<SearchResultItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(0);
  const [hasSearched, setHasSearched] = useState(false);
  const [error, setError] = useState('');

  // Auto-search when query param changes from history
  useEffect(() => {
    const q = searchParams.get('query');
    if (q && q !== query) {
      setQuery(q);
      setPage(0);
      setHasSearched(true);
      setLoading(true);
      api.post('/api/app/search/search', { query: q, skipCount: 0, maxResultCount: PAGE_SIZE })
        .then(res => { setResults(res.data?.items || []); setTotalCount(res.data?.totalCount || 0); })
        .catch(err => { setError('搜索失败: ' + (err?.response?.data?.error?.message || err.message)); })
        .finally(() => setLoading(false));
    }
  }, [searchParams]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    const q = query.trim();
    if (!q) return;
    setPage(0); setHasSearched(true); setLoading(true); setError('');
    setSearchParams({ tab: 'search', query: q }, { replace: true });
    api.post('/api/app/search/search', { query: q, skipCount: 0, maxResultCount: PAGE_SIZE })
      .then(res => { setResults(res.data?.items || []); setTotalCount(res.data?.totalCount || 0); })
      .catch(err => { setError('搜索失败，请检查网络连接'); console.error('Search error:', err); })
      .finally(() => setLoading(false));
  };

  const changePage = (p: number) => {
    setPage(p); setLoading(true);
    const q = query.trim();
    api.post('/api/app/search/search', { query: q, skipCount: p * PAGE_SIZE, maxResultCount: PAGE_SIZE })
      .then(res => { setResults(res.data?.items || []); setTotalCount(res.data?.totalCount || 0); })
      .catch(err => { setError('搜索失败'); console.error(err); })
      .finally(() => setLoading(false));
  };

  const totalPages = Math.ceil(totalCount / PAGE_SIZE);

  return (
    <>
      <form onSubmit={handleSearch} className="flex items-center gap-2 max-w-2xl mb-6 bg-white rounded-lg border border-[#E8E8E8] px-3 py-2">
        <Search className="h-5 w-5 text-[#999] shrink-0" />
        <input type="text" value={query} onChange={e => setQuery(e.target.value)}
          placeholder="输入关键词搜索资源内容..." className="flex-1 border-0 outline-none text-sm" />
        <button type="submit" className="px-5 py-2 rounded-md bg-[#0056D2] text-white text-sm font-medium hover:bg-[#0041A8]">搜索</button>
      </form>

      {error && <div className="mb-4 p-3 rounded-lg bg-[#FFF1F0] border border-[#FFA39E] text-sm text-[#FF4D4F]">{error}</div>}

      {!hasSearched && !loading && <div className="text-center py-16 text-[#999] bg-white rounded-lg border border-[#E8E8E8]"><Search className="h-12 w-12 mx-auto mb-3 text-[#ccc]" /><p className="text-sm">输入关键词搜索资源内容</p></div>}
      {loading && <div className="flex justify-center min-h-[200px] items-center"><div className="animate-spin rounded-full h-8 w-8 border-3 border-[#E8E8E8] border-t-[#0056D2]" /></div>}
      {hasSearched && !loading && results.length === 0 && !error && <div className="text-center py-16 text-[#999] bg-white rounded-lg border border-[#E8E8E8]"><Inbox className="h-12 w-12 mx-auto mb-3 text-[#ccc]" /><p className="text-sm">未找到匹配结果</p></div>}
      {hasSearched && !loading && results.length > 0 && (
        <>
          <div className="text-sm text-[#999] mb-4">共 {totalCount} 条结果</div>
          <div className="space-y-4">
            {results.map(r => (
              <div key={r.id} className="p-4 rounded-lg border border-[#E8E8E8] bg-white hover:border-[#0056D2] transition-colors">
                <div className="flex items-start gap-2 mb-2">
                  <FileText className="h-4 w-4 text-[#0056D2] shrink-0 mt-0.5" />
                  <div>
                    <div className="text-sm font-medium text-[#1a1a1a]">{r.resourceName}</div>
                    <div className="text-xs text-[#999] mt-0.5">第 {r.pageNumber} 页 · {r.fileExtension} · {formatDate(r.uploadDate)}</div>
                  </div>
                </div>
                <div className="text-sm text-[#666] leading-relaxed"
                  dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(r.highlightedContent || r.content?.substring(0, 200) || '') }} />
              </div>
            ))}
          </div>
          {totalPages > 1 && <Pagination page={page} totalPages={totalPages} onChange={changePage} />}
        </>
      )}
    </>
  );
}

// ===== History Panel =====
function HistoryPanel() {
  const [, setSearchParams] = useSearchParams();
  const [history, setHistory] = useState<SearchHistoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    setLoading(true); setError('');
    api.get('/api/app/search/my-search-history', { params: { skipCount: 0, maxResultCount: 50 } })
      .then(res => { setHistory(Array.isArray(res.data) ? res.data : []); setLoading(false); })
      .catch(err => { setError('加载历史失败'); console.error('History error:', err); setLoading(false); });
  }, []);

  const handleHistoryClick = (item: SearchHistoryItem) => {
    setSearchParams({ tab: 'search', query: item.queryText }, { replace: true });
  };

  if (loading) return <div className="flex justify-center min-h-[200px] items-center"><div className="animate-spin rounded-full h-8 w-8 border-3 border-[#E8E8E8] border-t-[#0056D2]" /></div>;
  if (error) return <div className="text-center py-16 text-[#999] bg-white rounded-lg border border-[#E8E8E8]"><History className="h-12 w-12 mx-auto mb-3 text-[#ccc]" /><p className="text-sm text-[#FF4D4F]">{error}</p></div>;
  if (history.length === 0) return <div className="text-center py-16 text-[#999] bg-white rounded-lg border border-[#E8E8E8]"><History className="h-12 w-12 mx-auto mb-3 text-[#ccc]" /><p className="text-sm">暂无搜索历史</p></div>;

  return (
    <div className="space-y-2">
      {history.map(h => (
        <button key={h.id} onClick={() => handleHistoryClick(h)}
          className="w-full flex items-center gap-3 p-3 rounded-lg border border-[#E8E8E8] bg-white hover:border-[#0056D2] hover:bg-[#F0F6FF] transition-colors text-left">
          <History className="h-4 w-4 text-[#999] shrink-0" />
          <div className="flex-1 min-w-0">
            <div className="text-sm text-[#1a1a1a] truncate">{h.queryText}</div>
            <div className="text-xs text-[#999] mt-0.5">{formatDate(h.creationTime)} · {h.resultCount} 条结果</div>
          </div>
          <Search className="h-4 w-4 text-[#ccc]" />
        </button>
      ))}
    </div>
  );
}

// ===== Pagination =====
function Pagination({ page, totalPages, onChange }: { page: number; totalPages: number; onChange: (p: number) => void }) {
  return (
    <div className="flex justify-center items-center gap-2 mt-8">
      <button disabled={page === 0} onClick={() => onChange(page - 1)} className="px-4 py-2 rounded-md border border-[#E8E8E8] text-sm text-[#666] hover:border-[#0056D2] disabled:opacity-40 disabled:cursor-not-allowed">上一页</button>
      {Array.from({ length: Math.min(totalPages, 7) }, (_: any, i: number) => {
        const p = page < 3 ? i : page > totalPages - 4 ? totalPages - 7 + i : page - 3 + i;
        if (p < 0 || p >= totalPages) return null;
        return <button key={p} onClick={() => onChange(p)} className={`w-9 h-9 rounded-md text-sm font-medium ${p === page ? 'bg-[#0056D2] text-white' : 'text-[#666] hover:bg-[#F5F7FA] border border-[#E8E8E8]'}`}>{p + 1}</button>;
      })}
      <button disabled={page >= totalPages - 1} onClick={() => onChange(page + 1)} className="px-4 py-2 rounded-md border border-[#E8E8E8] text-sm text-[#666] hover:border-[#0056D2] disabled:opacity-40 disabled:cursor-not-allowed">下一页</button>
    </div>
  );
}
