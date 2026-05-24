import { useState, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Book, Star, Search, History } from 'lucide-react';
import { useAuth } from '../lib/auth';
import { ResourcesTab } from './resources/ResourcesTab';
import { FavoritesTab } from './resources/FavoritesTab';
import { SearchTab } from './resources/SearchTab';
import { SearchHistoryTab } from './resources/SearchHistoryTab';

const tabs = [
  { key: 'resources', icon: Book, label: '资源库' },
  { key: 'favorites', icon: Star, label: '我的收藏' },
  { key: 'search', icon: Search, label: '资源搜索' },
  { key: 'history', icon: History, label: '搜索历史' },
];

export function ResourcesPage() {
  const auth = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const tabFromUrl = searchParams.get('tab') || 'resources';
  const [activeTab, setActiveTab] = useState(tabFromUrl);

  // Sync tab from URL (e.g. navigating from HomePage with ?tab=search)
  useEffect(() => {
    const t = searchParams.get('tab');
    if (t && tabs.some(tab => tab.key === t)) {
      setActiveTab(t);
    }
  }, [searchParams]);

  const handleTabChange = (key: string) => {
    setActiveTab(key);
    setSearchParams({ tab: key }, { replace: true });
  };

  if (!auth.isAuthenticated) {
    return (
      <div className="student-page">
        <div className="empty-state">
          <Book className="empty-state-icon" />
          <div className="empty-state-title">请先登录</div>
          <div className="empty-state-desc">登录后可访问资源库</div>
        </div>
      </div>
    );
  }

  return (
    <div className="student-page">
      {/* Page header */}
      <div className="page-header">
        <h1 className="page-title">资源中心</h1>
        <p className="page-desc">浏览、搜索、收藏教学资源</p>
      </div>

      {/* Tab bar */}
      <div className="resource-tabs">
        {tabs.map(tab => {
          const Icon = tab.icon;
          return (
            <button
              key={tab.key}
              className={`resource-tab ${activeTab === tab.key ? 'active' : ''}`}
              onClick={() => handleTabChange(tab.key)}
            >
              <Icon className="h-4 w-4" />
              <span>{tab.label}</span>
            </button>
          );
        })}
      </div>

      {/* Tab content — always mounted, hidden via CSS to preserve state */}
      <div className="resource-tab-content">
        <div style={{ display: activeTab === 'resources' ? 'block' : 'none' }}>
          <ResourcesTab />
        </div>
        <div style={{ display: activeTab === 'favorites' ? 'block' : 'none' }}>
          <FavoritesTab />
        </div>
        <div style={{ display: activeTab === 'search' ? 'block' : 'none' }}>
          <SearchTab />
        </div>
        <div style={{ display: activeTab === 'history' ? 'block' : 'none' }}>
          <SearchHistoryTab />
        </div>
      </div>
    </div>
  );
}
