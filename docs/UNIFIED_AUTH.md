# 统一账户接入（KnowledgeHub）

完整设计与迁移手册在 `~/projects/auth-server/docs/`（`architecture.md`、`migration-playbook.md`、
`integration-knowledgehub.md`）。

## 开关

`appsettings.json` 的 `UnifiedAuth` 段默认 `Enabled=false`。生产用环境变量开启：

```
UnifiedAuth__Enabled=true
UnifiedAuth__BaseUrl=http://account-server:8080
UnifiedAuth__ClientId=app_knowledgehub
UnifiedAuth__ClientSecret=<secret>
```

## 行为

- 租户内登录：`/connect/token` 密码授权与 Razor 登录页都改为账户服务校验密码 + 租户/成员/应用有效期
- 登录成功：复用本地 `AbpUsers`；没有则在租户内 JIT 建号并赋角色，回填 `app_identities`
- 改密：`/api/account/my-profile/change-password` 先更新统一账户，再同步本地哈希
- 新建用户（公开注册 / 后台建号 / Excel 导入）会登记到统一账户，失败不阻断本地创建
- 新建租户：按名称检查统一账户占用；创建后自动登记租户、开通本应用，并登记 ABP 自动创建的租户管理员
- 宿主 `admin`（`CurrentTenant == null`）保持本地密码

## 回滚

把 `UnifiedAuth__Enabled` 设回 `false` 并重启，立即恢复本地密码校验（本地哈希未被删除）。

## 限制

- 统一路径不写 ABP `IdentitySecurityLog`；登录审计看账户服务 `login_attempts`
- `/connect/token` 统一路径不触发 ABP 双因素；Razor 路径仍触发
- 后台“重置密码”、资料修改暂未同步到统一账户
