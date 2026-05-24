import { useState, useEffect } from 'react';
import { Star, Download, Eye, FileText, Video, Inbox } from 'lucide-react';
import { useAuth } from '../../lib/auth';
import { api, setTenantId } from '../../lib/api';
import { downloadResourceFile } from '../../lib/file-api';
import { FilePreviewModal } from '../../components/FilePreviewModal';
import type { Resource } from '../../lib/types';

export function FavoritesTab() {
  const auth = useAuth();
  const [resources, setResources] = useState<Resource[]>([]);
  const [loading, setLoading] = useState(true);
  const [totalCount, setTotalCount] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const pageSize = 12;

  useEffect(() => {
    if (auth.currentTenantId) {
      setTenantId(auth.currentTenantId);
    }
  }, [auth.currentTenantId]);

  useEffect(() => {
    if (auth.isAuthenticated) {
      loadFavorites();
    }
  }, [pageIndex, auth.isAuthenticated]);

  const loadFavorites = () => {
    setLoading(true);
    api.get('/api/app/resource/collected-list', {
      params: {
        skipCount: (pageIndex - 1) * pageSize,
        maxResultCount: pageSize,
      }
    }).then(res => {
      setResources(res.data?.items || []);
      setTotalCount(res.data?.totalCount || 0);
      setLoading(false);
    }).catch(() => {
      setResources([]);
      setLoading(false);
    });
  };

  const removeFavorite = (resource: Resource) => {
    api.post(`/api/app/resource/uncollect/${resource.id}`).then(() => {
      loadFavorites();
    }).catch(() => {});
  };

  const [previewResource, setPreviewResource] = useState<Resource | null>(null);

  const handleDownload = async (resource: Resource) => {
    if (!resource.id) return;
    try {
      await downloadResourceFile(resource.id, resource.originalFileName || resource.name || 'download');
    } catch {
      // error handled silently
    }
  };

  const getResourceTypeIcon = (type: number) => {
    return type === 1 ? <Video className="h-8 w-8" /> : <FileText className="h-8 w-8" />;
  };

  const getTypeGradient = (type: number) => {
    switch (type) {
      case 0: return 'linear-gradient(135deg, #3B82F6, #2563EB)';
      case 1: return 'linear-gradient(135deg, #8B5CF6, #7C3AED)';
      default: return 'linear-gradient(135deg, #6B7280, #4B5563)';
    }
  };

  return (
    <>
      {previewResource && (
        <FilePreviewModal
          resourceId={previewResource.id}
          resourceName={previewResource.originalFileName || previewResource.name || '未命名'}
          fileExtension={previewResource.fileExtension || ''}
          fileSize={previewResource.fileSize || 0}
          onClose={() => setPreviewResource(null)}
        />
      )}

      {loading ? (
        <div className="loading">
          <div className="loading-spinner" />
        </div>
      ) : resources.length === 0 ? (
        <div className="empty-state">
          <Inbox className="empty-state-icon" />
          <div className="empty-state-title">你还没有收藏任何资源</div>
          <div className="empty-state-desc">在资源库中点击收藏按钮即可收藏</div>
        </div>
      ) : (
        <>
          <div className="flex items-center gap-sm mb-lg">
            <span className="text-secondary">共收藏</span>
            <span className="text-primary font-semibold">{totalCount}</span>
            <span className="text-secondary">个资源</span>
          </div>

          <div className="resource-grid">
            {resources.map(resource => (
              <div key={resource.id} className={`resource-card type-${resource.resourceType || 0}`}>
                <div
                  className="resource-card-thumbnail"
                  style={{ background: getTypeGradient(resource.resourceType) }}
                >
                  {getResourceTypeIcon(resource.resourceType)}
                </div>
                <div className="resource-card-body">
                  <div className="flex-between mb-sm">
                    <span className="tag tag-primary">
                      {resource.resourceType === 0 ? '文档' : resource.resourceType === 1 ? '视频' : '资源'}
                    </span>
                    <button
                      onClick={() => removeFavorite(resource)}
                      className="btn btn-sm text-orange-500"
                      style={{ background: '#FFF7E6' }}
                    >
                      <Star className="h-4 w-4 fill-orange-400" />
                    </button>
                  </div>
                  <h3 className="resource-card-title">{resource.name || '未命名资源'}</h3>
                  <p className="resource-card-desc">{resource.description || '暂无描述'}</p>
                  <div className="resource-card-meta">
                    <span>{resource.creationTime?.split('T')[0]}</span>
                    <span>{((resource.fileSize || 0) / 1024 / 1024).toFixed(1)} MB</span>
                  </div>
                </div>
                <div className="resource-card-actions">
                  <button
                    onClick={() => setPreviewResource(resource)}
                    className="btn btn-default btn-sm"
                  >
                    <Eye className="h-4 w-4" /> 预览
                  </button>
                  <button
                    onClick={() => handleDownload(resource)}
                    className="btn btn-primary btn-sm"
                  >
                    <Download className="h-4 w-4" /> 下载
                  </button>
                </div>
              </div>
            ))}
          </div>

          {totalCount > pageSize && (
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
        </>
      )}
    </>
  );
}
