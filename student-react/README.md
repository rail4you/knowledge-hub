# KnowledgeHub Student React

独立学生端 React 预览项目，用于逐步替换现有 Angular `/student/**` 页面。

## 开发

```bash
cd student-react
npm install
npm run dev
```

默认地址：

- React: `http://localhost:3000`
- API: `https://localhost:44305`
- OIDC Client: `KnowledgeHub_App`（与 Angular 管理端共享登录态）

## 当前范围

- React + Vite + React Router
- Tailwind/shadcn 风格基础样式
- OIDC Authorization Code + PKCE 登录接入
- 后端基础数据联通：
  - `/api/app/course/published`
  - `/api/app/resource/league-approved`
  - `/api/app/news-article/published-list`

## 后端配置

本项目需要 API 放行 `http://localhost:3000`，并通过 DbMigrator 写入 OpenIddict client。

如果新增 client 后首次运行，请执行：

```bash
dotnet run --project src/KnowledgeHub.DbMigrator
```

后续如果只改前端代码，不需要重启 API。
