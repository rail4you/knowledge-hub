import { useState, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Search, FileText, Video, Inbox } from 'lucide-react';
import DOMPurify from 'dompurify';
import { useAuth } from '../../lib/auth';
import { api, setTenantId } from '../../lib/api';

interface SearchResultItem {
  id: string;
  resourceId: string;
  resourceName: string;
  pageNumber: number;
  content: string;
  highlightedContent?: string | null;
  title?: string | null;
  relevanceScore: number;
  fileExtension: string;
  resourceType: number;
  categoryName?: string;
  uploadDate: string;
  sourceType?: 'document' | 'video';
  videoId?: string;
  videoName?: string;
  videoUrl?: string;
  startTime?: string;
  endTime?: string;
  eventDescription?: string;
}

interface SearchResponse {
  items: SearchResultItem[];
  totalCount: number;
  query: string;
}

const pageSize = 12;

export function SearchTab() {
  const auth = useAuth();
  const [searchParams] = useSearchParams();
  const [searchQuery, setSearchQuery] = useState('');
  const [results, setResults] = useState<SearchResultItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [pageIndex, setPageIndex] = useState(1);
  const [hasSearched, setHasSearched] = useState(false);

  // Read query from URL when tab switches (e.g. from SearchHistoryTab)
  useEffect(() => {
    const queryFromUrl = searchParams.get('query');
    if (queryFromUrl) {
      setSearchQuery(queryFromUrl);
      setPageIndex(1);
      performSearch(queryFromUrl, 0);
    }
  }, [searchParams]);

  useEffect(() => {
    if (auth.currentTenantId) {
      setTenantId(auth.currentTenantId);
    }
  }, [auth.currentTenantId]);

  const performSearch = (query: string, skip: number) => {
    if (!query.trim()) return;
    setLoading(true);
    setHasSearched(true);
    api.post<SearchResponse>('/api/app/search/search', {
      query,
      skipCount: skip,
      maxResultCount: pageSize,
      sorting: 'relevanceScore desc',
    }).then(res => {
      setResults(res.data?.items || []);
      setTotalCount(res.data?.totalCount || 0);
      setLoading(false);
    }).catch(() => {
      setResults([]);
      setLoading(false);
    });
  };

  const handleSearch = () => {
    setPageIndex(1);
    performSearch(searchQuery, 0);
  };

  const handlePageChange = (newPage: number) => {
    setPageIndex(newPage);
    performSearch(searchQuery, (newPage - 1) * pageSize);
  };

  const getScoreColor = (score: number) => {
    if (score >= 0.8) return 'success';
    if (score >= 0.5) return 'warning';
    return 'default';
  };

  return (
    <>
      {/* 搜索框 */}
      <div className="card p-lg mb-lg">
        <div className="search-bar">
          <Search className="h-5 w-5 search-bar-icon" />
          <input
            type="text"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleSearch()}
            placeholder="输入关键词搜索文档..."
            className="input"
          />
          <button onClick={handleSearch} className="btn btn-primary" disabled={loading}>
            <Search className="h-4 w-4" />
            {loading ? '搜索中...' : '搜索'}
          </button>
        </div>
      </div>

      {/* 结果 */}
      {loading ? (
        <div className="loading">
          <div className="loading-spinner" />
        </div>
      ) : !hasSearched ? (
        <div className="empty-state">
          <Search className="empty-state-icon" style={{ color: 'var(--text-muted)' }} />
          <div className="empty-state-title">输入关键词开始搜索</div>
          <div className="empty-state-desc">支持文档内容搜索和视频字幕搜索</div>
        </div>
      ) : results.length === 0 ? (
        <div className="empty-state">
          <Inbox className="empty-state-icon" />
          <div className="empty-state-title">未找到搜索结果</div>
          <div className="empty-state-desc">请尝试其他关键词</div>
        </div>
      ) : (
        <>
          <div className="flex-between mb-lg">
            <span className="text-secondary">
              找到 <span className="text-primary font-semibold">{totalCount}</span> 个结果
            </span>
          </div>

          <div className="search-results">
            {results.map((result, index) => (
              <div key={`${result.resourceId}-${result.pageNumber}-${index}`} className="result-card">
                <div className="result-header">
                  <div className="result-title">
                    {result.sourceType === 'video' ? (
                      <Video className="result-icon" />
                    ) : (
                      <FileText className="result-icon" />
                    )}
                    <span className="result-name">{result.resourceName}</span>
                    <span className="result-page-tag">
                      {result.sourceType === 'video' && result.startTime
                        ? `${result.startTime}${result.endTime ? ' - ' + result.endTime : ''}`
                        : `第 ${result.pageNumber} 页`}
                    </span>
                  </div>
                  <div className="result-meta">
                    {result.categoryName && <span className="tag tag-primary">{result.categoryName}</span>}
                    <span className={`tag tag-${getScoreColor(result.relevanceScore)}`}>
                      {(result.relevanceScore * 100).toFixed(0)}% 相关
                    </span>
                    <span className="tag tag-default">{result.fileExtension || '文档'}</span>
                  </div>
                </div>
                <div className="result-content">
                  <p
                    className="preview-text"
                    dangerouslySetInnerHTML={{
                      __html: DOMPurify.sanitize(result.highlightedContent || result.eventDescription || result.content),
                    }}
                  />
                </div>
              </div>
            ))}
          </div>

          {totalCount > pageSize && (
            <div className="pagination">
              <button
                onClick={() => handlePageChange(pageIndex - 1)}
                disabled={pageIndex === 1}
                className="pagination-btn"
              >
                上一页
              </button>
              <span className="pagination-info">
                第 {pageIndex} / {Math.ceil(totalCount / pageSize)} 页
              </span>
              <button
                onClick={() => handlePageChange(pageIndex + 1)}
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
