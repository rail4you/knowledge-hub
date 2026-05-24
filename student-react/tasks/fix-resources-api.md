# 任务计划：修复学生门户资源库数据问题

## ✅ 已完成的修复

### 1. 添加 Bearer Token 拦截器
**文件**: `src/lib/api.ts`

```typescript
// 导入 token 获取函数
import { getAccessTokenFromStorage } from './auth';

// Request interceptor to auto-attach Bearer token
api.interceptors.request.use(async (config) => {
  try {
    const token = await getAccessTokenFromStorage();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  } catch (e) {
    // Silently ignore token errors
  }
  return config;
}, (error) => Promise.reject(error));
```

### 2. 添加独立 Token 获取函数
**文件**: `src/lib/auth.tsx`

```typescript
export async function getAccessTokenFromStorage(): Promise<string | null> {
  try {
    const userManager = createUserManager();
    const user = await userManager.getUser();
    if (user && !user.expired) {
      return user.access_token;
    }
    return null;
  } catch (e) {
    return null;
  }
}
```

### 3. 添加资源状态过滤
**文件**: `src/pages/ResourcesPage.tsx`

```typescript
// Filter by LeagueApproved resources (status: 3)
params.status = 3;
```

---

## 📋 待修复的其他页面

| 页面 | 文件 | 需要检查的 API |
|------|------|---------------|
| 资讯中心 | `NewsPage.tsx` | `/api/app/news/published`, `/api/app/news/hot` |
| 我的收藏 | `FavoritesPage.tsx` | `/api/app/resource-collection/items` |
| 搜索 | `SearchPage.tsx` | `/api/app/search/search` |
| 搜索历史 | `SearchHistoryPage.tsx` | `/api/app/search/my-search-history` |
| 课堂任务 | `AgentTasksPage.tsx` | `/api/app/teaching-agent/student-assignments` |

---

## 验证清单

- [x] API 请求携带 Bearer token（通过拦截器）
- [x] 资源列表添加 status 参数过滤
- [ ] 各页面数据正常显示
- [ ] 收藏功能正常工作
- [ ] 搜索功能正常工作
- [ ] 搜索历史正常工作

---

## 测试步骤

1. 登录 zmq/123456 (qidi 租户)
2. 访问 /student/resources 检查资源列表
3. 检查控制台无 401/403 错误
4. 测试收藏、预览、下载功能