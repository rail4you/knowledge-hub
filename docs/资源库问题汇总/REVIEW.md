# 修复审查记录

## 批次 7 (提交: `06ed0b3`)

### M-13 退出登录后页面状态异常

**修改文件**：`angular/src/app/home/home.component.ts`, `angular/src/app/student/layout/student-layout.component.ts`

**审查结论**：通过

| 检查项 | 结果 |
|--------|------|
| 根因分析正确性 | 通过 - `router.navigate` 是 SPA 路由跳转，不清除 ABP 框架缓存的 LeptonX 布局状态 |
| 修复方案合理性 | 通过 - `window.location.href` 完整刷新页面是最直接可靠的方案 |
| 边界情况 | 通过 - `authService.logout()` 回调内执行，确保 token 已清除后才刷新 |
| 遗漏检查 | 通过 - 全局搜索 `authService.logout` 确认共 3 处，其中 `auth-error-modal.component.ts` 是认证过期重登场景，ABP 框架会自动重定向到 IdP，无需同样处理 |
| 副作用 | 无 - 完整刷新体验略慢于 SPA 跳转，但退出场景可接受 |

---

### S-02 推荐资源点击无响应

**修改文件**：`angular/src/app/student/resources/student-resources.component.ts`

**审查结论**：通过

| 检查项 | 结果 |
|--------|------|
| 根因分析正确性 | 通过 - `selectRelatedResource` 缺少 `drawerVisible.set(true)`，与 `selectResource` 方法对比确认 |
| 修复方案合理性 | 通过 - 添加一行 `drawerVisible.set(true)` 与 `selectResource` 行为一致 |
| 边界情况 | 通过 - `selectedResource` 在 `drawerVisible.set(true)` 之前已赋值，Drawer 模板通过 `selectedResource()` 读取数据，不会出现空数据渲染 |
| 副作用 | 无 - Drawer 关闭逻辑 (`closeDrawer`) 未受影响 |

---

### M-01 资讯分类"编码"字段含义不明确

**修改文件**：`angular/src/app/admin/news/news-management.component.ts`

**审查结论**：通过（有备注）

| 检查项 | 结果 |
|--------|------|
| 根因分析正确性 | 通过 - 前端弹窗无编码输入框，`code: ''` 导致后端 `[Required]` 验证可能失败 |
| 修复方案合理性 | 通过 - 自动根据名称生成编码（名称 slug + 时间戳），用户无感知 |
| 唯一性冲突风险 | 可接受 - 后端有 `b.HasIndex(x => x.Code).IsUnique()` + `EnsureCodeUniqueAsync` 校验；前端 `Date.now().toString(36)` 毫秒级精度，正常操作不会冲突 |
| 编辑场景处理 | 通过 - `openEditCategory` 时 `code` 已有值，`saveCategory` 检查 `code` 非空则跳过自动生成，不会覆盖已有编码 |
| 名称含特殊字符 | 备注 - 中文名称经 `toLowerCase().replace(/\s+/g, '-')` 处理后仍可能包含非 ASCII 字符作为 code，但后端无字符限制，不影响功能 |
| 副作用 | 无 - 仅影响新建分类时 code 的自动生成逻辑 |

---

## 批次 8 (提交: `aeb12fb`, 审查修复: `eddde7f`)

### S-01 学生账号看不到二期功能模块

**修改文件**：`angular/src/app/student/layout/student-layout.component.html`, `angular/src/app/app.routes.ts`

**审查结论**：通过

| 检查项 | 结果 |
|--------|------|
| 路由正确性 | 通过 - 所有路由正确引用已存在的组件（MicroMajorListComponent, PracticumListComponent 等） |
| 导航入口合理性 | 通过 - 放在"我的课程"和"课堂任务"之间，图标选择恰当 |
| 权限守卫 | 通过 - student 路由已有 `canActivate: [authGuard, studentPortalGuard]` 守卫 |
| 组件复用 | 通过 - 直接复用管理端已有组件，无需创建学生专用版本 |

---

### M-02 资讯封面要求填写链接但前台没有展示闭环

**修改文件**：`news-detail.component.html/scss`, `news-list.component.html/scss`, `news-management.component.html/scss`

**审查结论**：通过（审查修复后）

| 检查项 | 结果 |
|--------|------|
| 详情页封面展示 | 通过 - 在摘要和正文之间展示封面图，宽度自适应，最大高度 400px |
| 列表页封面缩略图 | 通过 - 卡片顶部封面图，高度 160px，无封面时正常显示文本 |
| 管理后台预览 | 通过 - 输入 URL 后实时预览，添加 placeholder 说明 |
| 图片加载失败 | 通过(已修复) - 审查发现缺少容错，已添加 `(error)` 事件移除失败图片 |
| XSS 防护 | 通过 - 使用 Angular `[src]` 属性绑定，自动防御 XSS |
| 副作用 | 无 - 封面展示为可选（`@if` 条件判断），不影响无封面的资讯 |

---

### M-03 微专业与实训入口建议从课程模块中独立

**修改文件**：`angular/src/app/route.provider.ts`, `zh-Hans.json`

**审查结论**：通过（审查修复后）

| 检查项 | 结果 |
|--------|------|
| 菜单独立性 | 通过 - MicroMajorsGroup(order:5) 和 PracticumGroup(order:7) 成为独立一级菜单 |
| 子菜单挂载 | 通过 - 微专业3个子项、实训3个子项正确挂载到各自一级菜单下 |
| 本地化 | 通过 - 新增 `Menu:MicroMajorsGroup`(微专业) 和 `Menu:PracticumGroup`(实训) 翻译 |
| order 冲突 | 通过(已修复) - 审查发现 PracticumGroup(order:6) 与 Assessment(order:6) 冲突，已改为 7 |
| 课程模块瘦身 | 通过 - 课程模块不再包含微专业和实训，结构更清晰 |
