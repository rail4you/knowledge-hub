# 教师账号问题清单

## 1. 教师账号应该只能选择自己租户的学生

- 原始描述：这是教师账号，应该只能选择自己租户的学生才对吧
- 截图：[截图 1](./assets/教师账号-01-1.png)
- 图片描述：教师在学生选课/分配页面中可见的学生范围疑似过大，用户担心跨租户可见。
- 建议修复：明确教师与管理员的权限边界；教师仅能操作本租户、本授权课程下的学生，不显示租户切换入口。
- 初步关联代码：
  - `angular/src/app/learning/student-enrollment/student-enrollment.component.ts`
  - `src/KnowledgeHub.Application/Courses/StudentCourseAppService.cs`

## 2. 管理员创建的课程，教师账号看不到

- 原始描述：同样的课程，在管理员账号中创建的数据，在教师账号是不显示的
- 截图：[截图 1](./assets/教师账号-02-1.png)
- 图片描述：课程数据在管理员端存在，但教师端课程页为空或无法看到相同课程。
- 建议修复：重新定义课程可见性规则，明确“创建人可见”“同租户可见”“已分配教师可见”的业务逻辑，并统一前后端查询。
- 初步关联代码：
  - `angular/src/app/learning/my-courses/my-courses.component.ts`
  - `angular/src/app/learning/course-list/course-list.component.ts`
  - `src/KnowledgeHub.Application/Courses/CourseAppService.cs`

## 3. 教师账号不应进入学生门户

- 原始描述：教师账号是不是不应该也能进学生门户？
- 截图：[截图 1](./assets/教师账号-03-1.png)
- 图片描述：教师登录后仍能看到学生门户入口或直接访问学生门户页面。
- 建议修复：学生门户路由、首页入口和导航菜单改为基于角色控制，而不是仅基于“已登录”。
- 初步关联代码：
  - `angular/src/app/home/home.component.html`
  - `angular/src/app/app.routes.ts`
  - `angular/src/app/student/layout/student-layout.component.ts`

## 4. 教师账号修改资料失败

- 原始描述：教师账号修改资料失败
- 截图：[截图 1](./assets/教师账号-04-1.png)
- 图片描述：资料编辑保存时报错：`The field RoleType is invalid.`
- 建议修复：排查资料页提交模型中 `RoleType` 的绑定、隐藏字段默认值和后端 DTO 校验；避免把无效枚举值传回服务端。
- 初步关联代码：
  - `angular/src/app/identity-form-prop-contributors.ts`
  - 资料页对应的 ABP Account/Profile 组件
  - `src/KnowledgeHub.Application/Identity/TenantUserAppService.cs`
