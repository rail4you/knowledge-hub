# 修复代码计划方案

本计划基于 `issues/*.md` 中的问题清单生成，只给出实施方案，不直接改代码。目标是先按风险和影响面分批修复，避免同时改动过多模块。

## 当前整改进度

更新时间：`2026-04-27`

### 已完成

- 管理员 `2`：角色管理页新建角色已改为预设下拉，不再手工输入角色名。
- 管理员 `4`：用户编辑弹窗已合并为一个租户选择，角色区跟随所属租户自动刷新。
- 管理员 `5`、`17`：登录页租户切换已改为直接列表展示，不再通过按钮弹窗或手动输入。
- 管理员 `6`：PPTX 预览已替换为按幻灯片分页的 canvas 渲染，支持翻页、跳页和缩放。
- 管理员 `2`（用户侧）：用户编辑弹窗中的角色选择已改为单选下拉，不再多选角色。
- 管理员 `8`：搜索索引英文显示已改为中文标签展示。
- AI 页面资源列表刷新链路已修复，`OnPush` 页面现在会在资源接口返回后立即显示资源。
- 管理员 `19`：文档查看页长文本溢出已加换行与容器约束样式。
- 管理员 `23`：高频入口文案已从“知识库系统”调整为“资源库系统”。
- 教师 `3`：学生门户已加学生角色限制，教师不能直接进入。
- 学生 `1`：学生登录后默认跳转学生门户。
- 学生 `4`：学生门户已新增“我的收藏”导航和独立页面。
- 学生 `6`、`7`、`8`：学生课程详情新增学生路由，隐藏知识图谱、习题管理等后台操作。
- 学生 `9`：用户菜单不再渲染 signal 函数源码。
- 学生 `10`：AI 聊天输入框不再显示原始模板语法，侧栏按钮语义已改清晰。
- 学生 `10`：学生端文档问答回复已收敛为只显示最终答案，不再暴露工具调用、思考过程或折叠中间内容。

### 部分完成

- 管理员 `1`：权限弹窗中一批英文权限名已本地化，仍需全量验收所有权限组。
- 管理员 `3`：前端已将角色名称改为只读，先规避重命名报错；如需支持正式改名，后端仍需补校验。
- 管理员 `14`、`15`：AI 页左上 `+` 已明确为“新建对话”，但“上传新资源”入口仍未补。
- 管理员 `18`、`22`：前端生产环境默认 API 地址已不再写死 `localhost`，但仍需结合部署配置验证。
- 学生 `2`：预览失败提示已改中文，但预览失败根因尚未全部确认。
- 学生 `3`：下载链路未改后端授权，仍需联调验证。
- 管理员 `16`：前端已进一步收敛为单角色单选；后端仍建议补最终校验。
- 教师 `4`：资料页已移除会触发 `RoleType invalid` 的前端扩展字段，待服务重启后复测完整提交链路。
- 学生 `4`：收藏接口已补后端方法，当前前端先通过本地 `RestService` 封装接入，待 API 重启后重新生成 proxy 固化。

### 未开始

- 管理员 `4`、`5`、`17`
- 管理员 `6`
- 管理员 `9`、`10`、`11`、`12`、`13`
- 管理员 `20`、`21`
- 教师 `1`、`2`
- 学生 `5`、`11`

### 本轮已验证

- `angular` TypeScript 编译通过：

```bash
cd angular
npx tsc -p tsconfig.app.json --noEmit
```

- 后端 `Application` 项目编译通过（存在仓库既有 warning，无新增 error）：

```bash
dotnet build src/KnowledgeHub.Application/KnowledgeHub.Application.csproj
```

### 本轮涉及的主要代码文件

- `angular/src/app/app.routes.ts`
- `angular/src/app/home/*`
- `angular/src/app/student/layout/*`
- `angular/src/app/student/favorites/*`
- `angular/src/app/student/resource-collection.service.ts`
- `angular/src/app/student/student-portal.guard.ts`
- `angular/src/app/auth/current-user.utils.ts`
- `angular/src/app/account-form-prop-contributors.ts`
- `angular/src/app/learning/my-courses/*`
- `angular/src/app/learning/course-detail/*`
- `angular/src/app/learning/exercise-learning/exercise-learning.component.ts`
- `angular/src/app/search/search-history/search-history.component.ts`
- `angular/src/app/search/search.component.ts`
- `angular/src/app/ai/chat/*`
- `angular/src/app/shared/preview/file-preview.component.ts`
- `angular/src/app/document-viewer/document-viewer.component.ts`
- `angular/src/app/admin/identity-roles/identity-roles.component.ts`
- `angular/src/app/admin/identity-users/*`
- `angular/src/environments/environment.prod.ts`
- `src/KnowledgeHub.Application.Contracts/Resources/IResourceAppService.cs`
- `src/KnowledgeHub.Application/Resources/ResourceAppService.cs`
- `src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/*.json`

## 总体原则

- 先修“路由/环境/权限”这类会导致页面报错或角色越权的问题。
- 再修“搜索/预览/下载/索引”这类核心业务链路问题。
- 最后处理“本地化、术语统一、交互优化、样式溢出”等体验问题。
- 尽量沿用现有 ABP 结构：
  - Angular 路由：`angular/src/app/app.routes.ts`
  - Angular 菜单：`angular/src/app/route.provider.ts`
  - 应用契约：`src/KnowledgeHub.Application.Contracts`
  - 实现：`src/KnowledgeHub.Application`
  - 手写控制器：`src/KnowledgeHub.HttpApi/Controllers`

## 阶段 1：环境稳定性与错误基址

状态：`部分完成`

### 目标

- 修复线上仍访问 `localhost` 的问题。
- 消除“重新搜索后报错”“章节管理直接报错”“页面随机错误/404”等全局性故障。

### 涉及问题

- 管理员 18
- 管理员 22
- 学生 11

### 计划动作

1. 检查安装状态初始化逻辑是否硬编码 fallback 到 `https://localhost`。
2. 核对前端环境配置、`dynamic-env.json`、Docker 生产环境注入值。
3. 检查学生门户和后台路由刷新时的 fallback 配置。
4. 为初始化请求失败增加更可读的错误提示，而不是直接白屏。

### 重点文件

- `angular/src/app/install/install.initializer.ts`
- `angular/src/app/app.config.ts`
- `etc/docker/.env.production`
- 生产环境 `dynamic-env.json`

### 验证

- 线上环境打开任意受保护路由时，不再请求 `localhost`。
- 搜索历史“重新搜索”可正常跳转。
- 直达学生门户和学习页面刷新后不再 404。

## 阶段 2：角色与路由访问控制

状态：`进行中`

### 目标

- 学生只看学生门户，教师不进入学生门户。
- 学生不再看到管理员/教师操作入口。
- 教师只操作自己租户和授权范围的数据。

### 涉及问题

- 教师 1
- 教师 3
- 学生 1
- 学生 6
- 学生 7
- 学生 8

### 计划动作

1. 给学生门户入口和路由增加基于角色的前端保护。
2. 登录后按角色决定默认落地页。
3. 把课程详情页拆分为“学生视图”和“管理视图”，或在页面内部按角色裁剪操作区。
4. 教师端学生选择接口固定在当前租户，不暴露跨租户选择 UI。

### 重点文件

- `angular/src/app/home/home.component.ts`
- `angular/src/app/home/home.component.html`
- `angular/src/app/app.routes.ts`
- `angular/src/app/student/layout/student-layout.component.ts`
- `angular/src/app/student/layout/student-layout.component.html`
- `angular/src/app/learning/course-detail/course-detail.component.ts`
- `angular/src/app/learning/my-courses/my-courses.component.ts`
- `angular/src/app/learning/student-enrollment/student-enrollment.component.ts`
- `src/KnowledgeHub.Application/Courses/StudentCourseAppService.cs`

### 验证

- 学生登录后默认进入 `/student/resources` 或学生门户首页。
- 教师登录后不显示学生门户入口。
- 学生课程详情页不再出现“创建习题”“知识图谱”“后台左侧导航”等管理项。
- 教师选课页面只能看到当前租户学生。

## 阶段 3：权限、角色与资料编辑

状态：`进行中`

### 目标

- 角色管理页不再出现英文权限。
- 角色编辑/重命名稳定。
- 用户角色互斥规则明确。
- 资料编辑不再出现 `RoleType invalid`。

### 涉及问题

- 管理员 1
- 管理员 2
- 管理员 3
- 管理员 16
- 教师 4

### 计划动作

1. 优先改权限显示名映射，缺失项改走本地化资源。
2. 排查角色更新接口在租户切换、并发戳、唯一性校验上的问题。
3. 在用户角色选择中加入互斥规则，例如学生/教师不可同时选。
4. 对资料页和用户编辑页提交模型做对齐，确保 `RoleType` 不会传无效值。

### 重点文件

- `angular/src/app/admin/identity-roles/identity-roles.component.ts`
- `angular/src/app/admin/identity-users/identity-users.component.ts`
- `angular/src/app/identity-form-prop-contributors.ts`
- `src/KnowledgeHub.Application/Identity/TenantRoleAppService.cs`
- `src/KnowledgeHub.Application/Identity/TenantUserAppService.cs`
- `src/KnowledgeHub.Application.Contracts/Permissions/KnowledgeHubPermissionDefinitionProvider.cs`
- `src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/*.json`

### 验证

- 权限弹窗全部显示中文。
- 编辑角色名称可正常保存。
- 教师资料页保存成功。
- 用户创建/编辑时不允许互斥角色同时存在。

## 阶段 4：资源预览、下载与收藏

状态：`进行中`

### 目标

- 学生可正常预览、下载资源。
- 预览失败提示改为中文。
- 增加“我的收藏”入口和页面。
- PPT 预览精度得到可接受的改进。

### 涉及问题

- 管理员 6
- 管理员 7
- 学生 2
- 学生 3
- 学生 4

### 计划动作

1. 排查 `download` / `getFileUrl` 授权与学生权限配置是否匹配。
2. 修正 `file-preview` 中英文错误文案和调试提示。
3. 评估 PPTX 预览渲染链路，必要时改为 PDF 化。
4. 在学生门户新增“我的收藏”路由，复用现有收藏接口。
5. 补一个收藏列表接口，避免学生端只能收藏不能回看。

### 重点文件

- `angular/src/app/shared/preview/file-preview.component.ts`
- `angular/src/app/shared/preview/file-preview.component.html`
- `angular/src/app/shared/preview/pptx-viewer.component.ts`
- `angular/src/app/student/resources/student-resources.component.ts`
- `angular/src/app/student/layout/student-layout.component.html`
- `src/KnowledgeHub.Application/Resources/ResourceAppService.cs`

### 验证

- 学生可预览 PDF / Word / PPT / 文本资源。
- 学生下载资源成功。
- 学生可查看自己的收藏资源。
- 预览失败时显示中文友好提示。

## 阶段 5：搜索与索引链路

状态：`部分完成`

### 目标

- 搜索结果与资源库数据一致。
- 上传新资源后可以被搜索到。
- 搜索相关页面全部本地化。

### 涉及问题

- 管理员 8
- 管理员 21
- 学生 5

### 计划动作

1. 核对资源上传后是否正确创建索引任务。
2. 检查搜索页默认索引、租户过滤和索引名称映射。
3. 为 `documents/videos` 提供中文显示层。
4. 补上“索引失败/未完成”的后台可观测性。

### 重点文件

- `angular/src/app/search/search.component.ts`
- `angular/src/app/search/search.service.ts`
- `angular/src/app/admin/indexing-jobs/indexing-jobs.component.ts`
- `src/KnowledgeHub.Application/Resources/ResourceAppService.cs`
- `src/KnowledgeHub.Application/Search/IndexingJobAppService.cs`
- `src/KnowledgeHub.Application/Search/MeiliSearchService.cs`

### 验证

- 搜索“力学”可以命中已上传资源。
- 搜索页索引下拉显示中文。
- 后台能看到索引任务状态和失败原因。

## 阶段 6：课程、章节、习题与导入

状态：`未开始`

### 目标

- 章节删除、章节导入、习题导入稳定。
- 课程在管理员/教师视角下可见性规则统一。
- 表单“明明填了仍提示必填”的问题消失。

### 涉及问题

- 管理员 9
- 管理员 10
- 管理员 11
- 管理员 12
- 管理员 13
- 教师 2

### 计划动作

1. 统一章节模板和习题模板的格式约定。
2. 后端导入接口返回逐行错误明细，而不是只给总失败。
3. 章节删除前补充子节点、关联资源、关联习题的依赖清理。
4. 明确课程可见性规则，教师端查询不要只依赖学生选课记录。
5. 对比前端表单和 DTO 必填项，消除隐藏字段导致的误报。

### 重点文件

- `angular/src/app/learning/chapter-management/chapter-management.component.ts`
- `angular/src/app/admin/exercise/exercise-management.component.ts`
- `angular/src/app/learning/exercise-management/exercise-management.component.ts`
- `angular/src/app/learning/student-enrollment/student-enrollment.component.ts`
- `src/KnowledgeHub.Application/Courses/ChapterAppService.cs`
- `src/KnowledgeHub.Application/Exams/ExerciseAppService.cs`
- `src/KnowledgeHub.Application/Courses/CourseAppService.cs`
- `src/KnowledgeHub.Application/Courses/StudentCourseAppService.cs`

### 验证

- 模板导入成功率恢复，失败时可定位到具体行。
- 删除章节时有明确提示，且不会异常中断。
- 教师端能看到符合规则的课程数据。
- 学生姓名正常显示。

## 阶段 7：AI 页面与交互清理

状态：`进行中`

### 目标

- AI 页面不再露出模板表达式、内部标记和调试痕迹。
- “新增文档 / 新建对话”语义清晰。

### 涉及问题

- 管理员 14
- 管理员 15
- 学生 9
- 学生 10

### 计划动作

1. 修复 signal 在模板里的错误使用，确保显示的是 `userName()` 而不是函数本体。
2. 修复 placeholder 中直接写模板控制流导致的原样输出。
3. 清理 AI 响应中不适合展示的内部标记/红色代码样式。
4. 调整文档问答页左侧 `+` 图标语义，避免被误解为上传文档。

### 重点文件

- `angular/src/app/student/layout/student-layout.component.ts`
- `angular/src/app/student/layout/student-layout.component.html`
- `angular/src/app/ai/chat/chat.component.ts`
- `angular/src/app/ai/chat/chat.component.html`
- `angular/src/app/ai/chat/chat.component.scss`
- `angular/src/app/ai/lesson-plan/lesson-plan.component.html`

### 验证

- 用户菜单显示正常用户名。
- 聊天输入框不再显示 `@if (...)` 这类原始模板语法。
- AI 回复中不再暴露内部标记或不合理的红色强调。
- AI 页面按钮语义清晰。

## 阶段 8：术语统一、本地化与样式细节

状态：`进行中`

### 目标

- 全站用户可见术语从“知识库”统一为“资源库”。
- 英文环境不再夹杂中文，中文环境不再夹杂英文。
- 文本溢出和布局细节收口。

### 涉及问题

- 管理员 19
- 管理员 20
- 管理员 23

### 计划动作

1. 将用户可见文案统一替换为“资源库”，不改后端命名空间。
2. 补齐 `en.json` 与 `zh-Hans.json`，替换页面中写死文案。
3. 对文档正文、长文本、表格列做统一溢出策略。

### 重点文件

- `angular/src/app/home/home.component.html`
- `angular/src/app/login/login.component.html`
- `angular/src/app/student/layout/student-layout.component.html`
- `angular/src/app/document-viewer/document-viewer.component.ts`
- `src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/en.json`
- `src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/zh-Hans.json`

### 验证

- 首页、登录页、学生门户统一显示“资源库”。
- 英文环境文案完整。
- 长文本不再横向溢出。

## 建议执行顺序

1. 阶段 1：环境稳定性与错误基址
2. 阶段 2：角色与路由访问控制
3. 阶段 3：权限、角色与资料编辑
4. 阶段 4：资源预览、下载与收藏
5. 阶段 5：搜索与索引链路
6. 阶段 6：课程、章节、习题与导入
7. 阶段 7：AI 页面与交互清理
8. 阶段 8：术语统一、本地化与样式细节

## 执行前建议确认项

- 学生是否允许下载全部已审核资源，还是仅允许预览。
- 教师应该看到“本租户全部课程”，还是“分配给自己的课程”。
- “资源库”是否只改前端展示文案，还是连菜单 key 也同步重命名。
- AI 页面左侧 `+` 按钮的真实产品意图是“新建对话”还是“新增文档”。

确认以上文档后，再进入代码修改阶段最稳妥。
