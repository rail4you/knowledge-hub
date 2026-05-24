import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { History, Search, Inbox } from 'lucide-react';
import { useAuth } from '../../lib/auth';
import { api, setTenantId } from '../../lib/api';

interface SearchHistoryItem {
  id: string;
  queryText: string;
  creationTime: string;
  resultCount: number;
}

export function SearchHistoryTab() {
  const auth = useAuth();
  const navigate = useNavigate();
  const [history, setHistory] = useState<SearchHistoryItem[]>([]);
  const [loading, setLoading] = useState(true);

  const handleHistoryClick = (item: SearchHistoryItem) => {
    // Switch to search tab and pass query via URL
    navigate(`/student/resources?tab=search&query=${encodeURIComponent(item.queryText)}`, { replace: true });
  };

  useEffect(() => {
    if (auth.currentTenantId) {
      setTenantId(auth.currentTenantId);
    }
    loadHistory();
  }, [auth.currentTenantId]);

  const loadHistory = () => {
    if (!auth.isAuthenticated) return;
    setLoading(true);
    api.get('/api/app/search/my-search-history', {
      params: { skipCount: 0, maxResultCount: 50 }
    }).then(res => {
      setHistory(res.data || []);
      setLoading(false);
    }).catch(() => setLoading(false));
  };

  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleString('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  return (
    <>
      {loading ? (
        <div className="loading">
          <div className="loading-spinner" />
        </div>
      ) : history.length === 0 ? (
        <div className="empty-state">
          <History className="empty-state-icon" />
          <div className="empty-state-title">暂无搜索历史</div>
          <div className="empty-state-desc">切换到「资源搜索」标签页开始搜索</div>
        </div>
      ) : (
        <div className="flex flex-col gap-md">
          {history.map(item => (
            <div
              key={item.id}
              onClick={() => handleHistoryClick(item)}
              className="history-item"
            >
              <History className="history-item-icon h-5 w-5" />
              <div className="history-item-content">
                <div className="history-item-query">{item.queryText}</div>
                <div className="history-item-time">{formatDate(item.creationTime)}</div>
              </div>
              <div className="history-item-count">
                <span className="text-muted">{item.resultCount}</span>
                <span className="text-muted"> 个结果</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </>
  );
}
