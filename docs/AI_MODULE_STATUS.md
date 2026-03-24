# AI 功能模块开发状态

## 概述

为 KnowledgeHub 平台开发 5 个 AI 功能模块，使用 Microsoft Agent Framework + Qwen Plus API。

## 架构

```
┌─────────────────────────────────────┐
│  Angular Frontend                   │
│  - 智能问答                          │
│  - 教案生成                          │
│  - 案例分析                          │
│  - 职业规划                          │
│  - 模型管理                          │
└──────────────┬──────────────────────┘
               │ HTTP POST + SSE (AG-UI Protocol)
               ▼
┌─────────────────────────────────────┐
│  KnowledgeHub.AI.Api (Port 5000)    │
│  - KnowledgeHubAgent (/)            │
│  - LessonPlanAgent (/lesson-plan)   │
│  - CaseAnalysisAgent (/case-analysis)│
│  - CareerGuidanceAgent (/career-guidance)│
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  Qwen Plus API (DashScope)          │
│  https://dashscope.aliyuncs.com     │
└─────────────────────────────────────┘
```

---

## 功能状态

| 功能 | 后端 | 前端组件 | HTML模板 | 样式 | 测试 |
|------|:----:|:--------:|:--------:|:----:|:----:|
| 智能问答 | ✅ | ✅ | ✅ | ✅ | ✅ |
| 教案生成 | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| 案例分析 | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| 职业规划 | ✅ | ✅ | 🚧 | 🚧 | ❌ |
| 模型管理 | ✅ | 🚧 | ❌ | ❌ | ❌ |

图例: ✅ 完成 | 🚧 进行中 | ⚠️ 部分完成 | ❌ 未开始

---

## 后端文件

### AI API 项目 (`src/KnowledgeHub.AI.Api/`)

| 文件 | 说明 | 状态 |
|------|------|:----:|
| `Program.cs` | 主入口，注册4个Agent | ✅ |
| `KnowledgeHub.AI.Api.csproj` | 项目配置 | ✅ |
| `appsettings.json` | 配置文件(Qwen API Key) | ✅ |
| `Models/LessonPlan.cs` | 教案结构化输出模型 | ✅ |
| `Models/CaseAnalysis.cs` | 案例分析结构化输出模型 | ✅ |
| `Models/CareerAdvice.cs` | 职业建议结构化输出模型 | ✅ |
| `Models/Feedback.cs` | 反馈和模型管理模型 | ✅ |

### Agent 端点

| 端点 | Agent名称 | 说明 |
|------|-----------|------|
| `/` | KnowledgeHubAgent | 智能问答 |
| `/lesson-plan` | LessonPlanAgent | 教案生成 |
| `/case-analysis` | CaseAnalysisAgent | 案例分析 |
| `/career-guidance` | CareerGuidanceAgent | 职业规划 |

### API 端点

| 端点 | 方法 | 说明 |
|------|------|------|
| `/info` | GET | 获取所有Agent信息 |
| `/api/feedback` | POST | 提交反馈 |
| `/api/feedback/stats` | GET | 获取反馈统计 |
| `/api/models` | GET | 获取模型列表 |
| `/api/models/upload` | POST | 上传模型文件 |

---

## 前端文件

### Angular 组件 (`angular/src/app/ai/`)

| 文件 | 说明 | 状态 |
|------|------|:----:|
| `services/chat.service.ts` | AI聊天服务(支持多Agent) | ✅ |
| `chat/chat.component.ts` | 智能问答组件 | ✅ |
| `chat/chat.component.html` | 智能问答模板 | ✅ |
| `chat/chat.component.scss` | 智能问答样式 | ✅ |
| `lesson-plan/lesson-plan.component.ts` | 教案生成组件 | ✅ |
| `lesson-plan/lesson-plan.component.html` | 教案生成模板 | ✅ |
| `lesson-plan/lesson-plan.component.scss` | 教案生成样式 | ✅ |
| `case-analysis/case-analysis.component.ts` | 案例分析组件 | ✅ |
| `case-analysis/case-analysis.component.html` | 案例分析模板 | ✅ |
| `case-analysis/case-analysis.component.scss` | 案例分析样式 | ✅ |
| `career-guidance/career-guidance.component.ts` | 职业规划组件 | ✅ |
| `career-guidance/career-guidance.component.html` | 职业规划模板 | 🚧 |
| `career-guidance/career-guidance.component.scss` | 职业规划样式 | 🚧 |
| `model-management/model-management.component.ts` | 模型管理组件 | ❌ |

### 路由配置

| 文件 | 说明 | 状态 |
|------|------|:----:|
| `app.routes.ts` | 路由配置 | ✅ |
| `route.provider.ts` | 导航菜单配置 | 🚧 |

### 路由

| 路径 | 组件 | 说明 |
|------|------|------|
| `/ai/chat` | ChatComponent | 智能问答 |
| `/ai/lesson-plan` | LessonPlanComponent | 教案生成 |
| `/ai/case-analysis` | CaseAnalysisComponent | 案例分析 |
| `/ai/career-guidance` | CareerGuidanceComponent | 职业规划 |

---

## 待完成任务

### 高优先级

1. [ ] 完成职业规划组件 HTML 模板
2. [ ] 完成职业规划组件 SCSS 样式
3. [ ] 创建模型管理组件
4. [ ] 更新 `route.provider.ts` 添加智能问答模块到管理端导航
5. [ ] 添加本地化文本 (`zh-Hans.json`)

### 中优先级

6. [ ] 测试所有 AI 功能端到端流程
7. [ ] 添加反馈评分 UI 组件
8. [ ] 优化错误处理和加载状态

### 低优先级

9. [ ] 添加语音输入功能
10. [ ] 添加关联教学资源显示
11. [ ] 数据库持久化 (ChatThreads, ChatMessages)

---

## 运行命令

```bash
# 启动 AI API (端口 5000)
./dev.sh start ai

# 启动所有服务
./dev.sh start

# 查看服务状态
./dev.sh status

# 查看 AI API 日志
./dev.sh tail ai
```

---

## 技术栈

### 后端
- .NET 10.0
- Microsoft.Agents.AI v1.0.0-rc4
- Microsoft.Extensions.AI v10.3.0
- OpenAI SDK v2.8.0
- Qwen Plus API (DashScope)

### 前端
- Angular 21
- ng-zorro-antd
- TypeScript

---

## API 配置

```json
// appsettings.json
{
  "Qwen": {
    "ApiKey": "YOUR_QWEN_API_KEY_HERE",
    "BaseUrl": "https://dashscope.aliyuncs.com/compatible-mode/v1",
    "Model": "qwen-plus"
  }
}
```

---

## 更新日志

### 2026-03-24
- ✅ 创建 AI API 项目结构
- ✅ 注册 4 个 Agent 端点
- ✅ 创建结构化输出模型
- ✅ 完成智能问答功能
- ✅ 完成教案生成组件
- ✅ 完成案例分析组件
- ✅ 创建职业规划组件 (TS)
- 🚧 更新 dev.sh 支持 AI 服务
