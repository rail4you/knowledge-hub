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
