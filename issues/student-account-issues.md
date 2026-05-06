# 学生账号问题清单

## 1. 学生登录后不应看到这类后台首页

- 原始描述：学生账号登录号之后不应该有这样的界面吧
- 截图：[截图 1](./assets/学生账号-01-1.jpeg)
- 图片描述：学生登录后仍进入通用后台首页，而不是直接进入学生门户。
- 建议修复：学生登录成功后默认跳转学生门户；首页入口和默认落地页按角色分流。
- 初步关联代码：
  - `angular/src/app/home/home.component.ts`
  - `angular/src/app/app.routes.ts`
  - 登录后跳转逻辑

## 2. 无法预览

- 原始描述：无法预览
- 截图：[截图 1](./assets/学生账号-02-1.jpeg)
- 图片描述：学生点击资源预览后弹出失败提示，且文案为英文 `Failed to load file for preview`。
- 建议修复：排查学生预览权限、文件 URL 获取、二进制下载和预览组件渲染；失败提示改成中文。
- 初步关联代码：
  - `angular/src/app/student/resources/student-resources.component.ts`
  - `angular/src/app/shared/preview/file-preview.component.ts`
  - `src/KnowledgeHub.Application/Resources/ResourceAppService.cs`

## 3. 无法下载

- 原始描述：无法下载
- 截图：[截图 1](./assets/学生账号-03-1.jpeg)
- 图片描述：学生下载资源时报错，弹窗显示资源处理失败。
- 建议修复：检查学生是否具备 `Resources.Download` 权限、下载接口授权是否过严、前端错误处理是否缺失。
- 初步关联代码：
  - `angular/src/app/student/resources/student-resources.component.ts`
  - `src/KnowledgeHub.Application/Resources/ResourceAppService.cs`

## 4. 哪里显示我收藏的资源

- 原始描述：哪里显示我收藏的资源
- 截图：[截图 1](./assets/学生账号-04-1.jpeg)
- 图片描述：学生门户缺少“我的收藏/已收藏资源”入口与页面。
- 建议修复：补一条“我的收藏”路由和页面，并复用现有收藏/取消收藏接口。
- 初步关联代码：
  - `angular/src/app/student/layout/student-layout.component.html`
  - `angular/src/app/student/resources/student-resources.component.ts`
  - `src/KnowledgeHub.Application/Resources/ResourceAppService.cs`

## 5. 不要英文

- 原始描述：不要英文
- 截图：[截图 1](./assets/学生账号-05-1.jpeg)
- 图片描述：学生搜索页索引下拉中仍显示 `documents`、`videos` 等英文。
- 建议修复：同管理员问题 8，一并做搜索页本地化展示。
- 初步关联代码：
  - `angular/src/app/search/search.component.ts`

## 6. 继续学习后仍出现管理员式任务栏和习题管理

- 原始描述：点击继续学习跳转的界面左侧是这样的任务栏吗，还有习题管理（这不应该是学生账号的吧）
- 截图：[截图 1](./assets/学生账号-06-1.jpeg)
- 图片描述：学生从“继续学习”进入后，页面仍使用后台管理布局，并暴露了知识图谱、习题管理等管理型入口。
- 建议修复：学生学习页改为学生专用布局；隐藏管理动作，只保留学习、练习、进度等学生动作。
- 初步关联代码：
  - `angular/src/app/learning/my-courses/my-courses.component.ts`
  - `angular/src/app/learning/course-detail/course-detail.component.ts`
  - `angular/src/app/app.routes.ts`

## 7. 截图补充：学生仍可见后台左侧菜单

- 原始描述：源表未提供文字
- 截图：[截图 1](./assets/学生账号-07-1.jpeg)
- 图片描述：补充截图显示学生课程详情页仍出现完整后台左侧菜单。
- 建议修复：与问题 6 一并处理，拆分学生视图和后台视图。
- 初步关联代码：
  - `angular/src/app/learning/course-detail/course-detail.component.html`
  - `angular/src/app/student/layout/student-layout.component.html`

## 8. 截图补充：学生仍可见创建习题入口

- 原始描述：源表未提供文字
- 截图：[截图 1](./assets/学生账号-08-1.jpeg)
- 图片描述：补充截图显示学生仍可见“创建习题”等明显属于教师/管理员的按钮。
- 建议修复：对课程详情和习题页做角色化渲染，学生不显示创建、管理、导入等入口。
- 初步关联代码：
  - `angular/src/app/learning/course-detail/course-detail.component.html`
  - `angular/src/app/learning/exercise/exercise-list.component.ts`

## 9. 这个地方是什么意思

- 原始描述：这个地方时是什么意思
- 截图：[截图 1](./assets/学生账号-09-1.jpeg)
- 图片描述：右上角用户菜单中直接显示了类似模板/函数源码的字符串，而不是正常用户名。
- 建议修复：排查 Angular signal 在模板中的使用，避免把函数对象本身渲染到界面。
- 初步关联代码：
  - `angular/src/app/student/layout/student-layout.component.ts`
  - `angular/src/app/student/layout/student-layout.component.html`

## 10. AI 回答时，红色字体部分不应该显出来

- 原始描述：AI回答时，红色字体部分应该不要显出来吧
- 截图：[截图 1](./assets/学生账号-10-1.jpeg)
- 图片描述：AI 聊天页面中出现不适合用户阅读的红色文本/内部标记，页面底部输入框占位符还直接显示了模板控制流语法。
- 建议修复：清理助手输出中的内部标记和调试痕迹；修正模板中错误写法，避免把 `@if ...` 这种模板语法当作普通文本展示。
- 初步关联代码：
  - `angular/src/app/ai/chat/chat.component.html`
  - `angular/src/app/ai/chat/chat.component.ts`
  - `angular/src/app/ai/chat/chat.component.scss`
- 修复状态：`已完成（2026-04-27）`
- 本次落地：
  - `http://localhost:4200/student/ai/chat` 现已只展示给学生看的最终答案，不再显示工具调用、思考过程或折叠出来的中间内容。
  - 前端对流式消息增加了展示层清洗，常见的 `<think>`、`<thinking>`、工具块、思考折叠块和辅助说明行都会被过滤。
  - 后端问答提示词也已收紧，明确要求模型只输出最终答案，不向用户暴露检索步骤和工具调用过程。

## 11. 总会出现这样的界面，是不是网址不太稳定

- 原始描述：总会出现这样的界面，是不是网址不太稳定
- 截图：[截图 1](./assets/学生账号-11-1.jpeg)
- 图片描述：学生端频繁出现 `404` 或错误页，表现为地址不稳定。
- 建议修复：排查学生门户子路由、部署环境 API 地址、前端 fallback 和刷新直达路由配置。
- 初步关联代码：
  - `angular/src/app/app.routes.ts`
  - `angular/src/app/student/layout/student-layout.component.ts`
  - 部署环境 Nginx / Docker 前端路由转发配置
