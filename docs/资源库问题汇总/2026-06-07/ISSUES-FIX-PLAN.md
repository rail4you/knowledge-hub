# 资源库问题汇总（2026-06-07 批次）— 修复计划

> 来源：`/Users/bai/Desktop/副本资源库问题汇总(2).xlsx`
> 共 29 个问题：教师及学校管理员账号 13 项 + 学生账号 16 项
> 所有图片已提取到 `./images/`，命名规则：`NN-{角色}-{模块}-{问题简述}.png`
> 图片不入 git（仓库根 `.gitignore` 未忽略 `.impeccable/`，请预览后由我手工补一条忽略规则并清理图片目录）

---

## 0. 总体概览

| # | 角色 | 模块 | 问题一句话 | 严重度 | 类型 | 截图 |
|---|------|------|-----------|-------|------|------|
| 1 | 教师 | 资源库 | 80MB PPTX 在线预览失败 | 高 | 性能/预览 | `01-teacher-资源库-PPT预览失败.png` |
| 2 | 教师 | 资源库 | 找不到"收藏夹"入口 | 中 | 导航/UI | `02-teacher-资源库-收藏夹位置.png` |
| 3 | 教师 | 实训 | 新建实训项目保存失败 | 高 | 业务校验 | `03-teacher-实训-新建无法保存.png` |
| 4 | 教师 | 实训 | 列表保存提示"至少需要配置一个实训任务" | 中 | 校验提示 | `04-teacher-实训-需要配置实训任务.png` |
| 5 | 教师 | 就业 | 上传个人简历后，资源列表可见 | 中 | 关联/数据 | `05-teacher-就业-简历资源管理.png` |
| 6 | 教师 | 就业 | 但职业规划页"选择简历资源"下拉里不显示 | 高 | 业务关联 | `06-teacher-就业-职业规划下拉.png` |
| 7 | 教师 | 就业 | 招聘直播"创建失败 / 未找到资源" | 高 | API 404 | `07-teacher-就业-直播创建失败.png` |
| 8 | 教师 | 就业 | 投递管理点击"通过/拒绝"后页面错乱 | 高 | UI 渲染/状态 | `08-teacher-就业-通过拒绝后页面.png` |
| 9 | 教师 | 双高 | 双高评估项目编辑保存失败 | 高 | 业务校验 | `09-teacher-双高-评估编辑保存失败.png` |
| 10 | 教师 | 智能体 | 创建课堂任务时需逐个选学生 | 中 | 易用性 | `10-teacher-智能体-逐个选学生.png` |
| 11 | 教师 | 课程 | 章节（PN结与MOSFET）更新失败 | 高 | API/数据 | `11-teacher-课程-章节更新失败.png` |
| 12 | 教师 | 课程 | 学习统计导出 Excel 没有"学生姓名"列 | 中 | 报表/导出 | `12-teacher-课程-学习统计导出无姓名.png` |
| 13 | 教师 | 首页 | 首页顶部"进入资源库/了解更多"按钮无反应 | 高 | 路由/点击 | `13-teacher-首页-按钮无反应.png` |
| 14 | 学生 | 微专业 | 报名失败 [403] 您未获得授权 | 高 | 权限/接口 | `14-student-微专业-报名403.png` |
| 15 | 学生 | 课程 | 章节"资源"Tab 为空 | 中 | 数据/关联 | `15-student-课程-章节资源.png` |
| 16 | 学生 | 资讯 | 资讯详情页点赞失败 [403] | 高 | 权限/接口 | `16,17-student-资讯-点赞.png` |
| 17 | 学生 | 资讯 | 评论提交失败 [403] | 高 | 权限/接口 | `18-student-资讯-评论403.png` |
| 18 | 学生 | 资讯 | 点击"写评论"按钮却跳到首页 | 高 | 路由 | `19-student-资讯-写评论按钮.png` |
| 19 | 学生 | 就业 | 学生端找不到"就业指导"入口 | 中 | 导航/角色 | `20-student-就业-学生端就业大厅.png` |
| 20 | 学生 | 课程 | 课程中心分类 Tab 点击无反应 | 高 | 交互/筛选 | `21-student-课程中心-分类无反应.png` |
| 21 | 学生 | 就业 | 学生端还能看到"岗位项目申报/立即申请" | 中 | 权限/数据 | `22-student-就业-岗位申报.png` |
| 22 | 学生 | 资源 | 学生端文档 .docx 在线预览失败 | 高 | 预览 | `23-student-资源-文档无法预览.png` |
| 23 | 学生 | 资源库 | 学生端能进入"资源管理"页（管理员页） | 高 | 权限/路由 | `24-student-资源库-学生端看到管理页.png` |
| 24 | 学生 | 课程 | 学生端能打开"新建课程"对话框 | 高 | 权限/路由 | `25-student-课程管理-学生端新建课程.png` |
| 25 | 学生 | 智能体 | 任务上下文"章节 44 知识点 0" — 不知如何添加知识点 | 中 | 易用性 | `26-student-智能体-知识点入口.png` |
| 26 | 学生 | 知识图谱 | 节点全部叠在一起 | 中 | 渲染 | `27-student-知识图谱-节点重叠.png` |
| 27 | 学生 | 课程 | 相关课程卡片上的"0 入门"含义不明 | 低 | 文案/空态 | `28-student-课程-0入口含义.png` |
| 28 | 学生 | 我的课程 | 课程卡片"0人学习 0章节" | 高 | 数据聚合 | `29-student-我的课程-数据0.png` |
| 29 | 学生 | 我的课程 | 课程学习进度全部是 5% | 中 | 数据计算 | `30-student-我的课程-进度都是5%.png` |
| 30 | 学生 | 资源库 | mp4 视频无法在线预览 | 高 | 预览/视频 | `31-student-资源库-视频无法预览.png` |
| 31 | 学生 | 首页 | 学习仪表板"最近学习"显示 Course 3a219eac 等 ID | 中 | 文案/可读性 | `32-student-首页-学习仪表板.png` |

> 说明：表里"30 / 31"是因为表里只有 29 条用户反馈，但有些条目配了 2 张图（如"点赞失败"前一张是按钮位置、后一张是错误页），方便定位。

---

## 1. 共同根因速览（先做这些能消除一半问题）

1. **权限策略过严或缺失**
   - 影响 #14, #16, #17, #18（部分）, #23, #24, #21
   - 这些 403 多数是因为 AppService 上 `Authorize` 用错了 permission group，或学生角色没继承相关权限。统一过一遍  `KnowledgeHubPermissionDefinitionProvider` 即可。

2. **前端路由没有按角色守卫**
   - 影响 #23, #24：学生端通过直接输入 URL 进入了管理员路由。`app.routes.ts` 中的 `loadChildren` 应统一走 `canActivate: [authGuard, roleGuard]`。

3. **预览/播放器缺少后端转换层**
   - 影响 #1, #22, #30：PPTX/DOCX/MP4 体积大，浏览器原生无法预览或转码失败。需要在后端加 `libreoffice`/`ffmpeg` 转换服务 + 缩略图缓存。

4. **学习统计的聚合 SQL/EF 查询缺关联**
   - 影响 #15, #25, #28, #29, #11, #31：这些"全是 0"或"全部 5%"的现象根因都是 `CourseAppService.GetMyCoursesAsync` / `LearningStatisticsAppService.ExportAsync` / `KnowledgeResourceAppService.GetByChapterAsync` 等查询没有正确 join / include / 过滤 Tenant。

5. **UI 状态管理散落 + 错误处理粗暴**
   - 影响 #8（投递管理"通过/拒绝"后页面错乱）：订阅没取消 + 用 `*ngIf` 时机不对导致路由组件状态串。

---

## 2. 详细修复计划

> 每项都按：**问题 → 复现路径 → 根因推测 → 修复步骤 → 验收 → 风险** 给出。
> 优先级 P0（必须修） / P1（应修） / P2（可优化）。

---

### P0-1  PPTX/DOCX 在线预览失败（#1, #22）

**截图**：`01-teacher-资源库-PPT预览失败.png`、`23-student-资源-文档无法预览.png`

**复现**：
- 教师端：进入"资源管理 → 资源列表"，点击 "八纲基础证(2).pptx"（80.95 MB）在线预览 → 弹"预览加载失败，请稍后重试"。
- 学生端：打开"学生端手册.docx"（812.47 KB），点击"在线预览"→ 同样失败。

**根因**：
1. `ResourceFileController.Preview` (src/KnowledgeHub.HttpApi/Controllers/ResourceFileController.cs:69) 直接 `return File(stream, contentType)`，把 80 MB 二进制一次性塞给浏览器。Office 文档浏览器无法原生渲染 → 触发 `<embed>`/`<iframe>` 错误。
2. 前端 `PptxViewerComponent` / `FilePreviewComponent` 只支持 PDF/图片，没有真正的 PPTX/DOCX 渲染分支。

**修复步骤**：
1. **后端**新增 `/api/resource-file/{id}/preview/converted` 端点：
   - 调用 LibreOffice（headless）将 pptx/docx 转 PDF，缓存到 FileStorage（key: `converted/{resourceId}.pdf`）。
   - 转换超时 60s，失败回退到原始 `preview` 端点。
2. **前端**`PptxViewerComponent` / `file-preview.component.ts`：
   - PPTX/DOCX 一律走 PDF 路线（`pdfjs-dist` 渲染），MP4 走 `<video>`。
   - 超过 20MB 显示"文件较大，已转 PDF，请稍候"，不直接失败。
3. **Docker**：在 `KnowledgeHub.HttpApi.Host` 镜像里装 `libreoffice-core`、`libreoffice-writer`、`libreoffice-impress`，挂 `/tmp` 卷用于临时转换。

**验收**：
- 80MB PPTX、812KB DOCX 都能在 5 秒内渲染出 PDF 首页。
- 关闭 LibreOffice 进程后请求不挂死，能回退到下载按钮。

**风险**：LibreOffice 装包大、CPU 吃紧 → 需用队列（推荐 `Hangfire`）异步转 + Redis 缓存，避免多人同时点预览把服务打挂。

---

### P0-2  招聘直播"创建失败 / 服务器上找不到所请求的资源"（#7）

**截图**：`07-teacher-就业-直播创建失败.png`

**复现**：就业 → 招聘直播 → 填"直播标题""计划时间"→ 创建 → toast "创建失败" + 弹窗 "未找到资源!"。

**根因**：后端在 `RecruitmentLiveAppService.CreateAsync` 中引用了一个不存在的导航属性或外键（如 `EmployerId` 必填但表单未传）。

**修复步骤**：
1. 看 `.dev/logs/api.log` 找到 404 真实路径：
   ```bash
   ./dev.sh tail api
   # 触发"创建直播"，搜 "未找到资源" 或 "RouteNotFound"
   ```
2. 在 `RecruitmentLiveController` / `RecruitmentLiveAppService` 中：
   - 校验 `EmployerId` 可空时使用当前用户所属企业。
   - 检查 DTO 上是否有 `[Required]` 但前端未传。
3. 如使用 ABP 自动路由，排查 `[RemoteService(Name=...)]` 名称拼写。

**验收**：直播能成功创建并出现在列表中；toast 提示"创建成功"。

---

### P0-3  投递管理点击"通过/拒绝"后页面错乱（#8）

**截图**：`08-teacher-就业-通过拒绝后页面.png`

**复现**：投递管理列表 → 点击某行"通过"或"拒绝" → 整个主区域变白/只显示数字和 Loading 图标。

**根因**：
- `ApplicationReviewComponent` 行为：在 `onApprove/onReject` 后 `*ngIf` 切换太快，loading spinner 替换了表格；
- 或者 `JobApplication.Status` 字段更新后，前端订阅未 unsubscribe，组件 OnDestroy 后又触发一次 `setState`。

**修复步骤**：
1. `employment/my-applications.component.ts`：把"通过/拒绝"按钮的 loading 状态用 row-level spinner 替代全屏 spinner。
2. 把列表数据用 `signal` 而非 `BehaviorSubject`，避免变更检测重复触发。
3. OnDestroy 时 `takeUntil(destroy$)` 收尾所有 RxJS。

**验收**：点击后行内显示"已通过/已拒绝"角标，列表结构稳定，不闪烁。

---

### P0-4  双高评估项目编辑保存失败（#9）

**截图**：`09-teacher-双高-评估编辑保存失败.png`

**复现**：双高评估 → 评估管理 → 编辑项目 → 改任意字段 → 保存 → toast "保存失败"。

**根因**：
- "指标配置"区使用了 `FormArray`，提交时 JSON 序列化丢失 `null` 字段；
- 后端 `DoubleHighProjectAppService.UpdateAsync` 必填字段 `BatchCode` 没做兼容（编辑态允许为空）。

**修复步骤**：
1. 前端 `double-high-project-detail.component.ts`：提交前对 `FormArray` 做 `markAsDirty` + `patchValue`，剔除空字符串。
2. 后端 `DoubleHighProjectAppService.UpdateAsync`：必填字段检查时跳过"未修改"场景。
3. EF 配置：`ProjectIndicators` 级联删除/更新时不要带 NULL。

**验收**：编辑项目后保存成功，刷新数据不变。

---

### P0-5  章节更新失败（#11）

**截图**：`11-teacher-课程-章节更新失败.png`

**复现**：课程 → 章节管理 → 选中"PN结与MOSFET工作原理" → 编辑 → 改描述 → 保存 → toast "更新失败"。

**根因**：
- `parentId` 字段在校验时被强制要求同 CourseId，但父章节被移动课程后子章节 parentId 没同步；
- DTO 用了 `string? parentId` 但 EF 写入时 `Guid.Empty` 被当成新对象。

**修复步骤**：
1. 后端 `ChapterAppService.UpdateAsync`：当 `parentId` 变化时校验同 CourseId 且不形成环（parent-of-self）。
2. 前端编辑章节时把 `parentId` 一并提交，缺省传 `null`。

**验收**：跨章节移动子章节、修改描述都能保存成功。

---

### P0-6  微专业报名 / 资讯点赞 / 资讯评论 [403]（#14, #16, #17）

**截图**：
- `14-student-微专业-报名403.png`
- `16-student-资讯-点赞按钮.png`、`17-student-资讯-点赞403.png`
- `18-student-资讯-评论403.png`

**复现**：学生登录后执行上述三个动作 → 报 [403] 您未获得授权。

**根因**：学生角色缺对应权限。ABP 默认会因 `[Authorize]` attribute 拒绝。常见漏洞：
- AppService 上写了 `[Authorize(KnowledgeHubPermissions.MicroMajors.Enroll)]` 但学生没继承；
- 资讯评论/点赞的 Permission 用了 `R` 角色组（管理员），而不是公共组。

**修复步骤**：
1. `KnowledgeHubPermissionDefinitionProvider` 中：
   - 资讯 `News.CreateComment`、`News.Like` 移到 **公共** 权限组（`MultiLevelPermissionProvider` 默认 student 拥有）。
   - 微专业 `MicroMajors.Enroll` 给 `Student` 角色开绿灯。
2. 资讯评论新增防刷：同一用户 10s 内只能发 1 条。
3. 点赞去重：用 `(userId, newsId)` 唯一索引（DB 层），重复点赞取消而非报错。

**验收**：学生可正常报名、点赞、写评论，接口返回 200。

---

### P0-7  点击"写评论"按钮跳到首页（#18）

**截图**：`19-student-资讯-写评论按钮.png`

**复现**：学生端 → 资讯详情 → 滚动到评论区 → 点击"写评论 0"按钮 → 跳到首页。

**根因**：
- 该按钮被错误绑定到 `[routerLink]="['/']"` 而非 `[(ngModel)]` 切换评论输入框；
- 或者 `(click)` 事件调用了 `Location.back()`，而用户从首页直接打开资讯 → 退栈到首页。

**修复步骤**：
1. `news-detail.component.html`：把"写评论"按钮的 `(click)` 改为 `toggleCommentEditor()`，不涉及路由。
2. 输入框 `@if (showEditor)` 控制显隐。
3. 提交流程复用 #17 的 Comment API。

**验收**：点击写评论，输入框平滑展开或聚焦，URL 不变。

---

### P0-8  学生端能进入资源管理 / 新建课程（#23, #24）

**截图**：`24-student-资源库-学生端看到管理页.png`、`25-student-课程管理-学生端新建课程.png`

**复现**：学生账号直接访问 `/#/resources/management`、`/#/courses/management` 都能进入管理页并能弹"新建课程"。

**根因**：
- `app.routes.ts` 的 `loadChildren` 没有挂 `canMatch: [roleGuard]`；
- 后端缺少 `ResourceAppService.List` 的 `GetListAsync` 在学生身份下的过滤；
- `CourseAppService.CreateAsync` 没做角色检查（但已加 [Authorize]，可能是 navigation 路由被静默忽略）。

**修复步骤**：
1. 前端 `app.routes.ts`：所有 `/admin/*` 路由统一加 `canMatch: [adminRoleGuard]`，未命中 → 跳 `/403`。
2. 后端 `ResourceAppService.GetListAsync`：非 admin 调用时强制 `Status = Approved AND IsPublic = true`。
3. 教学端"课程管理"路由的 `data: { roles: ['Teacher', 'Admin'] }`，学生直接 404。
4. 学生端顶栏不显示"管理"菜单（已部分实现，需补完）。

**验收**：学生访问管理 URL → 跳 403 或 404；API 端用学生 token 调用管理接口返回 403。

---

### P0-9  课程中心分类 Tab 点击无反应（#20）

**截图**：`21-student-课程中心-分类无反应.png`

**复现**：学生端课程中心 → 点击"微专业/专业群/学习中心"等分类 chip → 列表无变化。

**根因**：
- chip 是 `<a>` 标签但没绑 `(click)`，只是占位；
- 或者 queryParam 改了但 effect 没触发 refetch（`effect()` 依赖 signal 但传了普通变量）。

**修复步骤**：
1. `learning/course-list/...`：把分类 chip 改为 `<button (click)="setCategory(c.id)">`。
2. `setCategory` 中 `activeCategory.set(c.id)`，然后 `effect(() => fetch(...))` 监听该 signal。
3. 增加 URL `?category=` 同步，刷新保留状态。

**验收**：点击不同分类，列表切换并显示对应数据；URL 同步更新。

---

### P0-10 我的课程数据全是 0 / 进度都是 5%（#28, #29）

**截图**：`29-student-我的课程-数据0.png`、`30-student-我的课程-进度都是5%.png`

**复现**：学生进入"我的课程" → 每张课程卡都显示 "0 人学习 / 0 章节"、进度 5%。

**根因**：
- `MyCoursesComponent` 调用的 DTO 字段直接来自 `CourseListDto.StudentCount`/`ChapterCount`/`Progress`，但 admin 创建课程时这些字段没回填或回填为 0；
- `Progress` 的计算公式写死为 `Math.min(0.05, 1)`（占位代码），5% 永远不变。
- SQL EF 用了 `LEFT JOIN` + `WHERE`，导致 `Course.StudentCount` 实际为 null。

**修复步骤**：
1. 后端 `CourseAppService.GetMyCoursesAsync`：
   - 用 GroupBy 真实计算 `EnrollmentCount`（关联 `CourseEnrollment` 表）和 `ChapterCount`（关联 `Chapter` 表）。
   - 删除占位的 `Math.min(0.05, 1)`，改为 `finishedChapters / totalChapters`。
2. 章节数允许为 0（无章节的课程显示"—"而非"0 章节"）。
3. 修复 LEFT JOIN WHERE 问题（按 `Project Guidelines` 中"排查步骤"中提示的修复方式）。

**验收**：新建课程后，统计数字准确；进度随学习更新。

---

### P0-11 视频预览失败（#30）

**截图**：`31-student-资源库-视频无法预览.png`

**复现**：学生端资源库 → 点击视频卡片"预览" → 弹出播放器但无法播放。

**根因**：
- 视频 content-type 在部分 CDN 节点被设成 `application/octet-stream`；
- 前端 `<video>` 缺少 `playsinline`、`preload="metadata"`；
- 视频文件本身编码不被浏览器支持（如 rmvb）。

**修复步骤**：
1. 后端 `ResourceFileController.GetContentType` 已经覆盖 mp4，校验下文件实际 mime：
   ```csharp
   var ext = Path.GetExtension(...);
   if (ext == ".mp4") contentType = "video/mp4";
   ```
2. 前端播放器：
   ```html
   <video [src]="..." controls preload="metadata" playsinline></video>
   ```
3. 不支持格式 → 显示"该格式暂不支持在线预览，请下载"。

**验收**：MP4 预览正常；非 MP4 显示提示并允许下载。

---

### P1-12 收藏夹入口缺失（#2）

**截图**：`02-teacher-资源库-收藏夹位置.png`

**复现**：教师不知道在哪打开"收藏夹"。

**修复步骤**：
1. 在侧边栏"资源库"下增加子菜单"我的收藏"（已有 `student-favorites`，教师端可复用同一组件，只切换 query）。
2. 资源详情页右上角"收藏"图标点击后 toast "已加入收藏"，并提示"在我的收藏查看"。

**验收**：教师在"资源库 → 我的收藏"看到自己收藏过的资源。

---

### P1-13 简历资源不显示在职业规划下拉（#5, #6）

**截图**：`05-teacher-就业-简历资源管理.png`、`06-teacher-就业-职业规划下拉.png`

**复现**：在资源管理上传"个人简历"（联盟审核通过）→ 智能问答 → 职业规划 → 选简历下拉里没有这条。

**根因**：
- 资源管理支持任意文档，但职业规划下拉硬编码或过滤了"标签=简历"的资源；
- 上传时没有 `ResourceType = Resume` 字段。

**修复步骤**：
1. `Resource` 实体新增 `IsResume bool` 字段 + 迁移。
2. 上传"文档"类型时弹"是否作为简历使用"勾选。
3. 职业规划下拉查询：`Resource.Status = Approved AND IsResume = true AND OwnerUserId = currentUser`。
4. `GetWithDetailsAsync` 加 `.AsNoTracking().Where(r => r.IsResume)`。

**验收**：用户上传并审核通过的简历，职业规划下拉中可见。

---

### P1-14 直播"通过/拒绝"后页面错乱（#8，已在 P0-3）

---

### P1-15 实训项目保存校验体验（#3, #4）

**截图**：`03-teacher-实训-新建无法保存.png`、`04-teacher-实训-需要配置实训任务.png`

**根因**：弹"至少需要配置一个实训任务"，但表单里没有任何明显的"任务"区域。

**修复步骤**：
1. `PracticumFormComponent` 在 `FormArray` 长度为 0 时，"保存"按钮直接禁用 + tooltip 解释。
2. 表单里"任务配置"放在"基本信息"下方，并加一个明显的"+"按钮。

**验收**：用户点保存前就明白要加任务。

---

### P1-16 智能体任务——逐个选学生（#10）

**截图**：`10-teacher-智能体-逐个选学生.png`

**修复步骤**：
1. 学生选择从 `nz-select` 单选改为多选（`nzMode="multiple"`），支持搜索。
2. 顶部增加"按班级/按标签"批量选择按钮。

**验收**：老师能一次选 50 个学生，搜索"张三"快速定位。

---

### P1-17 学习统计导出无学生姓名（#12）

**截图**：`12-teacher-课程-学习统计导出无姓名.png`

**修复步骤**：
1. `LearningStatisticsAppService.ExportToExcelAsync`：在 DTO 增加 `StudentName`，从 `IdentityUser` join。
2. CSV 第一行表头加"学生姓名 / 学号"。

**验收**：导出的 Excel A 列为"学生姓名"。

---

### P1-18 首页按钮无反应（#13）

**截图**：`13-teacher-首页-按钮无反应.png`

**修复步骤**：
1. `home.component.html`：两个 CTA 按钮加 `routerLink="/resources"`、`routerLink="/news"`，并 `[queryParams]`。
2. 增加 hover/active 视觉反馈。

**验收**：点击 CTA 跳到对应模块。

---

### P1-19 学生端缺"就业指导"入口（#19）

**截图**：`20-student-就业-学生端就业大厅.png`

**修复步骤**：
1. 学生顶栏"就业"菜单增加"就业指导"子项（教师端已有"就业指导"）。
2. 直接复用 `Employment.MyGuidanceComponent`（已有）。

**验收**：学生看到"就业指导"。

---

### P1-20 岗位项目申报仍对学生可见（#21）

**截图**：`22-student-就业-岗位申报.png`

**修复步骤**：
1. 岗位列表"立即申请"按钮加角色判断，学生隐藏。
2. 后端 `ProjectApplicationAppService.CreateAsync` 加 `[Authorize(...Employer...)]`。

**验收**：学生看不到申请按钮，API 拒绝。

---

### P1-21 章节"资源"Tab 为空 / 知识点添加入口不明（#15, #25）

**截图**：`15-student-课程-章节资源.png`、`26-student-智能体-知识点入口.png`

**修复步骤**：
1. 章节"资源"Tab：后端 `ChapterAppService.GetAsync` 加载关联 `KnowledgeResources`。
2. 知识点入口：
   - 教师端：课程 → 章节管理 → 选中章节 → 右侧"知识点"列表 → "+ 新增知识点"按钮。
   - 学生端：智能体任务上下文显示"知识点 0"时，toast 提示"请联系老师添加"。

**验收**：教师能加知识点，学生任务上下文数字准确。

---

### P1-22 知识图谱节点重叠（#26）

**截图**：`27-student-知识图谱-节点重叠.png`

**修复步骤**：
1. `chapter-tree-graph.component.ts` / `mind-map-graph.component.ts` 调整 d3 force layout 参数（`forceCollide`、`forceManyBody` 强度）。
2. 默认缩放比例 +30%，节点间距 ≥ 60px。
3. 节点超过 30 个时切换到 "Cluster" 模式，按 parent 分组。

**验收**：任意 100 节点以内不重叠。

---

### P1-23 "0 入门"文案不明（#27）

**截图**：`28-student-课程-0入口含义.png`

**修复步骤**：
1. 改为图标 + tooltip："已有 X 人加入学习"。
2. 数据 0 时直接隐藏该指标。

**验收**：新手能看懂。

---

### P1-24 学习仪表板"Course 3a219eac"显示 ID（#31）

**截图**：`32-student-首页-学习仪表板.png`

**修复步骤**：
1. "最近学习" 列表调 `CourseAppService.GetByIdAsync` 取标题；或后端在 DTO 加 `CourseName` 字段。

**验收**：显示"集成电路设计"等可读名字。

---

## 3. 修复优先级排序（建议 Sprint 划分）

### Sprint 1（一周）— 必修 P0
- [ ] P0-1 预览（PPTX/DOCX 转 PDF）
- [ ] P0-3 投递管理 UI 错乱
- [ ] P0-4 双高评估保存
- [ ] P0-5 章节更新失败
- [ ] P0-6 三处 403 权限修复
- [ ] P0-7 写评论按钮路由修复
- [ ] P0-8 学生越权访问管理页
- [ ] P0-9 课程中心分类点击
- [ ] P0-10 我的课程数据
- [ ] P0-11 视频预览

### Sprint 2（一周）— 体验优化 P1
- [ ] P1-12 收藏夹入口
- [ ] P1-13 简历下拉
- [ ] P1-15 实训校验
- [ ] P1-16 智能体批量选学生
- [ ] P1-17 学习统计导出
- [ ] P1-18 首页 CTA
- [ ] P1-19 学生端就业指导
- [ ] P1-20 岗位申报越权
- [ ] P1-21 章节资源/知识点
- [ ] P1-22 知识图谱布局
- [ ] P1-23 / P1-24 文案

### P2（远期）
- 资讯评论防刷、点赞去重
- 预览服务队列化、Redis 缓存
- 通用"管理后台"路由守卫
- 报表导出异步化（大表格）

---

## 4. 跨问题（建议立刻并行推进）

| 工作 | 涉及 | 落地 |
|------|------|------|
| **统一权限矩阵** | 14, 16, 17, 21, 23, 24 | 在 `KnowledgeHubPermissionDefinitionProvider` 加注释，列清 student / teacher / admin 各自能做什么；CI 加单元测试覆盖 |
| **统一路由守卫** | 23, 24, 8 部分 | `app.routes.ts` 抽 `adminGuard` / `teacherGuard` / `studentGuard`；所有 `loadChildren` 必须挂 |
| **预览服务化** | 1, 22, 30 | 抽 `PreviewService`（后端） + `FilePreviewComponent`（前端）作为单一入口 |
| **学习数据统计服务** | 15, 25, 28, 29, 31 | 抽 `LearningStatisticsService`，所有"数字"都走它，避免散落 EF 查询 |
| **错误提示国际化** | 全部 | 把硬编码中文 toast 改为 `::ErrorMsg` localization key，便于以后扩 i18n |

---

## 5. 验证 & 回归

1. **后端单元测试**（`dotnet test`）：
   - `ChapterAppService_UpdateAsync_ShouldNotAllowSelfParent`
   - `RecruitmentLiveAppService_CreateAsync_ShouldNotThrow_ForTeacher`
   - `LearningStatisticsAppService_ExportAsync_ShouldIncludeStudentName`
2. **前端 e2e**（Playwright）：
   - `tests/e2e/issues-2026-06-07.spec.ts`：
     - 学生访问 `/courses/management` → 期望 403
     - 教师点击双高评估编辑保存 → 期望 toast "保存成功"
     - 学生点赞/评论 → 期望 200
3. **手动回归**：
   - 用教师号 / 学生号 / admin 号各跑一遍
   - 重点验证修复的 30 个场景

---

## 6. 文件 & 资源

- 原始 xlsx：`/Users/bai/Desktop/副本资源库问题汇总(2).xlsx`（**不要动**）
- 提取的图片：`./images/01-...32-...png`（**不入 git**，请预览后确认补 `.gitignore` 一行 `.impeccable/issues-*/images/`）
- 本计划：`./ISSUES-FIX-PLAN.md`
- 引用代码定位（核心）：
  - `src/KnowledgeHub.HttpApi/Controllers/ResourceFileController.cs:69`（预览）
  - `src/KnowledgeHub.Application/Resources/ResourceAppService.cs`（资源状态）
  - `src/KnowledgeHub.Application/Courses/KnowledgeResourceAppService.cs`（章节资源）
  - `src/KnowledgeHub.Application/Employment/EmploymentAppService.cs`（就业）
  - `src/KnowledgeHub.Application.Contracts/Permissions/KnowledgeHubPermissionDefinitionProvider.cs`（权限定义）
  - `angular/src/app/app.routes.ts`（前端路由）
  - `angular/src/app/shared/preview/file-preview.component.ts`（前端预览）
  - `angular/src/app/learning/my-courses/my-courses.component.ts`（我的课程）
  - `angular/src/app/learning/knowledge-graph/*.component.ts`（知识图谱）
  - `angular/src/app/recruitment-live/live-room.component.ts`（直播）

---

**请审阅**。如确认无误，我会按 Sprint 1 顺序开始落代码（先 P0-6 权限矩阵，因为这是面铺得最广、最容易集中修的）。
如有调整（例如想砍掉某些 P2、或把某项提到更靠前），告诉我即可。
