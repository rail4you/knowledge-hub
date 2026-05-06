# 管理端问题修复记录

## 修复 01 - 角色名称改为预设下拉

- 修复时间：`2026-04-26`
- 页面路由：`/identity/roles`
- 对应问题：
  - 管理员 `2`：角色改为选择的方式
  - 管理员 `3`：更改已有角色的名称会发生错误
- 问题现象：
  - 新建角色时，角色名称使用自由输入，管理员需要自己手打“管理员、教师、学生”等角色名。
  - 编辑已有角色时也可以直接修改角色名称，但当前保存链路存在报错风险。
- 修复内容：
  - 新建角色弹窗中的“角色名称”已改为预设角色下拉，不再允许手工输入。
  - 预设角色统一收敛为：`管理员(admin)`、`联盟管理员(LeagueAdmin)`、`院校管理员(SchoolAdmin)`、`教师(Teacher)`、`学生(Student)`、`企业用户(EnterpriseUser)`。
  - 编辑已有角色时，已移除“角色名称”编辑框，避免在当前后端能力未稳定前继续触发重命名报错。
- 代码位置：
  - `angular/src/app/admin/identity-roles/identity-roles.component.ts`
  - `angular/src/app/admin/identity-roles/identity-roles.component.html`
- 验证方式：
  - 打开 `http://localhost:4200/identity/roles`
  - 点击“新建角色”，确认角色名称为下拉选择
  - 点击任意已有角色“编辑”，确认弹窗中不再显示角色名称编辑框
- 备注：
  - 本次先修复管理端 UI 和操作约束，保证管理员可用性。
  - 如果后续产品仍要求支持“角色重命名”，需要再补后端重命名校验、唯一性处理和异常提示。

## 修复 02 - 用户编辑只保留一个租户选择

- 修复时间：`2026-04-26`
- 页面路由：`/identity/users`
- 对应问题：
  - 管理员 `4`：要选择两次租户，有些累赘
- 问题现象：
  - 用户弹窗里“基本信息”有一个“所属租户”菜单。
  - “租户与角色”区域又重复出现一份“选择租户”菜单，管理员需要选两次。
  - 当两个菜单选择不一致时，角色区内容容易和当前租户不同步。
- 修复内容：
  - 移除“租户与角色”区域里的重复租户菜单，只保留“基本信息 > 所属租户”这一处选择。
  - 当“所属租户”变化时，角色列表会自动按该租户重新加载。
  - 租户切换后，原先已勾选但不属于新租户的角色会自动清掉，避免脏数据残留。
- 代码位置：
  - `angular/src/app/admin/identity-users/identity-users.component.ts`
  - `angular/src/app/admin/identity-users/identity-users.component.html`
- 验证方式：
  - 打开 `http://localhost:4200/identity/users`
  - 点击“新建用户”或“编辑”
  - 在“基本信息 > 所属租户”切换租户，确认“租户与角色”区域的角色列表同步刷新
  - 确认弹窗中不再出现第二个“选择租户”菜单

## 修复 03 - 登录页租户切换改为直接列表

- 修复时间：`2026-04-26`
- 页面路由：`https://localhost:44305/Account/Login`
- 对应问题：
  - 管理员 `5`：租户切换也改为选择的方式吧
  - 管理员 `17`：租户还要手打很麻烦
- 问题现象：
  - 登录页原本需要先点击“切换租户”按钮，再弹出选择框。
  - 交互多一步，且原始诉求是不想再通过按钮或手动输入完成租户切换。
- 修复内容：
  - 登录页会直接显示租户列表，不再要求先点“切换租户”按钮。
  - 当前选中的租户会高亮显示，点击某个租户后立即写入 `__tenant` cookie 并刷新页面。
  - 原有开发环境里的弹窗式租户选择逻辑已移除，避免和新的列表切换冲突。
- 代码位置：
  - `src/KnowledgeHub.HttpApi.Host/wwwroot/global-scripts.js`
  - `src/KnowledgeHub.HttpApi.Host/wwwroot/global-styles.css`
  - `src/KnowledgeHub.HttpApi.Host/wwwroot/dev-login-helper.js`
- 验证方式：
  - 打开 `https://localhost:44305/Account/Login`
  - 确认页面直接出现租户列表，而不是“切换租户”按钮触发弹窗
  - 点击不同租户后，页面刷新且当前租户显示同步变化

## 修复 04 - 用户角色改为单选

- 修复时间：`2026-04-26`
- 页面路由：`/identity/users`
- 对应问题：
  - 管理员 `2`：角色改为选择的方式
  - 管理员 `16`：角色可以同时学生和教师，是不是不太合理
- 问题现象：
  - “租户与角色”区域原先使用多选复选框，允许一次勾选多个角色。
  - 这会让“学生/教师可同时存在”之类的冲突角色仍然有机会在前端被选出来。
- 修复内容：
  - 用户编辑弹窗中的角色选择已改为单选下拉，不再允许多选角色。
  - 保存用户时前端只提交一个角色名称，避免一次提交多角色。
  - 编辑已有用户时，如果历史上存在多个角色，界面会优先显示第一个角色；再次保存后会按当前单选值收敛。
- 代码位置：
  - `angular/src/app/admin/identity-users/identity-users.component.ts`
  - `angular/src/app/admin/identity-users/identity-users.component.html`
- 验证方式：
  - 打开 `http://localhost:4200/identity/users`
  - 点击“新建用户”或“编辑”
  - 确认“租户与角色”中的角色控件为单选下拉，而不是复选框列表
  - 保存后确认请求只携带一个角色

## 修复 05 - PPTX 预览改为按幻灯片渲染

- 修复时间：`2026-04-26`
- 页面路由：`/resources`
- 对应问题：
  - 管理员 `6`：PPT 预览与实际文件有较大出入
- 问题现象：
  - 旧版 PPTX 预览只是从压缩包里粗略抽取文本和图片，再手工拼成页面。
  - 文字位置、图片布局、分页效果都和实际 PowerPoint 差异很大，且缺少真正的幻灯片翻页体验。
- 修复内容：
  - 已移除原来的手写 PPTX XML 解析方案，改用专门的前端 PPTX 渲染库 `pptxviewjs`。
  - 预览现在基于 `canvas` 按幻灯片逐页渲染，支持上一页、下一页、页码跳转和缩放。
  - PPTX 预览弹窗宽度已调大，给幻灯片更接近实际比例的显示空间。
- 代码位置：
  - `angular/src/app/shared/preview/pptx-viewer.component.ts`
  - `angular/src/app/shared/preview/pptx-viewer.component.html`
  - `angular/src/app/shared/preview/pptx-viewer.component.scss`
  - `angular/src/app/shared/preview/file-preview.component.html`
  - `angular/package.json`
- 验证方式：
  - 打开 `http://localhost:4200/resources`
  - 选择一个 `.pptx` 资源点击“预览”
  - 确认预览区按幻灯片分页展示，支持翻页、页码切换和缩放
  - 对比实际 PPTX，确认版式接近原始文件

## 修复 06 - AI 页面资源列表不刷新

- 修复时间：`2026-04-26`
- 页面路由：
  - `http://localhost:4200/ai/chat`
  - `http://localhost:4200/ai/lesson-plan`
  - `http://localhost:4200/ai/case-analysis`
  - `http://localhost:4200/ai/career-guidance`
- 问题现象：
  - AI 页面调用资源接口后，后端其实已经返回资源数据，但页面上仍看不到资源列表。
  - `lesson-plan` 页的空态组件依赖也不完整，空资源时提示区域不稳定。
- 修复内容：
  - `ChatService.getResources()` 的 `fetch` 回调已切回 Angular `NgZone`，确保 `OnPush` 组件在拿到资源后立即刷新视图。
  - `lesson-plan` 页面已补齐 `NzEmptyModule`，空列表状态能正常显示。
- 代码位置：
  - `angular/src/app/ai/services/chat.service.ts`
  - `angular/src/app/ai/lesson-plan/lesson-plan.component.ts`
- 验证方式：
  - 打开任一 AI 页面
  - 确认资源接口返回后，页面能立即显示资源列表
  - 在无资源场景下，`lesson-plan` 页能正常显示空态提示
