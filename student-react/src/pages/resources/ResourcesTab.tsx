import { useState, useEffect } from 'react';
import {
  Search,
  FileText,
  Video,
  Download,
  Star,
  Eye,
  Flame,
  ChevronDown,
  ChevronRight,
  ChevronUp,
  Inbox,
  Filter,
  X,
} from 'lucide-react';
import { useAuth } from '../../lib/auth';
import { api, setTenantId } from '../../lib/api';
import { downloadResourceFile } from '../../lib/file-api';
import { FilePreviewModal } from '../../components/FilePreviewModal';
import type { Resource } from '../../lib/types';

interface ResourceCategory {
  id: string;
  name: string;
  parentId?: string | null;
  children?: ResourceCategory[];
}

interface RatingSummary {
  averageRating: number;
  totalReviews: number;
}

interface RecommendedResource {
  resourceId: string;
  resourceName: string;
  description?: string;
  resourceType: number;
  categoryId?: string;
  categoryName?: string;
  fileSize?: number;
  fileExtension?: string;
  creationTime?: string;
  recommendationReason: string;
  averageRating: number;
}

export function ResourcesTab() {
  const auth = useAuth();
  const [resources, setResources] = useState<Resource[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterText, setFilterText] = useState('');
  const [selectedType, setSelectedType] = useState<number | null>(null);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);
  const [expandedCategories, setExpandedCategories] = useState<Set<string>>(new Set());
  const [categories, setCategories] = useState<ResourceCategory[]>([]);

  const [totalCount, setTotalCount] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const pageSize = 12;

  const [selectedResource, setSelectedResource] = useState<Resource | null>(null);
  const [drawerVisible, setDrawerVisible] = useState(false);
  const [ratingSummaries, setRatingSummaries] = useState<Record<string, RatingSummary>>({});
  const [collectedIds, setCollectedIds] = useState<Record<string, boolean>>({});

  const [recommendedResources, setRecommendedResources] = useState<RecommendedResource[]>([]);
  const [recommendationsCollapsed, setRecommendationsCollapsed] = useState(false);

  // Preview state
  const [previewResource, setPreviewResource] = useState<Resource | null>(null);

  useEffect(() => {
    if (auth.currentTenantId) {
      setTenantId(auth.currentTenantId);
    }
  }, [auth.currentTenantId]);

  useEffect(() => {
    loadCategories();
  }, []);

  useEffect(() => {
    loadResources();
  }, [selectedType, selectedCategoryId, pageIndex]);

  useEffect(() => {
    if (auth.isAuthenticated) {
      loadRecommendations();
    }
  }, [auth.isAuthenticated]);

  const loadResources = () => {
    setLoading(true);
    const params: Record<string, any> = {
      skipCount: (pageIndex - 1) * pageSize,
      maxResultCount: pageSize,
      status: 3, // LeagueApproved
    };
    if (filterText) params.filter = filterText;
    if (selectedType !== null) params.resourceType = selectedType;
    if (selectedCategoryId) params.categoryId = selectedCategoryId;

    api.get('/api/app/resource/filtered-list', { params }).then(res => {
      setResources(res.data.items || []);
      setTotalCount(res.data.totalCount || 0);
      setLoading(false);
      const items = res.data.items || [];
      loadRatingSummaries(items);
      loadCollectionStatus(items);
    }).catch(() => {
      setResources([]);
      setLoading(false);
    });
  };

  const loadCategories = () => {
    api.get('/api/app/resource/categories').then(res => {
      setCategories(res.data);
    }).catch(() => {});
  };

  const loadRatingSummaries = (items: Resource[]) => {
    const summaries = { ...ratingSummaries };
    items.forEach(resource => {
      if (!resource.id) return;
      api.get(`/api/app/resource-review/rating-summary/${resource.id}`).then(res => {
        summaries[resource.id!] = res.data;
        setRatingSummaries({ ...summaries });
      }).catch(() => {});
    });
  };

  const loadCollectionStatus = (items: Resource[]) => {
    if (items.length === 0) return;
    items.forEach(resource => {
      if (!resource.id) return;
      api.get(`/api/app/resource/is-collected/${resource.id}`).then(res => {
        setCollectedIds(prev => ({ ...prev, [resource.id!]: res.data }));
      }).catch(() => {});
    });
  };

  const loadRecommendations = () => {
    api.get('/api/app/recommendation/personalized', { params: { count: 10 } }).then(res => {
      setRecommendedResources(res.data || []);
    }).catch(() => {});
  };

  const onSearch = () => {
    setPageIndex(1);
    loadResources();
  };

  const toggleCollection = (resource: Resource) => {
    if (!resource.id) return;
    const isCollected = collectedIds[resource.id];
    const url = isCollected
      ? `/api/app/resource/uncollect/${resource.id}`
      : `/api/app/resource/collect/${resource.id}`;

    api.post(url).then(() => {
      setCollectedIds(prev => ({ ...prev, [resource.id!]: !isCollected }));
    }).catch(() => {});
  };

  const openPreview = (resource: Resource) => {
    setPreviewResource(resource);
  };

  const closePreview = () => {
    setPreviewResource(null);
  };

  const handleDownload = async (resource: Resource) => {
    if (!resource.id) return;
    try {
      await downloadResourceFile(resource.id, resource.originalFileName || resource.name || 'download');
    } catch {
      // error handled silently
    }
  };

  const selectResource = (resource: Resource) => {
    setSelectedResource(resource);
    setDrawerVisible(true);
  };

  const toggleCategoryExpand = (categoryId: string) => {
    const next = new Set(expandedCategories);
    if (next.has(categoryId)) next.delete(categoryId);
    else next.add(categoryId);
    setExpandedCategories(next);
  };

  const getResourceTypeIcon = (type: number) => {
    return type === 1 ? <Video className="h-6 w-6" /> : <FileText className="h-6 w-6" />;
  };

  const resourceTypes = [
    { label: '全部', value: null as number | null },
    { label: '文档', value: 0 },
    { label: '视频', value: 1 },
  ];

  return (
    <>
      {/* File Preview Modal */}
      {previewResource && (
        <FilePreviewModal
          resourceId={previewResource.id}
          resourceName={previewResource.originalFileName || previewResource.name || '未命名'}
          fileExtension={previewResource.fileExtension || ''}
          fileSize={previewResource.fileSize || 0}
          onClose={closePreview}
        />
      )}

      {/* 推荐轮播 */}
      {!recommendationsCollapsed && recommendedResources.length > 0 && (
        <div className="recommendation-section">
          <div className="recommendation-header">
            <div className="recommendation-title">
              <Flame className="h-5 w-5" />
              为你推荐
            </div>
            <button
              onClick={() => setRecommendationsCollapsed(true)}
              className="btn btn-default btn-sm"
            >
              <ChevronUp className="h-4 w-4" /> 收起
            </button>
          </div>
          <div className="recommendation-scroll">
            {recommendedResources.map(rec => (
              <div
                key={rec.resourceId}
                onClick={() => selectResource({
                  id: rec.resourceId,
                  name: rec.resourceName,
                  description: rec.description,
                  resourceType: rec.resourceType,
                  categoryId: rec.categoryId,
                  fileSize: rec.fileSize,
                  fileExtension: rec.fileExtension,
                  creationTime: rec.creationTime,
                } as Resource)}
                className="recommendation-card"
              >
                <div className={`w-12 h-12 rounded-lg flex items-center justify-center mb-3 ${rec.resourceType === 1 ? 'bg-purple-100 text-purple-600' : 'bg-blue-100 text-blue-600'}`}>
                  {getResourceTypeIcon(rec.resourceType)}
                </div>
                <div className="text-sm font-medium truncate mb-1">{rec.resourceName}</div>
                <div className="flex items-center gap-2">
                  <span className="tag tag-warning">{rec.recommendationReason}</span>
                  {rec.averageRating > 0 && (
                    <span className="text-xs text-muted">{rec.averageRating.toFixed(1)}</span>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* 收起状态 */}
      {recommendationsCollapsed && recommendedResources.length > 0 && (
        <div
          onClick={() => setRecommendationsCollapsed(false)}
          className="card card-clickable p-md mb-lg flex items-center gap-sm"
        >
          <Flame className="h-4 w-4 text-orange-500" />
          <span className="text-secondary">为你推荐</span>
          <ChevronDown className="h-4 w-4 text-muted" />
        </div>
      )}

      {/* 主内容区 */}
      <div className="content-layout">
        {/* 左侧分类 */}
        <aside className="category-sidebar">
          <div className="sidebar-title">
            <Filter className="h-4 w-4 inline mr-2" />
            全部分类
          </div>
          <div className="category-list">
            <div
              className={`category-item ${selectedCategoryId === null ? 'active' : ''}`}
              onClick={() => { setSelectedCategoryId(null); setPageIndex(1); }}
            >
              全部资源
            </div>
            {categories.map(cat => (
              <div key={cat.id} className="category-group">
                <div
                  className={`category-item ${selectedCategoryId === cat.id ? 'active' : ''}`}
                  onClick={() => { setSelectedCategoryId(cat.id); setPageIndex(1); }}
                >
                  {cat.children && cat.children.length > 0 && (
                    <span onClick={e => { e.stopPropagation(); toggleCategoryExpand(cat.id); }}>
                      {expandedCategories.has(cat.id) ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
                    </span>
                  )}
                  <span>{cat.name}</span>
                </div>
                {cat.children && cat.children.length > 0 && expandedCategories.has(cat.id) && (
                  <div className="category-children">
                    {cat.children.map(child => (
                      <div
                        key={child.id}
                        className={`category-item ${selectedCategoryId === child.id ? 'active' : ''}`}
                        onClick={() => { setSelectedCategoryId(child.id); setPageIndex(1); }}
                      >
                        {child.name}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            ))}
          </div>
        </aside>

        {/* 右侧内容 */}
        <div className="content-main">
          {/* 搜索和类型切换 */}
          <div className="content-toolbar">
            <div className="search-bar" style={{ maxWidth: '400px' }}>
              <Search className="h-5 w-5 search-bar-icon" />
              <input
                type="text"
                value={filterText}
                onChange={e => setFilterText(e.target.value)}
                placeholder="搜索资源名称..."
                onKeyDown={e => e.key === 'Enter' && onSearch()}
                className="input"
              />
              <button onClick={onSearch} className="btn btn-primary btn-sm">搜索</button>
            </div>
            <div className="type-tabs">
              {resourceTypes.map(type => (
                <button
                  key={type.label}
                  onClick={() => { setSelectedType(type.value); setPageIndex(1); }}
                  className={`type-tab ${selectedType === type.value ? 'active' : ''}`}
                >
                  {type.label}
                </button>
              ))}
            </div>
          </div>

          {/* 加载状态 */}
          {loading && (
            <div className="loading">
              <div className="loading-spinner" />
            </div>
          )}

          {/* 空状态 */}
          {!loading && resources.length === 0 && (
            <div className="empty-state">
              <Inbox className="empty-state-icon" />
              <div className="empty-state-title">暂无资源</div>
              <div className="empty-state-desc">请尝试其他搜索条件</div>
            </div>
          )}

          {/* 资源卡片网格 */}
          {!loading && resources.length > 0 && (
            <div className="resource-grid">
              {resources.map(resource => (
                <div
                  key={resource.id}
                  onClick={() => selectResource(resource)}
                  className={`resource-card type-${resource.resourceType || 0}`}
                >
                  <div className="resource-card-thumbnail">
                    {getResourceTypeIcon(resource.resourceType)}
                  </div>
                  <div className="resource-card-body">
                    <h3 className="resource-card-title">{resource.name}</h3>
                    <p className="resource-card-desc">{resource.description || '暂无描述'}</p>
                    <div className="resource-card-meta">
                      <div className="flex items-center gap-md">
                        {ratingSummaries[resource.id!] && (
                          <span className="text-orange-500">
                            {ratingSummaries[resource.id!].averageRating.toFixed(1)}
                            <span className="text-muted">({ratingSummaries[resource.id!].totalReviews})</span>
                          </span>
                        )}
                        {!ratingSummaries[resource.id!] && <span>暂无评分</span>}
                      </div>
                      {resource.creationTime && (
                        <span>{new Date(resource.creationTime).toLocaleDateString('zh-CN')}</span>
                      )}
                    </div>
                  </div>
                  <div className="resource-card-actions">
                    <button
                      onClick={e => { e.stopPropagation(); openPreview(resource); }}
                      className="btn btn-default btn-sm"
                    >
                      <Eye className="h-4 w-4" /> 预览
                    </button>
                    <button
                      onClick={e => { e.stopPropagation(); handleDownload(resource); }}
                      className="btn btn-primary btn-sm"
                    >
                      <Download className="h-4 w-4" /> 下载
                    </button>
                    <button
                      onClick={e => { e.stopPropagation(); toggleCollection(resource); }}
                      className={`btn btn-sm ${collectedIds[resource.id!] ? 'text-orange-500' : ''}`}
                    >
                      <Star className={`h-4 w-4 ${collectedIds[resource.id!] ? 'fill-orange-400' : ''}`} />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* 分页 */}
          {!loading && totalCount > pageSize && (
            <div className="pagination">
              <button
                onClick={() => setPageIndex(p => Math.max(1, p - 1))}
                disabled={pageIndex === 1}
                className="pagination-btn"
              >
                上一页
              </button>
              <span className="pagination-info">
                第 {pageIndex} / {Math.ceil(totalCount / pageSize)} 页
              </span>
              <button
                onClick={() => setPageIndex(p => Math.min(Math.ceil(totalCount / pageSize), p + 1))}
                disabled={pageIndex >= Math.ceil(totalCount / pageSize)}
                className="pagination-btn"
              >
                下一页
              </button>
            </div>
          )}
        </div>
      </div>

      {/* 详情 Drawer */}
      {drawerVisible && selectedResource && (
        <ResourceDrawer
          resource={selectedResource}
          isCollected={!!collectedIds[selectedResource.id!]}
          onClose={() => setDrawerVisible(false)}
          onPreview={() => openPreview(selectedResource)}
          onDownload={() => handleDownload(selectedResource)}
          onToggleCollection={() => toggleCollection(selectedResource)}
          getResourceTypeIcon={getResourceTypeIcon}
        />
      )}
    </>
  );
}

interface ResourceDrawerProps {
  resource: Resource;
  isCollected: boolean;
  onClose: () => void;
  onPreview: () => void;
  onDownload: () => void;
  onToggleCollection: () => void;
  getResourceTypeIcon: (type: number) => React.ReactNode;
}

function ResourceDrawer({
  resource,
  isCollected,
  onClose,
  onPreview,
  onDownload,
  onToggleCollection,
  getResourceTypeIcon,
}: ResourceDrawerProps) {
  return (
    <>
      <div className="drawer-overlay" onClick={onClose} />
      <div className="drawer">
        <div className="drawer-header">
          <h2 className="drawer-title">{resource.name}</h2>
          <button onClick={onClose} className="drawer-close">
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="drawer-body">
          <div className="flex items-center gap-md mb-lg">
            <div className={`w-12 h-12 rounded-lg flex items-center justify-center ${resource.resourceType === 1 ? 'bg-purple-100 text-purple-600' : 'bg-blue-100 text-blue-600'}`}>
              {getResourceTypeIcon(resource.resourceType)}
            </div>
            <div>
              <div className="tag tag-primary">
                {resource.resourceType === 0 ? '文档' : '视频'}
              </div>
              {resource.fileSize && (
                <div className="text-muted text-sm mt-sm">
                  {(resource.fileSize / 1024 / 1024).toFixed(2)} MB
                </div>
              )}
            </div>
          </div>

          <div className="grid gap-md mb-lg">
            <div className="flex items-center gap-md">
              <span className="text-muted w-16">上传时间</span>
              <span>{resource.creationTime ? new Date(resource.creationTime).toLocaleString('zh-CN') : '-'}</span>
            </div>
          </div>

          {resource.description && (
            <div className="mb-lg">
              <div className="text-muted mb-sm">描述</div>
              <div className="text-secondary">{resource.description}</div>
            </div>
          )}

          <div className="flex gap-md">
            <button onClick={onPreview} className="btn btn-primary flex-1">
              <Eye className="h-4 w-4" /> 预览
            </button>
            <button onClick={onDownload} className="btn btn-outline flex-1">
              <Download className="h-4 w-4" /> 下载
            </button>
            <button
              onClick={onToggleCollection}
              className={`btn ${isCollected ? 'text-orange-500 bg-orange-50' : 'btn-default'}`}
            >
              <Star className={`h-4 w-4 ${isCollected ? 'fill-orange-400' : ''}`} />
            </button>
          </div>
        </div>
      </div>
    </>
  );
}
