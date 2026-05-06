# 管理员账号问题清单

## 1. 选择权限出现英文

- 原始描述：选择权限出现英文
- 截图：
  - [截图 1](./assets/管理员账号-01-1.png)
  - [截图 2](./assets/管理员账号-01-2.png)
- 图片描述：角色权限弹窗里权限组和子权限仍出现 `Alliance`、`Create`、`Update`、`Delete`、`ManageMembers` 等英文。
- 建议修复：补齐权限组和权限项显示名映射，优先走本地化资源，不要直接回退到权限代码名。
- 初步关联代码：
  - `angular/src/app/admin/identity-roles/identity-roles.component.ts`
  - `src/KnowledgeHub.Application.Contracts/Permissions/KnowledgeHubPermissionDefinitionProvider.cs`
  - `src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/*.json`

## 2. 角色改为选择的方式

- 原始描述：角色改为选择的方式
- 截图：[截图 1](./assets/管理员账号-02-1.png)
- 图片描述：界面中的角色相关输入看起来仍是自由输入或不明确的编辑方式，用户期望改为下拉选择。
- 建议修复：统一角色选择交互，禁止手工输入角色名；新增/编辑用户时改成多选下拉或标签选择。
- 初步关联代码：
  - `angular/src/app/admin/identity-users/identity-users.component.ts`
  - `angular/src/app/custom-identity-user.service.ts`
  - `src/KnowledgeHub.Application/Identity/TenantUserAppService.cs`
- 修复状态：`已完成（2026-04-26）`
- 本次落地：
  - `http://localhost:4200/identity/roles` 的新建角色弹窗已把角色名称改成预设下拉。
  - `http://localhost:4200/identity/users` 的用户角色选择已改成单选下拉，不再多选。
  - 详情见 [管理端问题修复记录](./admin-fix-log.md) 的“修复 01”。

## 3. 更改已有角色的名称会发生错误

- 原始描述：更改已有角色的名称会发生错误
- 截图：[截图 1](./assets/管理员账号-03-1.png)
- 图片描述：编辑角色后提交失败，弹窗提示请求异常。
- 建议修复：排查角色重命名的服务端校验、租户上下文和并发戳处理；补充前端错误提示。
- 初步关联代码：
  - `angular/src/app/admin/identity-roles/identity-role.service.ts`
  - `src/KnowledgeHub.Application/Identity/TenantRoleAppService.cs`
- 修复状态：`部分完成（2026-04-26）`
- 本次落地：
  - 角色编辑弹窗中的名称字段已改为只读，先避免管理员继续触发重命名报错。
  - 如果后续要正式支持角色改名，仍需补后端接口校验与错误处理。

## 4. 要选择两次租户，有些累赘

- 原始描述：要选择两次租户，有些累赘
- 截图：[截图 1](./assets/管理员账号-04-1.png)
- 图片描述：同一流程里租户既作为筛选项出现，又在表单中再次选择，交互重复。
- 建议修复：当页面已选定租户时，表单默认继承该值；仅 host 需要可切换，租户管理员不重复选择。
- 初步关联代码：
  - `angular/src/app/admin/identity-roles/identity-roles.component.ts`
  - `angular/src/app/admin/identity-users/identity-users.component.ts`
- 修复状态：`已完成（2026-04-26）`
- 本次落地：
  - `http://localhost:4200/identity/users` 已只保留一个“所属租户”菜单。
  - 角色区会跟随该租户选择自动刷新。
  - 详情见 [管理端问题修复记录](./admin-fix-log.md) 的“修复 02”。

## 5. 租户切换也改为选择的方式吧

- 原始描述：租户切换也改为选择的方式吧
- 截图：[截图 1](./assets/管理员账号-05-1.png)
- 图片描述：租户切换弹窗要求手输租户名称，不利于使用且容易输错。
- 建议修复：把租户切换改成可搜索下拉框或租户列表选择，而不是纯文本输入。
- 初步关联代码：
  - ABP 账户/租户切换入口
  - `angular/src/app/app.config.ts`
  - 如需本地替换，需新增自定义 tenant switch 组件
- 修复状态：`已完成（2026-04-26）`
- 本次落地：
  - `https://localhost:44305/Account/Login` 登录页已直接显示租户列表切换，不再先点按钮。
  - 详情见 [管理端问题修复记录](./admin-fix-log.md) 的“修复 03”。

## 6. PPT 预览与实际文件有较大出入

- 原始描述：PPT预览与实际文件有较大出入
- 截图：[截图 1](./assets/管理员账号-06-1.png)
- 图片描述：PPT 预览内容排版、字号或换行与源文件差异明显。
- 建议修复：重新评估前端 PPTX 渲染方案；若当前纯前端解析精度不够，优先考虑转 PDF 后预览或引入更稳定的 Office 渲染链路。
- 初步关联代码：
  - `angular/src/app/shared/preview/pptx-viewer.component.ts`
  - `angular/src/app/shared/preview/file-preview.component.ts`
- 修复状态：`已完成（2026-04-26）`
- 本次落地：
  - `/resources` 的 PPTX 预览已改为专门的前端 PPTX 幻灯片渲染方案，支持翻页、页码跳转和缩放。
  - 详情见 [管理端问题修复记录](./admin-fix-log.md) 的“修复 05”。

## 7. 不要出现这种代码之类的文字

- 原始描述：不要出现这种代码之类的文字
- 截图：[截图 1](./assets/管理员账号-07-1.png)
- 图片描述：页面底部或空态区域出现面向开发/调试的说明文字，不适合最终用户。
- 建议修复：移除调试提示、技术术语和未本地化的错误文案，仅保留用户可理解的提示。
- 初步关联代码：
  - `angular/src/app/shared/preview/file-preview.component.html`
  - `angular/src/app/shared/preview/file-preview.component.ts`

## 8. 出现英文

- 原始描述：出现英文
- 截图：[截图 1](./assets/管理员账号-08-1.png)
- 图片描述：搜索页“搜索索引”下拉选项显示 `documents`、`videos` 等英文。
- 建议修复：索引名称增加中文展示层，保留内部值但显示为“文档”“视频”等本地化文案。
- 初步关联代码：
  - `angular/src/app/search/search.component.ts`
  - `angular/src/app/search/search.service.ts`

## 9. 习题使用模板导入失败

- 原始描述：习题使用模板导入失败
- 截图：[截图 1](./assets/管理员账号-09-1.png)
- 图片描述：使用模板导入习题时报错，说明模板字段与后端解析规则不一致，或校验信息不清晰。
- 建议修复：统一模板列定义、后端导入解析和前端导入说明；返回逐行错误信息。
- 初步关联代码：
  - `angular/src/app/admin/exercise/exercise-management.component.ts`
  - `angular/src/app/learning/exercise-management/exercise-management.component.ts`
  - `src/KnowledgeHub.Application/Exams/ExerciseAppService.cs`

## 10. 必填项都已填写，仍需填写

- 原始描述：必填项都已填写，仍需填写
- 截图：[截图 1](./assets/管理员账号-10-1.png)
- 图片描述：表单所有可见项已填，但提交仍触发“必填”校验，说明存在隐藏字段、DTO 校验或绑定不一致。
- 建议修复：对比前端表单模型与后端 DTO 的必填规则，去掉隐藏必填字段或补齐默认值。
- 初步关联代码：
  - 对应业务表单组件
  - `src/KnowledgeHub.Application.Contracts/*` 中相关 DTO

## 11. 章节删除失败

- 原始描述：章节删除失败
- 截图：[截图 1](./assets/管理员账号-11-1.png)
- 图片描述：章节管理页面删除章节时失败。
- 建议修复：检查是否存在子章节、关联知识点、关联习题或外键未清理；完善服务端级联删除和友好提示。
- 初步关联代码：
  - `angular/src/app/learning/chapter-management/chapter-management.component.ts`
  - `src/KnowledgeHub.Application/Courses/ChapterAppService.cs`

## 12. 章节导入失败

- 原始描述：章节导入失败
- 截图：[截图 1](./assets/管理员账号-12-1.png)
- 图片描述：章节模板导入后失败，疑似模板列顺序、层级解析或空行处理不一致。
- 建议修复：统一章节模板规范、增强层级解析容错、输出逐行错误结果。
- 初步关联代码：
  - `angular/src/app/learning/chapter-management/chapter-management.component.ts`
  - `src/KnowledgeHub.Application/Courses/ChapterAppService.cs`

## 13. 不显示学生名字

- 原始描述：不显示学生名字
- 截图：[截图 1](./assets/管理员账号-13-1.png)
- 图片描述：学生列表/选课列表中学生名称为空，只显示其他字段。
- 建议修复：排查跨租户查询时用户 `Name` 为空的回退逻辑，至少回退到 `UserName` 或学号。
- 初步关联代码：
  - `angular/src/app/learning/student-enrollment/student-enrollment.component.ts`
  - `src/KnowledgeHub.Application/Courses/StudentCourseAppService.cs`

## 14. 如何添加新的文档？点击 + 号无反应

- 原始描述：如何添加新的文档？点击+号无反应
- 截图：[截图 1](./assets/管理员账号-14-1.png)
- 图片描述：文档问答页左侧文档资源区有 `+` 按钮，但用户看不出含义，且与“新增文档”形成误解。
- 建议修复：把 `+` 的语义改成明确按钮文案，例如“新建对话”；如果确实需要新增文档，应提供单独上传入口。
- 初步关联代码：
  - `angular/src/app/ai/chat/chat.component.html`
  - `angular/src/app/ai/chat/chat.component.ts`

## 15. 同上

- 原始描述：同上
- 截图：[截图 1](./assets/管理员账号-15-1.png)
- 图片描述：教案生成页的资源选择区交互不清晰，用户同样找不到“新增文档/资源”的入口。
- 建议修复：统一 AI 模块中“选择现有资源”和“上传新资源”的入口设计，避免误导。
- 初步关联代码：
  - `angular/src/app/ai/lesson-plan/lesson-plan.component.html`
  - `angular/src/app/ai/lesson-plan/lesson-plan.component.ts`

## 16. 角色可以同时学生和教师，是不是不太合理

- 原始描述：角色可以同时学生和教师，是不是不太合理
- 截图：[截图 1](./assets/管理员账号-16-1.png)
- 图片描述：用户可同时拥有“学生”和“教师”等互斥角色。
- 建议修复：明确角色互斥规则，在前端选择和后端保存时都加校验，避免角色冲突。
- 初步关联代码：
  - `angular/src/app/admin/identity-users/identity-users.component.ts`
  - `src/KnowledgeHub.Application/Identity/TenantUserAppService.cs`
- 修复状态：`部分完成（2026-04-26）`
- 本次落地：
  - 用户编辑页前端角色控件已改为单选，不能再同时勾选学生和教师。
  - 如需彻底闭环，后端仍建议补最终校验，防止绕过前端直接提交多角色。

## 17. 租户还要手打很麻烦

- 原始描述：租户还要手打很麻烦
- 截图：[截图 1](./assets/管理员账号-17-1.png)
- 图片描述：租户切换弹窗只能手动输入租户名称。
- 建议修复：与问题 5 一并处理，改为可搜索选择，不再手填。
- 初步关联代码：
  - ABP 租户切换 UI，自定义替换组件
- 修复状态：`已完成（2026-04-26）`
- 本次落地：
  - 登录页租户切换已从按钮/弹框交互改为直接列表切换，不再需要手动输入。
  - 详情见 [管理端问题修复记录](./admin-fix-log.md) 的“修复 03”。

## 18. 平台不稳定

- 原始描述：平台不稳定
- 截图：[截图 1](./assets/管理员账号-18-1.png)
- 图片描述：线上页面报错，错误内容显示仍在请求 `https://localhost/api/app/install/status`，说明生产环境前端 API 地址配置错误。
- 建议修复：修正动态环境配置和安装状态初始化逻辑，禁止在生产环境回退到 `localhost`。
- 初步关联代码：
  - `angular/src/app/install/install.initializer.ts`
  - `angular/src/app/app.config.ts`
  - `etc/docker/.env.production`
  - `dynamic-env.json` 生成/部署链路

## 19. 页面设置有问题，这个文字像溢出来一样

- 原始描述：页面设置有问题，这个文字像溢出来一样
- 截图：[截图 1](./assets/管理员账号-19-1.png)
- 图片描述：文档查看页面正文长句横向溢出，遮挡阅读区域。
- 建议修复：为正文容器补齐 `word-break`、`overflow-wrap`、横向滚动控制，并统一高亮样式。
- 初步关联代码：
  - `angular/src/app/document-viewer/document-viewer.component.ts`

## 20. 英文界面依旧有中文出现

- 原始描述：英文界面依旧有中文出现
- 截图：[截图 1](./assets/管理员账号-20-1.png)
- 图片描述：英文语言环境下，页面标题、空态文案、菜单或按钮仍是中文。
- 建议修复：补齐 `en.json` 资源，并替换页面内写死中文的模板字符串。
- 初步关联代码：
  - `src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/en.json`
  - 多个 Angular 页面模板

## 21. 上传了力学的资源，但是搜索不到

- 原始描述：上传了力学的资源，但是搜索不到
- 截图：[截图 1](./assets/管理员账号-21-1.png)
- 图片描述：资源库中已存在与“力学”相关文档，但搜索页返回 0 结果。
- 建议修复：排查上传后索引任务是否完成、索引名称是否正确、租户过滤是否误伤、搜索页默认索引是否与写入索引一致。
- 初步关联代码：
  - `src/KnowledgeHub.Application/Resources/ResourceAppService.cs`
  - `src/KnowledgeHub.Application/Search/IndexingJobAppService.cs`
  - `src/KnowledgeHub.Application/Search/MeiliSearchService.cs`
  - `angular/src/app/search/search.service.ts`

## 22. 点击重新搜索，之后发生错误

- 原始描述：点击重新搜索，之后发生错误
- 截图：[截图 1](./assets/管理员账号-22-1.png)
- 图片描述：搜索历史页点击“重新搜索”后弹出全局错误，底层仍是错误的 `install/status` 地址请求。
- 建议修复：与问题 18 一并处理全局 API 基址问题，同时检查搜索历史页跳转逻辑。
- 初步关联代码：
  - `angular/src/app/search/search-history/search-history.component.ts`
  - `angular/src/app/install/install.initializer.ts`

## 23. 是资源库，不是知识库，麻烦改一下

- 原始描述：是资源库，不是知识库，麻烦改一下
- 截图：[截图 1](./assets/管理员账号-23-1.png)
- 图片描述：首页、登录页、学生门户等多处系统名称仍显示“知识库系统”。
- 建议修复：统一产品术语，把用户可见的“知识库”改成“资源库”；代码层保留命名空间 `KnowledgeHub` 不必强改。
- 初步关联代码：
  - `angular/src/app/home/home.component.html`
  - `angular/src/app/login/login.component.html`
  - `angular/src/app/student/layout/student-layout.component.html`
  - `src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/*.json`
