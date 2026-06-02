# 🎙️ 招聘直播功能 — 开发计划 & 进度追踪

> 基于 [recruitment-live](https://github.com/recruitment-live) WebRTC 视频通话系统，整合到 KnowledgeHub 平台。
> 
> **开始日期**: 2026-06-02
> **目标**: 教师端创建直播 → 分配租户内学生 → 双方进入直播间 WebRTC 双向视频通话

---

## 实施顺序

```
阶段 1 (2天)    阶段 2 (1天)    阶段 3 (1天)    阶段 4 (3天)    阶段 5-7 (2天)
Domains+EF ──► App层 ──► WebSocket ──► Angular ──► 菜单+国际化+测试
                                       前端
```

---

## 阶段 1：后端领域层（Domain + EF Core）

### 1.1 新建实体 `RecruitmentLive`

- [ ] **文件**: `src/KnowledgeHub.Domain/RecruitmentLive/RecruitmentLive.cs`
  - 字段: `Id`, `TenantId`, `Title`, `Description`, `TeacherId`, `TeacherName`, `StudentId`, `StudentName`, `RoomCode`, `Status`, `ScheduledAt`, `StartedAt`, `EndedAt`, `InterviewScheduleId`, `IsDeleted`
  - 继承 `FullAuditedAggregateRoot<Guid>`, `IMultiTenant`, `ISoftDelete`
  - 构造函数: `RecruitmentLive(Guid id, string title, Guid teacherId, string teacherName, string roomCode)`
  - 方法: `AssignStudent(Guid studentId, string studentName)`, `Start()`, `End()`, `Cancel()`

- [ ] **文件**: `src/KnowledgeHub.Domain.Shared/RecruitmentLive/RecruitmentLiveStatus.cs`
  - 枚举: `Waiting=0`, `Active=1`, `Ended=2`, `Cancelled=3`

### 1.2 EF Core 配置

- [ ] **文件**: `src/KnowledgeHub.EntityFrameworkCore/RecruitmentLive/RecruitmentLiveCfg.cs`
  - 表名 `RecruitmentLives`
  - `RoomCode` 唯一索引 (长度 10)
  - `TeacherId` 索引
  - `StudentId` 索引
  - `Status` 索引
  - `InterviewScheduleId` 弱引用（不建外键约束）

- [ ] **修改**: `src/KnowledgeHub.EntityFrameworkCore/KnowledgeHubDbContext.cs`
  - 添加 `DbSet<RecruitmentLive> RecruitmentLives`

- [ ] **修改**: `src/KnowledgeHub.EntityFrameworkCore/KnowledgeHubEntityFrameworkCoreModule.cs`
  - 注册 `IRepository<RecruitmentLive, Guid>` (ABP 自动注册，确认约定)

### 1.3 数据库迁移

- [ ] 执行: `dotnet ef migrations add AddRecruitmentLive`
- [ ] 执行: `dotnet run --project src/KnowledgeHub.DbMigrator`

---

## 阶段 2：后端应用层（Application + Contracts）

### 2.1 DTOs

- [ ] **文件**: `src/KnowledgeHub.Application.Contracts/RecruitmentLive/Dtos/RecruitmentLiveDtos.cs`
  - `RecruitmentLiveDto` — 完整 DTO（含 `CreatorUserName`, `StudentUserName`, `RoomCode` 等）
  - `CreateRecruitmentLiveDto` — 创建（Title, Description, StudentId?, ScheduledAt?）
  - `UpdateRecruitmentLiveDto` — 编辑
  - `UserBriefDto` — 用户摘要（Id, UserName, Name）
  - `PagedRecruitmentLiveRequestDto` — 分页查询

### 2.2 AppService 接口

- [ ] **文件**: `src/KnowledgeHub.Application.Contracts/RecruitmentLive/IRecruitmentLiveAppService.cs`
  ```csharp
  public interface IRecruitmentLiveAppService : IApplicationService
  {
      // 教师端
      Task<PagedResultDto<RecruitmentLiveDto>> GetTeacherLivesAsync(PagedRecruitmentLiveRequestDto input);
      Task<RecruitmentLiveDto> CreateLiveAsync(CreateRecruitmentLiveDto input);
      Task<RecruitmentLiveDto> UpdateLiveAsync(Guid id, UpdateRecruitmentLiveDto input);
      Task CancelLiveAsync(Guid id);
      Task DeleteLiveAsync(Guid id);
      Task<string> GetWebSocketTokenAsync(Guid liveId);  // 一次性 + 30秒过期
      
      // 学生端
      Task<PagedResultDto<RecruitmentLiveDto>> GetStudentLivesAsync(PagedRecruitmentLiveRequestDto input);
      
      // 通用
      Task<RecruitmentLiveDto> GetLiveAsync(Guid id);
      Task<List<UserBriefDto>> GetTenantStudentsAsync(string? filter);  // 审核新增
      
      // ICE 服务器配置
      Task<List<IceServerDto>> GetIceServersAsync();
  }
  ```

### 2.3 AppService 实现

- [ ] **文件**: `src/KnowledgeHub.Application/RecruitmentLive/RecruitmentLiveAppService.cs`
  - 创建直播: 生成6位字母数字 `RoomCode` (唯一性检查)
  - WebSocket token: AES 加密 `{liveId, userId, role, expires}` → Base64，30秒过期
  - `GetTenantStudentsAsync`: 查询当前租户下 Role=Student 的用户
  - IceServers: 从 `appsettings.json` 读取

### 2.4 权限

- [ ] **修改**: `src/KnowledgeHub.Application.Contracts/Permissions/KnowledgeHubPermissions.cs`
  - 添加 `RecruitmentLive` (父), `RecruitmentLive.Default`, `RecruitmentLive.Create`, `RecruitmentLive.Manage`

- [ ] **修改**: `src/KnowledgeHub.Application.Contracts/Permissions/KnowledgeHubPermissionDefinitionProvider.cs`
  - 注册权限组和权限项

### 2.5 配置

- [ ] **修改**: `src/KnowledgeHub.HttpApi.Host/appsettings.json`
  ```json
  "RecruitmentLive": {
    "IceServers": [
      { "Urls": ["stun:stun.l.google.com:19302"] }
    ],
    "WsTokenExpirationSeconds": 30
  }
  ```

---

## 阶段 3：WebSocket 信令服务器

### 3.1 房间模型

- [ ] **文件**: `src/KnowledgeHub.HttpApi/WebSocket/LiveRoom.cs`
  ```csharp
  class LiveRoom {
      public Guid LiveId;
      public WebSocket TeacherWs;
      public WebSocket StudentWs;
      public string TeacherUserId;
      public string StudentUserId;
  }
  ```

### 3.2 WebSocket Handler

- [ ] **文件**: `src/KnowledgeHub.HttpApi/WebSocket/RecruitmentLiveWebSocketHandler.cs`
  - 消息类型: `join-live`, `user-joined`, `user-left`, `offer`, `answer`, `ice-candidate`, `chat`, `hang-up`, `ping`, `pong`, `error`
  - Token 验证: AES 解密 + userId/liveId/过期时间 三重校验
  - 房间管理: `ConcurrentDictionary<string, LiveRoom>` (仅缓存 WebSocket 引用)
  - 状态持久化: 连接/断开时更新 `RecruitmentLive` 数据库表
  - 心跳: `ping`/`pong` 10秒间隔，超时自动结束直播
  - 消息大小限制: 16KB

### 3.3 WebSocket 路由

- [ ] **修改**: `src/KnowledgeHub.HttpApi/KnowledgeHubHttpApiModule.cs`
  - 在 `OnApplicationInitialization` 中注册 WebSocket 中间件:
    ```csharp
    app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
    app.Map("/api/recruitment-live/ws", WebSocketHandler);
    ```

### 3.4 DI 注册

- [ ] **修改**: `src/KnowledgeHub.HttpApi/KnowledgeHubHttpApiModule.cs`
  - `context.Services.AddSingleton<RecruitmentLiveWebSocketHandler>();`

---

## 阶段 4：Angular 前端

### 4.1 生成 Proxy

- [ ] 执行: `cd angular && abp generate-proxy -t ng`
  - 确认 `angular/src/app/proxy/recruitment-live/` 生成正确

### 4.2 共享服务

- [ ] **文件**: `angular/src/app/recruitment-live/recruitment-live.service.ts`
  - 状态机: `idle → connecting → waiting → signaling → connected → disconnected → ended`
  - Signals: `liveState`, `remoteStream`, `chatMessages`, `callDuration`, `micEnabled`, `camEnabled`
  - 方法: `connect(liveId, wsToken)`, `disconnect()`, `toggleMic()`, `toggleCam()`, `switchCamera()`, `sendChat(text)`, `hangUp()`
  - 连接重试: ICE 超时15秒自动重试（最多2次）
  - 浏览器兼容性检测

### 4.3 共享直播间组件

- [ ] **文件**: `angular/src/app/recruitment-live/live-room.component.ts`
  - 权限校验: 验证当前用户是否为该直播的教师/学生
  - 角色适配: 教师自动发起 offer，学生等待 offer
  - 显示: 房间码、分享链接、等待状态、对方视频、本地方画中画
  - 连接状态指示器

- [ ] **文件**: `angular/src/app/recruitment-live/live-room.component.html`
  - 借鉴 recruitment-live 的 index.html 布局
  - 等待状态: 显示房间码 + 复制链接 + 等待提示
  - 通话状态: 远程视频全屏 + 本地视频画中画 + 底部控制栏
  - 控制栏: 麦克风开关、摄像头开关、切换摄像头、聊天、挂断
  - 聊天面板: 可展开/收起

- [ ] **文件**: `angular/src/app/recruitment-live/live-room.component.scss`
  - 深色主题风格（与 recruitment-live 设计一致）
  - 响应式布局（支持移动端）

### 4.4 教师端页面

- [ ] **文件**: `angular/src/app/admin/recruitment-live/recruitment-live-management.component.ts`
  - 数据: 从 `RecruitmentLiveService` 获取教师创建的直播列表（分页）
  - 操作: 进入直播间、取消直播、删除直播

- [ ] **文件**: `angular/src/app/admin/recruitment-live/recruitment-live-management.component.html`
  - NgZorro Table: 标题、学生、状态、计划时间、操作按钮

- [ ] **文件**: `angular/src/app/admin/recruitment-live/recruitment-live-create.component.ts`
  - 表单: 标题(必填)、描述、选择学生(NgZorro Select 搜索)、计划时间(NgZorro DatePicker)

- [ ] **文件**: `angular/src/app/admin/recruitment-live/recruitment-live-create.component.html`
  - 表单布局 + 创建按钮

### 4.5 学生端页面

- [ ] **文件**: `angular/src/app/student/recruitment-live/student-recruitment-live.component.ts`
  - 数据: 从 `RecruitmentLiveService` 获取分配给自己的直播列表
  - 操作: 点击卡片进入直播间

- [ ] **文件**: `angular/src/app/student/recruitment-live/student-recruitment-live.component.html`
  - 卡片网格布局: 标题、教师、状态

### 4.6 路由注册

- [ ] **修改**: `angular/src/app/app.routes.ts`
  ```typescript
  { path: 'admin/recruitment-live', loadComponent: ... , canActivate: [authGuard, permissionGuard], data: { requiredPolicy: 'KnowledgeHub.RecruitmentLive.Create' } },
  { path: 'admin/recruitment-live/create', loadComponent: ... , canActivate: [authGuard, permissionGuard], data: { requiredPolicy: 'KnowledgeHub.RecruitmentLive.Create' } },
  { path: 'admin/recruitment-live/:id', loadComponent: ... , canActivate: [authGuard] },
  ```

- [ ] **修改**: `angular/src/app/student/student.routes.ts`
  ```typescript
  { path: 'recruitment-live', loadComponent: () => import('../recruitment-live/student-recruitment-live.component').then(m => m.StudentRecruitmentLiveComponent), data: { name: '招聘直播', icon: 'video-camera' } },
  { path: 'recruitment-live/:id', loadComponent: () => import('../recruitment-live/live-room.component').then(m => m.LiveRoomComponent) },
  ```

- [ ] **修改**: `angular/src/app/student/layout/student-layout.component.html`
  - Tab 导航添加 `<a routerLink="/student/recruitment-live" ...>招聘直播</a>`

---

## 阶段 5：菜单 & 本地化

### 5.1 教师端菜单

- [ ] **修改**: `angular/src/app/route.provider.ts`
  ```typescript
  {
    path: '/admin/recruitment-live',
    name: '::Menu:RecruitmentLive',
    iconClass: 'fas fa-video',
    parentName: '::Menu:Employment',
    layout: eLayoutType.application,
    requiredPolicy: 'KnowledgeHub.RecruitmentLive.Create',
  }
  ```

### 5.2 本地化

- [ ] **修改**: `src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/zh-Hans.json`
  - 添加 30+ 翻译键 (菜单、按钮、状态、提示)

- [ ] **修改**: `src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/en.json`
  - 英文翻译

### 5.3 权限种子

- [ ] **修改**: `src/KnowledgeHub.Domain/KnowledgeHubDataSeederContributor.cs`
  - 种子数据中添加 `RecruitmentLive` 权限

---

## 阶段 6：测试验证

- [ ] 教师创建直播（`/admin/recruitment-live/create`）
- [ ] 学生列表看到直播（`/student/recruitment-live`）
- [ ] 双方进入不同浏览器/标签页的直播间
- [ ] WebRTC 视频连通（能看到对方、听到声音）
- [ ] 麦克风开关
- [ ] 摄像头开关
- [ ] 前后摄像头切换
- [ ] 文字聊天
- [ ] 一方挂断 → 另一方正确提示
- [ ] 重新进入已结束的直播 → 正确提示
- [ ] 非参与者访问直播间 → 权限错误

---

## 阶段 7：部署检查清单

- [ ] Nginx 配置添加 WebSocket 代理支持
  ```nginx
  location /api/recruitment-live/ws {
      proxy_pass http://api:44305;
      proxy_http_version 1.1;
      proxy_set_header Upgrade $http_upgrade;
      proxy_set_header Connection "upgrade";
      proxy_read_timeout 86400s;
  }
  ```
- [ ] 确认 Kestrel 不限制 WebSocket 请求体大小
- [ ] HTTPS 配置（摄像头 API 要求安全上下文）

---

## 文件变更清单

| # | 操作 | 路径 | 阶段 |
|---|------|------|------|
| 1 | 新建 | `src/KnowledgeHub.Domain.Shared/RecruitmentLive/RecruitmentLiveStatus.cs` | 1 |
| 2 | 新建 | `src/KnowledgeHub.Domain/RecruitmentLive/RecruitmentLive.cs` | 1 |
| 3 | 新建 | `src/KnowledgeHub.EntityFrameworkCore/RecruitmentLive/RecruitmentLiveCfg.cs` | 1 |
| 4 | 修改 | `src/KnowledgeHub.EntityFrameworkCore/KnowledgeHubDbContext.cs` | 1 |
| 5 | 新建 | `src/KnowledgeHub.Application.Contracts/RecruitmentLive/Dtos/RecruitmentLiveDtos.cs` | 2 |
| 6 | 新建 | `src/KnowledgeHub.Application.Contracts/RecruitmentLive/IRecruitmentLiveAppService.cs` | 2 |
| 7 | 新建 | `src/KnowledgeHub.Application/RecruitmentLive/RecruitmentLiveAppService.cs` | 2 |
| 8 | 修改 | `src/KnowledgeHub.Application.Contracts/Permissions/KnowledgeHubPermissions.cs` | 2 |
| 9 | 修改 | `src/KnowledgeHub.Application.Contracts/Permissions/KnowledgeHubPermissionDefinitionProvider.cs` | 2 |
| 10 | 修改 | `src/KnowledgeHub.HttpApi.Host/appsettings.json` | 2 |
| 11 | 新建 | `src/KnowledgeHub.HttpApi/WebSocket/LiveRoom.cs` | 3 |
| 12 | 新建 | `src/KnowledgeHub.HttpApi/WebSocket/RecruitmentLiveWebSocketHandler.cs` | 3 |
| 13 | 修改 | `src/KnowledgeHub.HttpApi/KnowledgeHubHttpApiModule.cs` | 3 |
| 14 | 新建 | `angular/src/app/recruitment-live/recruitment-live.service.ts` | 4 |
| 15 | 新建 | `angular/src/app/recruitment-live/live-room.component.ts` | 4 |
| 16 | 新建 | `angular/src/app/recruitment-live/live-room.component.html` | 4 |
| 17 | 新建 | `angular/src/app/recruitment-live/live-room.component.scss` | 4 |
| 18 | 新建 | `angular/src/app/admin/recruitment-live/recruitment-live-management.component.ts` | 4 |
| 19 | 新建 | `angular/src/app/admin/recruitment-live/recruitment-live-management.component.html` | 4 |
| 20 | 新建 | `angular/src/app/admin/recruitment-live/recruitment-live-create.component.ts` | 4 |
| 21 | 新建 | `angular/src/app/admin/recruitment-live/recruitment-live-create.component.html` | 4 |
| 22 | 新建 | `angular/src/app/student/recruitment-live/student-recruitment-live.component.ts` | 4 |
| 23 | 新建 | `angular/src/app/student/recruitment-live/student-recruitment-live.component.html` | 4 |
| 24 | 修改 | `angular/src/app/app.routes.ts` | 4 |
| 25 | 修改 | `angular/src/app/student/student.routes.ts` | 4 |
| 26 | 修改 | `angular/src/app/student/layout/student-layout.component.html` | 4 |
| 27 | 修改 | `angular/src/app/route.provider.ts` | 5 |
| 28 | 修改 | `src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/zh-Hans.json` | 5 |
| 29 | 修改 | `src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/en.json` | 5 |
| 30 | 修改 | `src/KnowledgeHub.Domain/KnowledgeHubDataSeederContributor.cs` | 5 |
| 31 | 新建 | `src/KnowledgeHub.EntityFrameworkCore/Migrations/*_AddRecruitmentLive.cs` | 1 |

---

## 进度

| 阶段 | 状态 | 完成时间 | 备注 |
|------|------|----------|------|
| 阶段 1 | ✅ 已完成 | 2026-06-02 | Domain + EF Core + Migration |
| 阶段 2 | ✅ 已完成 | 2026-06-02 | Application + Contracts + Permissions |
| 阶段 3 | ✅ 已完成 | 2026-06-02 | WebSocket 信令服务器 |
| 阶段 4 | ✅ 已完成 | 2026-06-02 | Angular 前端（所有组件、路由） |
| 阶段 5 | ✅ 已完成 | 2026-06-02 | 菜单 + 本地化 + 配置 |
| 阶段 6 | ⬜ 待测试 | - | 端到端测试验证 |
| 阶段 7 | ⬜ 待执行 | - | 部署检查清单 |
