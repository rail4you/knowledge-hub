# 易课通 Android 客户端 — 逐步迁移规划

> 目标：让教师与学生用手机完成「登录 → 找课 → 学习 → 练习 → 查看进度」的主链路。
> 采用**一个 App、单一登录界面、按账号角色自动分流**到教师端 / 学生端。
>
> 技术栈：Kotlin + Jetpack Compose + OkHttp + kotlinx.serialization + DataStore。
> 直接复用 Web 端已有的 ABP REST API，不新增后端接口。

---

## 一、环境现状（2026-11-21 实测）

| 项 | 状态 |
|---|---|
| Android SDK | `/opt/homebrew/share/android-commandlinetools`，platform 34/35、build-tools 34.0.0 |
| 模拟器 | AVD `tcm_phone`（Android 34），已启动并连通 `emulator-5554` |
| JDK / Gradle | JDK 21；Gradle 8.2.1（本地缓存）；AGP 8.2.1；Compose Compiler 1.5.14 |
| 后端 | `https://localhost:44305`（Debug DLL 直跑，PostgreSQL 5433） |
| 统一账户服务 | `http://127.0.0.1:8080`（Go `~/projects/auth-server/bin/accountctl serve`，PostgreSQL 55433） |
| 前后端联调 | ✅ 模拟器经 `10.0.2.2:44305` 访问后端，`/connect/token`、`/api/public/tenants`、`/api/abp/application-configuration` 均返回 200 |

> 说明：后端默认 `dev.sh start api` 因 NuGet 缓存中个别 analyzer DLL 缺失导致构建失败，改用**已编译 DLL 直接运行**（见第六节）。这不影响 API 能力，但后续应修复 NuGet 缓存恢复标准启动方式。

### 租户登录与统一账户（重要）

租户内账号（如 qidi 的 `zmq / 123456`）走 **UnifiedAuth 统一账户服务**，本地 `AbpUsers` 密码不生效。要让其登录成功，必须同时满足：

1. **启动账户服务**：`cd ~/projects/auth-server && set -a && source .env && set +a && ./bin/accountctl serve`（监听 8080，数据库容器 `account-postgres` 需在跑）
2. **开启后端 UnifiedAuth 开关**：知识库后端启动时带环境变量
   ```
   UnifiedAuth__Enabled=true
   UnifiedAuth__BaseUrl=http://127.0.0.1:8080
   UnifiedAuth__ClientId=app_knowledgehub
   UnifiedAuth__ClientSecret=<app 的 client_secret>
   ```
3. **client_secret**：若遗忘，用管理后台轮换（账号 `admin / <本地管理员密码>`）：
   - `POST http://127.0.0.1:8080/admin/api/login` 拿 cookie + `csrf_token`
   - `POST http://127.0.0.1:8080/admin/api/apps/52b1193e-8e1d-49cd-b388-38e61c3106e0/rotate`，带 Cookie 与 `X-CSRF-Token` 头，返回新明文 secret（只显示一次）
   - 轮换后知识库后端要用新 secret 重启

> 账号体系对照：**宿主账号**（如 `admin / 1q2w3E*`、`qa_student_fix / Test123!`）走本地密码，即使不开 UnifiedAuth 也能登录；**租户账号**（如 qidi 的 `zmq / 123456`）必须开 UnifiedAuth + 账户服务。

> **一键启动**：先 `export KH_CLIENT_SECRET=<从本地密钥管理或管理后台轮换获取>`，再运行 `./android-client/scripts/start-local-services.sh`（账户服务 + 带 UnifiedAuth 的后端），加 `--background` 让后端后台运行。后端重启务必走此脚本，否则租户账号会登不上。`client_secret` 不入库。

> ✅ 2026-11-21 实测：`zmq / 123456`（租户 qidi）在统一账户链路下登录成功，角色 `Student`，App 正确进入学生端并展示该生真实课程数据（选课 7 门、平均进度 28%）。

---

## 二、MVP 已打通的能力（Phase 1 ✅）

### 账户系统
- [x] 单一登录界面：学校/租户下拉（真实拉取 `GET /api/public/tenants`）、账号、密码
- [x] ABP OpenIddict 密码模式登录：`POST /connect/token`（`client_id=KnowledgeHub_App`，`__tenant` 头传租户）
- [x] token 持久化（DataStore）：access_token / refresh_token / 租户 / 用户角色
- [x] 登录成功后读取 `GET /api/abp/application-configuration` 判断角色
- [x] **角色分流**：`Teacher/SchoolAdmin/LeagueAdmin/EnterpriseUser/admin` → 教师端；`Student` → 学生端（与 Web 端 `admin-roles.ts` 口径一致）
- [x] 退出登录清空会话

### 课程学习系统（学生端）
- [x] **工作台首页**：欢迎卡片、学习概览、常用模块快捷入口、继续学习、即将开放模块；所有入口由 `AppModules` 注册表驱动
- [x] 学习概览：选课数 / 学习中 / 已完成 / 平均进度（`GET /api/app/learning/dashboard`）
- [x] 我的课程列表（`GET /api/app/learning/my-courses`）
- [x] 课程中心：本校发布 / 共享到本校 / 公共课程三类数据源（`GET /api/app/course/published`、`/api/app/course-share/shared-to-me`、`/api/app/course/public-library-courses`），支持关键词搜索与专业筛选
- [x] 未选课课程可查看详情、课程简介、章节目录与资源清单；详情页按 `isEnrolled` 显示「报名学习 / 开始学习」
- [x] 课程详情 + 章节树（`GET /api/app/course/{id}/detail`、`/api/app/chapter/chapter-tree/{id}`）
  - 注意：ABP 对带 `Guid id` 参数的方法生成路由 `/{id}/detail`（id 在路径里），不是 `detail?id=`。写错会被匹配到 `GetAsync` 报 `The value 'detail' is not valid`。
- [x] 选课（`POST /api/app/course/enroll/{courseId}`，与 Web 端路由一致）
- [x] **课程详情页**：简介、主讲教师、专业/章节/学生、Tab（课程资源 / 章节目录）、底部"开始学习"
- [x] **课程资源**：章节直挂 `knowledgeResources`（`name/description/resourceId`），资源汇总展示，可标记"可预览"
- [x] **学习页**（`LearnScreen`）：左章节列表 + 右内容区
  - 章节资源（`GET /api/app/chapter/chapter-tree/{id}` 带出的知识资源），学习行为记录 `POST /api/app/learning/record-progress`
  - 章节习题（`GET /api/app/exercise/by-chapter/{chapterId}`），支持 5 种题型：单选(0)/多选(1)/判断(2)/填空(3)/简答(4)
  - 提交答案（`POST /api/app/student-exercise-record/save-or-update-record`）；判断题答案按后端口径转 `true/false`
  - 历史提交记录（`GET /api/app/student-exercise-record/records-by-chapter`），含对错、正确答案、解析、作答与时间
- [x] **进度口径修正**：取消手动"标记完成"，改为按习题完成比例自动展示
  - `GET /api/app/learning/my-exercise-progress/{courseId}` → `{completedCount, totalCount}`，显示 `完成数/总数` 与百分比
  - 用户不手动提交进度；进度仅由习题提交数驱动
- [x] **学习统计 Tab**：在学/已完成/习题练习/资源学习四项指标、平均进度、最近 7 天学习活动、知识点掌握度、最近学习、进行中课程、最近习题记录（全部 / 正确 / 错误筛选）
- [x] **设置 Tab**：`GET/PUT /api/app/user/my-profile` 查看与修改姓名、邮箱、手机号；`POST /api/account/my-profile/change-password` 修改密码；退出登录
- [x] **标准安卓导航**：底部 5 个一级 Tab（工作台 / 我的学习 / 课程中心 / 学习统计 / 设置），课程详情与章节学习为二级页面，资源预览为三级页面；系统返回键逐级回退，非工作台 Tab 先回工作台

### 课程学习系统（教师端）
- [x] 我教的课程（`POST /api/app/course/get-by-filter?onlyMine=true`）
- [x] 课程学习概览：学生数/活跃/习题/完成率/正确率（`POST /api/app/student-exercise-record/course-learning-overview`）
- [x] 学生学习情况列表（`POST /api/app/student-exercise-record/learning-statistics`）

### 端到端实测结果
- ✅ 用宿主 `admin`（admin 角色）登录 → 正确进入**教师端**，界面渲染正常，退出正常
- ✅ 学生端分流逻辑与教师端对称，API 端点已验证连通

---

## 三、后续迁移路线（按优先级）

原则：**每阶段都复用现有后端 API，先做"只读查看"，再逐步开放"写入操作"**。UI 沿用 PRODUCT.md 的蓝色系、移动优先规范。

### Phase 2 — 学生端学习闭环增强（P0）
| 功能 | 后端 API（已有） |
|---|---|
| 章节习题练习 | `/api/app/student-exercise-record/*`（save-or-update-record、records-by-chapter） |
| 专项练习 / 自评 | `submit-self-assessment`、`mark-answer-viewed` |
| 作业查看与提交 | `/api/app/assignment/my-assignments`、`/assignment/submit` |
| 知识点掌握度 | `/api/app/learning/knowledge-mastery/{courseId}` |
| 收藏 / 最近学习 | 资源活动相关接口 |

### Phase 3 — 教师端教学管理（P1）
| 功能 | 后端 API（已有） |
|---|---|
| 课程发布 / 撤回 | `/api/app/course/publish`、`/course/unpublish` |
| 章节管理 / 导入 | `/api/app/chapter/*` |
| 作业发布与批改 | `/api/app/assignment/statistics`、`/grade/{id}` |
| 学生学习详情 | `/api/app/student-exercise-record/student-learning-detail` |
| 学情分析报表 | `/api/app/learning-analytics/*` |

### Phase 4 — 资源与搜索（P1）
- 资源库浏览 / 检索：`/api/app/resource*`、`/api/search/*`
- 文档查看器（PDF/图片）：复用 Web 端 document-viewer 能力，App 内嵌预览
- 微专业：`/api/app/micro-major*`

### Phase 5 — 成长与就业（P2）
- 实训 / 证书：`/api/app/practicum*`、`/api/app/certificate*`
- 就业信息 / 招聘直播：`/api/app/employment*`、`/api/app/recruitment-live*`
- 双高 / 特色专业展示：`/api/app/double-high*`、`/api/app/special-education*`

### Phase 6 — AI 与消息（P2）
- 教学智能体 / AI 问答：`KnowledgeHub.AI.Api` + `/api/app/ai-*`
- 知识图谱：`/api/app/knowledge-graph*`
- 消息通知、语音助手：按需接入

---

## 四、架构与关键约定

```
android-client/
├── app/src/main/java/com/knowledgehub/mobile/
│   ├── MainActivity.kt              # 入口：会话恢复 + 角色分流
│   ├── data/
│   │   ├── SessionManager.kt        # token/角色持久化 + 角色判定
│   │   ├── ApiInitializer.kt
│   │   ├── api/ApiClient.kt         # OkHttp：Bearer + __tenant 头、统一解析
│   │   ├── model/                   # DTO（对应后端 Contracts）
│   │   └── repository/              # AuthRepository / CourseRepository
│   ├── theme/Theme.kt               # 品牌蓝主题
│   └── ui/
│       ├── common/
│       │   ├── BrandComponents.kt    # 品牌顶栏：Logo + “易课通” + 页面/角色
│       │   └── AppModules.kt         # 模块注册表 + 通用底部导航（新增模块入口）
│       ├── login/ student/ teacher/  # Compose 页面
│       │   └── student/
│       │       ├── StudentScreen.kt          # 一级 Tab 与二级页面路由
│       │       ├── WorkbenchTab.kt           # 工作台首页与模块快捷入口
│       │       ├── CourseCenterTab.kt        # 课程中心（三类范围 + 搜索筛选）
│       │       ├── LearningStatsTab.kt       # 学习统计
│       │       └── SettingsTab.kt            # 账户信息与改密
```

- **API 基址**：`app/build.gradle.kts` 的 `API_BASE_URL`，模拟器用 `https://10.0.2.2:44305`，真机改为局域网 IP。
- **角色判定**：`SessionManager.isTeacher()`，与 Web 端 `admin-roles.ts` 保持一致。
- **DTO 字段**：以 `src/KnowledgeHub.Application.Contracts/**` 为准；`kotlinx.serialization` 开启 `ignoreUnknownKeys`，后端加字段不影响旧版 App。
- **代理生成**：后端 DTO 变更需同步更新 `data/model/`（手动维护，无生成脚本）。
- **品牌一致性**：所有页面顶栏统一用 `BrandTopBar` / `BrandTopBarWithBack`（Logo + 应用名 + 页面标题），登录页有完整 Logo 区。
- **错误处理**：`ApiClient.friendlyError()` 统一解析 OAuth 与 ABP 两种错误结构，向用户展示友好提示而非原始 JSON。
- **401 全局处理**：网络层收到 401 时通过 `AuthEvents.notifyUnauthorized()` 发事件，`AppNav` 收集后清会话并跳登录页；后端重启/token 失效不再停留在"请求失败"页面。
- **导航分层**：登录 → 角色分流 → 一级 Tab（底部导航）→ 二级页面（课程详情 / 章节学习）→ 三级预览（PDF / 视频 / Web）。每个一级 Tab 独立加载数据，切换保留状态。
- **模块扩展**：`ui/common/AppModules.kt` 统一注册模块 ID、名称、图标、描述、是否进底部导航、是否可用；工作台卡片和底部导航都从注册表读取。新增模块只需注册并在 `StudentScreen.reloadModule/selectModule` 补一个分支；未上线模块设 `enabled=false`，点击提示“即将开放”。
- **返回行为**：系统返回键按「预览/章节学习 → 课程详情 → 当前 Tab → 工作台」逐级回退，避免从二级页面直接退出 App。

---

## 五、技术债 / 风险清单

| 项 | 说明 | 处理时机 |
|---|---|---|
| 自签证书信任 | `ApiClient.kt` 中 `trustAllManager` 信任所有证书，**仅用于联调** | 生产前改为正式 CA 或证书锁定（已标 TODO） |
| refresh_token 续期 | 目前 token 过期/后端重启后由 401 事件引导重新登录 | Phase 2 接入 `/connect/token` refresh grant，减少重新登录 |
| 后端启动方式 | NuGet 缓存缺失导致 `dev.sh start api` 构建失败 | 尽快 `dotnet restore` 修复，回到标准启动 |
| 教师/学生演示账号 | 当前精简库无现成账号，需 Web 端建号后 App 登录 | 验收前准备 |
| 离线缓存 | 当前无本地缓存，弱网体验一般 | Phase 4 视需求引入 Room |

---

## 六、如何构建与运行

```bash
cd android-client
# SDK 路径已写入 local.properties；确保 JAVA_HOME=JDK21
./gradlew assembleDebug
# 产物：app/build/outputs/apk/debug/app-debug.apk

# 安装到模拟器
adb -s emulator-5554 install -r app/build/outputs/apk/debug/app-debug.apk
adb -s emulator-5554 shell am start -n com.knowledgehub.mobile/.MainActivity
```

后端需先运行（见第一节），并确保模拟器可通过 `10.0.2.2:44305` 访问。

---

## 七、修复记录

### 2026-11-21 课程详情报错 + 品牌缺失
- **问题 1**：学生点进课程详情显示 API 错误 `The value 'detail' is not valid`。
  - 根因：`CourseRepository.getDetail` 用了 `/api/app/course/detail?id=xxx`，被 ABP 匹配成 `GetAsync(id="detail")`。
  - 修复：改为 ABP 约定路由 `/api/app/course/{id}/detail`。
  - 顺带修正 `ChapterDto` 字段名 `order` → `sortOrder`（后端实际字段），一级章节现在显示描述。
- **问题 2**：进入课程详情后顶栏只有"课程详情"，缺少应用名称/logo。
  - 修复：新增 `ui/common/BrandComponents.kt`（AppLogo / BrandTopBar / BrandTopBarWithBack），学生端/教师端首页、课程详情、学情详情顶栏统一显示 Logo + "易课通" + 页面/角色标题。
- **顺带**：`ApiClient.friendlyError()` 统一解析 OAuth/ABP 错误，避免把原始 JSON 抛给用户（登录失败、加载失败均受益）。
- ✅ 实测：`zmq / 123456`（qidi）进入课程详情，detail 与 progress 接口均 200，界面正常展示课程信息/进度/章节，顶栏品牌一致。

### 2026-11-21 课程资源/习题/提交记录（参照 Web 版打通基础）
- 参照 Web 路由 `/student/courses/:id?tab=resources` 与 `/student/courses/:id/learn`。
- **关键认知**：进度本来就是"已提交习题数 / 全部习题数"，由后端算好后下发（`my-exercise-progress`）。因此删除了手动"标记完成/标记学至50%"按钮——用户不应自己提交完成。
- 课程详情增强：简介、主讲教师、课程资源（章节 `knowledgeResources`）、Tab、底部"开始学习"；进度显示 `已学习习题 X / 全部习题 Y`。
- 新增学习页 `LearnScreen` / `LearnViewModel`：章节资源学习、5 种题型作答、历史提交记录（对错/正确答案/解析/时间）。
- 字段坑：`KnowledgeResourceDto.importanceLevel` 后端是字符串（"normal"），`fileSize` 可为 null——模型已按真实类型修正；判断题(type=2)答案需转 `true/false` 才与后端口径一致。
- 题型枚举（真实）：0单选 1多选 2判断 3填空 4简答。
- ✅ 实测：detail / my-exercise-progress / by-chapter / records-by-chapter 全部 200；提交真实习题答案后进度 2/8→3/8、记录数 1→2（验证后已清理测试数据恢复原状）。

### 2026-11-21 服务假死恢复 + 401 自动跳登录
- **用户反馈**："检查下服务的状态，现在看不到数据"。
- **排查结论**：44305 端口 LISTEN 但 `/health-status` 超时，后端进程假死；账户服务 8080、KH 数据库 5433、账户数据库 55433、模拟器 `emulator-5554` 均正常。
- **恢复动作**：停掉旧后端后台任务、清理 44305 残留，用一键脚本中的 UnifiedAuth 环境变量重新启动 `KnowledgeHub.HttpApi.Host.dll`；重启后 health 200，`zmq / 123456`（qidi）登录成功，`/api/app/learning/my-courses` 返回 7 门课程。
- **连带问题**：后端重启后 App 本地旧 token 失效，`my-courses` 返回 401，界面停在"请求失败"。
- **修复**：
  - 新增 `data/AuthEvents.kt`：`MutableSharedFlow<Unit>` 广播 unauthorized。
  - `ApiClient.execute()` 收到 401 时调用 `AuthEvents.notifyUnauthorized()`，并抛友好错误"登录已过期，请重新登录"。
  - `MainActivity.AppNav` 收集事件后 `session.clear()` 并置 `isLoggedIn=false`，自动回登录页。
- ✅ 实测：后端重启后 App 自动回登录页；重新登录后学生端首页正常显示"学习概览"（选课 7 / 学习中 4 / 已完成 1 / 平均 28%）与"我的课程 7 门"，课程卡片正常渲染，品牌顶栏"易课通 / 学生端"正常。
- 注：当前 shell 未加载 SDK PATH 时，adb 绝对路径为 `/opt/homebrew/share/android-commandlinetools/platform-tools/adb`。

### 2026-11-21 学习页移动版改造 + 资源/习题真实可用
- **用户反馈**：App 要符合安卓手机软件界面；资源与习题预览/学习有问题；章节要保留层级但不能用侧边栏；选章后要有大空间学习。
- **导航重构**：`LearnScreen` 拆成两步——移动版「章节目录」（可折叠层级 + 搜索 + 资源数/子章节）→「章节学习」全屏页（资源 / 习题 / 提交记录三个 Tab）。取消原 `Row(0.32/0.68)` 侧边章节布局。
- **章节层级**：`flattenChapters()` 递归保留父子关系，按 `depth` 缩进；父节点可展开/收起，点击行直接进入该章学习。
- **资源预览**：
  - `detectResourceKind()` 从 `fileExtension / originalFileName / content` 识别类型（旧资源扩展名在 `content` 路径里）。
  - `ApiClient.withAccessToken()` 为 WebView/外部打开追加 `access_token` 查询参数（后端 GrantAllPoliciesMiddleware 放行）。
  - 文本直接取内容；图片走 WebView `loadUrl`；Office 轮询 `/preview-pdf-info` 就绪后取 `/preview-pdf`；PDF/Office 用 `PdfRenderer` 原生逐页渲染，带翻页控件。
  - 音视频下载字节到缓存，用系统 `VideoView + MediaController` 播放，避免 WebView 黑屏与 ANR；超过 25MB 降级为“请下载后查看 + 外部打开”。
- **习题作答**：兼容 JSON 数组/对象/换行选项；判断题生成“正确/错误”；支持重新作答、提交后查看答案与解析、记录列表展示对错/答案/时间；进度仍由后端 `my-exercise-progress` 计算。
- **关键 bug 修复**：
  - `recordResourceStudy` 与 `submitAnswer` 原用 `mapOf` 混合 String/Int/Long，被推断为 `Map<String, Any>`，运行时报 `Serializer for class 'Any' is not found`；改用 `buildJsonObject`。
  - 历史答案归一化误把非判断题的 `A` 转成 `true`；改为仅 `exerciseType==2` 时转换。
  - 非判断题答案显示误调用 `formatBoolAnswer`（`A` 显示为“正确”）；改为仅判断题转换。
  - `WebView.loadDataWithBaseURL` 加载媒体页在主线程同步 JNI 等待触发 ANR；改为异步 `loadUrl`，最终音视频改为原生播放器。
- ✅ 实测：章节目录层级/折叠/搜索正常；docx「简历资源」在 App 内逐页显示（1/3，可翻页）；mp4 视频正常播放画面；多选题重新作答可提交并显示“回答正确/错误”；验证后已把记录恢复为原始答案 `A`（API 确认 `studentAnswer=A, isCorrect=false`）。

### 2026-11-21 课程详情移动版优化
- **用户反馈**：课程详情的主讲教师、专业等信息要用手机版本排版；详情页的章节目录也要有层级。
- `CourseDetailView` 重做：封面 Hero（有封面图则展示，叠加课程标签与标题）、独立「主讲教师」卡片、两列信息网格（专业 / 学期 / 章节 / 学生，学分/学时可选）、课程简介、学习进度卡片、吸顶 Tab（课程资源 / 章节目录）。
- `CourseDetailDto` 补齐后端已有字段 `semester / credits / semesterHours / difficulty / status`，用于移动端信息展示。
- 章节目录改为递归层级列表：按 `depth` 缩进、父节点展开/收起、显示资源数与子章节数；不再平铺全部章节。
- 支持从详情页层级目录直接进入指定章节：`StudentScreen` 增加 `learningChapterId`，`LearnScreen` 增加 `initialChapterId`，课程详情点击章节后自动打开该章学习页；底部“开始学习”仍进入章节目录。
- ✅ 实测：课程详情移动端排版清晰（教师卡片、专业/学期/章节/学生网格、简介、进度均正常）；章节目录显示“第一章 → 1.1 章节”“设计表现基础概论 → 子章节”等层级与折叠箭头；点击“1.1 章节”直接进入该章节学习页。

### 2026-11-21 学生端扩展：课程中心 / 学习统计 / 设置
- **用户反馈**：发现课程要对接 Web `/student/courses` 的统一课程列表（未选课也能查看并预览）；增加 `/student/my-learning` 风格学习统计 Tab；增加设置 Tab 查看账户信息、修改密码。
- 课程中心：
  - 旧实现 `POST /api/app/course/get-published` 不是 Web 口径；改为 `GET /api/app/course/published`，并新增共享课程 `GET /api/app/course-share/shared-to-me`、公共课程 `GET /api/app/course/public-library-courses`、专业字典 `GET /api/app/major/lookup-list`。
  - `CourseCenterItem` 统一三类课程卡片：本校/共享/公共标签、来源学校、专业、教师、章节/学时/学分/学习人数、已选课徽章与进度；支持关键词和专业筛选。
  - 选课路由修正为 Web 同款 `POST /api/app/course/enroll/{courseId}`；课程详情底部按 `isEnrolled` 显示「报名学习 / 开始学习」，未选课可先看简介、章节和资源。
- 学习统计：
  - `LearningDashboard` 补齐 `totalResourceActivities / knowledgeDimensions / masteryValues`；新增最近习题记录 `GET /api/app/student-exercise-record/my-recent-records`（全部 / 正确 / 错误筛选）。
  - 移动端展示四项指标、平均进度、7 日活动柱状条、知识点掌握度、最近学习、进行中课程、最近习题记录。
  - 后端 `TotalLearningTime` 实为分钟数，界面按「分钟」展示，避免误标为小时。
- 设置：
  - 新增 `MyProfileDto / UpdateMyProfileDto / ChangePasswordInput`，`ApiClient.putJson()`；对接 `GET/PUT /api/app/user/my-profile` 与 `POST /api/account/my-profile/change-password`。
  - 支持查看登录账号、姓名、实名、邮箱/手机验证状态、脱敏证件号；编辑资料与修改密码有校验和 Snackbar 反馈。
- ✅ 实测：`compileDebugKotlin`、`assembleDebug` 成功；7 个新增接口均返回 200；模拟器验证四个 Tab、三类课程筛选、未选课课程详情、学习统计与设置页均正常显示。

### 2026-11-22 学生端工作台首页与标准导航架构
- **用户反馈**：学生端加类似工作台的首页，显示当前可用模块并可跳转；参考常见安卓 App 的导航 Tab 和页面组织，让用户清楚了解并使用功能，后续新模块能快速跳转。
- 新增 `WorkbenchTab.kt`：欢迎卡片（用户名/租户）、学习概览四项指标、常用模块卡片、继续学习课程、查看全部课程、即将开放模块。
- 新增 `AppModules.kt`：模块注册表 + 通用底部导航。工作台卡片和底部导航均从注册表读取；`studentUpcoming` 预留作业、资源库、消息通知三个占位模块。
- `StudentScreen.kt`：一级页面由数组下标改为模块 ID；统一 `selectModule/reloadModule`；新增 `BackHandler` 实现逐级返回；登录页返回后仍保持学生端角色分流。
- ✅ 实测：`compileDebugKotlin`、`assembleDebug` 成功；模拟器 `emulator-5554` 登录 qidi `zmq` 后工作台显示「学习中 4 / 已完成 1 / 习题记录 25 / 累计 184 分」和 7 门继续学习课程；点击工作台「课程中心」卡片正确进入课程中心，顶部标题与底部选中态同步；底部 5 个 Tab 均可切换；即将开放模块为灰色占位且不误跳页面。
