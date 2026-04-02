# 视频解析功能开发计划

## 概述

基于 Qwen3-VL 模型实现视频内容分析功能，支持时间轴事件提取和视频内容描述。

## 测试验证结果 (2026-04-02)

### API Key 验证
- ✅ Qwen API Key 有效 (`YOUR_QWEN_API_KEY_HERE`)
- ✅ qwen3-vl-plus 模型可用
- ✅ BaseUrl: `https://dashscope.aliyuncs.com/compatible-mode/v1`

### 视频分析测试
- ✅ 5秒视频片段：成功识别仓库打包场景
- ✅ 30秒视频片段：成功提取6个时间轴事件
- ✅ Timeline 提示词工作正常

### 测试用例结果

```json
{
  "events": [
    {
      "start_time": "00:00:00",
      "end_time": "00:00:03",
      "event": "一名身穿浅绿色长袖衬衫和米色高腰裤的女性站在仓库工作台后方..."
    },
    {
      "start_time": "00:00:04",
      "end_time": "00:00:06",
      "event": "女性将黄色信封包裹平稳放置在工作台中央..."
    }
    // ... 更多事件
  ]
}
```

---

## 当前实现状态

### 后端文件

| 文件 | 说明 | 状态 |
|------|------|:----:|
| `VideoAnalysisAppService.cs` | 视频分析服务 | ✅ |
| `IVideoAnalysisAppService.cs` | 服务接口 | ✅ |
| `VideoAnalysisDtos.cs` | DTO 定义 | ✅ |

### 服务功能

- ✅ `AnalyzeVideoTimelineAsync` - 分析视频时间轴
- ✅ `AnalyzeLocalVideoAsync` - 分析本地视频文件
- ✅ `SaveVideoTimelineToMeiliSearchAsync` - 保存视频时间轴到 Meilisearch
- ✅ 自动格式转换 (非 MP4 → MP4)
- ✅ 自动压缩 ( > 7MB)
- ✅ Base64 编码视频
- ✅ JSON 响应解析
- ✅ Token 使用统计
- ✅ 远程 URL 视频分析
- ✅ 资源创建时自动触发视频索引

---

## 技术细节

### API 调用格式

```json
{
  "model": "qwen3-vl-plus",
  "messages": [{
    "role": "user",
    "content": [
      {
        "type": "video_url",
        "video_url": {"url": "data:video/mp4;base64,..."},
        "fps": 1
      },
      {"type": "text", "text": "提示词..."}
    ]
  }]
}
```

### 配置项

```json
{
  "Qwen": {
    "ApiKey": "YOUR_QWEN_API_KEY_HERE",
    "BaseUrl": "https://dashscope.aliyuncs.com/compatible-mode/v1",
    "VisionModel": "qwen3-vl-plus"
  }
}
```

### 视频限制

| 参数 | 值 | 说明 |
|------|-----|------|
| 最大文件大小 | 7MB (base64后) | 超出则自动压缩 |
| fps | 1 | 每秒抽取1帧 |
| 支持格式 | MP4 | 其他格式自动转换 |

---

## 待完成任务

### 测试验证
1. [ ] 端到端测试：Angular 前端 → API → Qwen VL
2. [ ] 测试大视频文件压缩流程
3. [ ] 测试非 MP4 格式转换
4. [x] 测试资源创建自动触发视频索引

### 功能增强
5. [ ] 添加视频摘要生成功能
6. [ ] 添加视频内容搜索标记
7. [x] 支持远程 URL 视频分析
8. [x] 保存时间轴到 Meilisearch
9. [x] 资源创建自动触发视频索引

### 错误处理
7. [ ] 优化错误消息
8. [ ] 添加超时处理
9. [ ] 添加重试机制

### 文档
10. [ ] 编写 API 使用文档
11. [ ] 添加单元测试

---

## API 使用示例

### 本地视频分析
```bash
curl -X POST "https://localhost:44305/api/app/video-analysis/analyze-local-video?filePath=/path/to/video.mp4"
```

### 时间轴分析（本地文件）
```bash
curl -X POST "https://localhost:44305/api/app/video-analysis/analyze-video-timeline" \
  -H "Content-Type: application/json" \
  -d '{"filePath": "/path/to/video.mp4"}'
```

### 时间轴分析（远程 URL）
```bash
curl -X POST "https://localhost:44305/api/app/video-analysis/analyze-video-timeline" \
  -H "Content-Type: application/json" \
  -d '{"videoUrl": "https://www.w3schools.com/html/mov_bbb.mp4"}'
```

### 测试用公开视频 URL
| 来源 | URL |
|------|-----|
| W3Schools Sample | `https://www.w3schools.com/html/mov_bbb.mp4` |

---

## Meilisearch Videos 索引

### 索引结构

视频时间轴事件存储在 `videos` 索引中，每个时间轴事件作为独立文档存储：

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000_0",
  "videoId": "550e8400-e29b-41d4-a716-446655440000",
  "videoName": "兔子与蝴蝶动画",
  "videoUrl": "https://www.w3schools.com/html/mov_bbb.mp4",
  "startTime": "00:00:00",
  "endTime": "00:00:02",
  "eventDescription": "俯视镜头中，一只体型圆润...",
  "order": 0,
  "indexedAt": "2026-04-02T12:45:24Z"
}
```

### 索引设置

| 属性 | 类型 | 说明 |
|------|------|------|
| filterable | videoId, videoName, indexedAt | 可过滤 |
| searchable | videoName, eventDescription | 可搜索 |
| sortable | order, indexedAt, startTime | 可排序 |

### 保存时间轴到 Meilisearch

```bash
curl -X POST "https://localhost:44305/api/app/video-analysis/save-video-timeline-to-meili-search/{videoId}?videoName=视频名称&videoUrl=视频URL" \
  -H "Content-Type: application/json" \
  -d '{"events": [...], "rawContent": "..."}'
```

---

## 资源创建自动视频索引

当创建新资源时，系统会自动检测是否为视频资源并触发索引流程：

### 检测条件

满足以下任一条件即视为视频资源：
1. `ResourceType == ResourceType.Video`
2. 文件扩展名为视频格式：`.mp4`, `.mov`, `.avi`, `.mkv`, `.wmv`, `.flv`, `.webm`, `.m4v`, `.mpg`, `.mpeg`, `.3gp`, `.qt`

### 处理流程

```
资源创建 → 检测为视频 → 创建 VideoIndexingJob → 后台任务执行
    ↓
调用 Qwen VL API 分析视频时间轴
    ↓
保存时间轴到 Meilisearch videos 索引
```

### 相关文件

| 文件 | 说明 |
|------|------|
| `VideoIndexingJob.cs` | 视频索引任务实体 |
| `VideoIndexingJobArgs.cs` | 后台任务参数 |
| `VideoIndexingBackgroundJob.cs` | 后台任务处理器 |
| `ResourceAppService.cs` | 资源创建时检测并触发 |

### 数据库表

- `KhVideoIndexingJobs` - 视频索引任务表

---

## 参考文档

- [阿里云百炼 - 视觉理解](https://help.aliyun.com/zh/model-studio/vision)
- [Qwen3-VL 视频理解](https://help.aliyun.com/zh/model-studio/vision#071b239d9371c)