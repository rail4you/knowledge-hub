# 易课通 Android 客户端（Kotlin + Compose）

一个 App、单一登录界面，按账号角色（Student / Teacher）自动分流到学生端或教师端。
复用易课通后端（ABP Framework）已有的 REST API，专注打通**账户系统**与**课程学习系统**。

## 快速开始

前置条件：JDK 21、Android SDK（platform 34）、后端服务运行在 `https://localhost:44305`。

```bash
cd android-client
./gradlew assembleDebug
adb install -r app/build/outputs/apk/debug/app-debug.apk
```

- 模拟器访问宿主机后端用 `10.0.2.2`（已在 `app/build.gradle.kts` 配好）。
- 真机调试：把 `API_BASE_URL` 改为电脑局域网 IP。

## 目录结构

```
app/src/main/java/com/knowledgehub/mobile/
├── MainActivity.kt            入口（会话恢复 + 角色分流）
├── data/                      SessionManager、ApiClient、Repository、DTO
├── theme/                     品牌主题
└── ui/{login,student,teacher} Compose 界面
```

## 已打通

- 登录（租户选择 + 密码登录 + token 持久化）、角色分流、退出登录
- 学生端：学习概览、我的课程、发现课程、课程详情/章节、选课、记录进度
- 教师端：我教的课程、课程学习概览、学生学习情况

## 文档

- [ANDROID_MIGRATION_PLAN.md](./ANDROID_MIGRATION_PLAN.md) — 环境现状、MVP 能力、后续分阶段迁移路线、技术债清单

> 注意：`ApiClient.kt` 中信任自签证书的代码仅用于本地联调，上线前必须替换为正式证书策略。
