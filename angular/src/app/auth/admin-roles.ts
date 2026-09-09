/**
 * 管理端角色统一定义。
 *
 * 凡持有其中任一角色的已登录用户，一律视为"管理端身份"：
 * - 永远不渲染门户首页 `/` 与租户主页 `/tenant/:id`
 * - 永远不进入学生端 `/student/**`
 * 而是直接路由到管理后台 `/resources`。
 *
 * 注意：`admin` 为宿主超级管理员的角色名，必须包含在内。
 */
export const ADMIN_ROLES = ['Teacher', 'SchoolAdmin', 'LeagueAdmin', 'EnterpriseUser', 'admin'];
